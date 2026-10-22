using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Modules.Guns;
using WildWasteland.Modules.HUD;
using WildWasteland.Modules.NPC;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>
    /// Gunplay module entry (README 3.1, 6.3): weapon catalog and stats, ballistics, projectiles and tracers, melee, ammo/magazines/unload/
    /// overflow, ironsights zoom, weapon switch timing and the vanilla hit observers. Owns the lifecycle, config, hooks, jobs, pages and
    /// ModAPI providers of everything under src/Modules/Gunplay (ex Guns module + the Gunplay parts of NPC, HUD and PlayerBody).
    /// Config: [Gunplay] / [Gunplay / &lt;Sub&gt;] (D12, no migration). Assets: plugins/WildWasteland/Gunplay/ (Sounds/knifestab.wav).
    /// Hook subscription order inside Init keeps the order the parts had across the old entries (PlayerBody -> NPC -> Guns -> HUD).
    /// </summary>
    public sealed class GunplayModule : ModuleBase
    {
        static readonly string[] _conflicts = { "com.denis.apocalypter.gunplay", "com.denis.apocalypter.apocaunloader" };

        /// <summary>The running instance (log/hook owner); null before the first Init.</summary>
        internal static GunplayModule Instance;
        /// <summary>BepInEx\plugins\WildWasteland\Gunplay\ (Sounds/knifestab.wav).</summary>
        internal static string AssetDir;
        static bool _running;

        /// <summary>True between Init and Shutdown.</summary>
        internal static bool Running { get { return _running; } }

        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Gunplay"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Gunplay (guns, ammo & tracers)"; } }

        /// <summary>The standalone Gunplay / Apocaunloader plugins replace this module (AGENTS 5.8).</summary>
        public override string[] ConflictsWith { get { return _conflicts; } }

        /// <summary>Binds every Gunplay entry (see <see cref="GunplaySettings.Bind"/>).</summary>
        public override void BindConfig(ModuleConfig cfg) { GunplaySettings.Bind(cfg); }

        /// <summary>Starts every part: statics reset, input/camera parts, hooks in the old cross-entry order, direct patches, jobs.</summary>
        public override void Init()
        {
            Instance = this;
            AssetDir = Dir;
            ResetAll();
            Ironsights.Init(this);
            WeaponTransition.Init(this);
            EmptyGunSound.Init(this);

            // ex PlayerBody: blast lance thrown from the seat ignores the own car (CreateObject postfix, first as before)
            Hooks.CreateObjectEnter.Postfix(this, LanceSeatCollision.AfterCreateObject);
            // ex NPC: vanilla shot/damage observers (VanillaEvents before the projectile prefixes, as with NPC -> Guns init order)
            Hooks.SetFsmFloatEnter.Postfix(this, VanillaEvents.AfterDamage);
            Hooks.RaycastEnter.Prefix(this, VanillaEvents.BeforePlayerShot);
            Hooks.SensorRayHitEnter.Prefix(this, VanillaEvents.BeforeNpcShot);
            Hooks.SensorRayHitEnter.Finalizer(this, VanillaEvents.AfterNpcShot);

            // ex Guns: shared targets through Core dispatchers
            Hooks.SensorRayHitEnter.Prefix(this, Tracers.BeforeRayHit);
            Hooks.RaycastEnter.Prefix(this, Tracers.BeforeRaycast);
            Hooks.SetFsmFloatEnter.Postfix(this, Tracers.AfterSetFsmFloat);
            Hooks.CreateObjectEnter.Prefix(this, Tracers.BeforeMeleeEffect);
            Hooks.CreateObjectEnter.Postfix(this, AfterCreateObject);
            Hooks.GetButtonDownUpdate.Prefix(this, ReloadTapHook.Prefix);
            Hooks.ActivateGameObjectEnter.Prefix(this, MagazineDrawHook.Prefix);
            Hooks.EnterState.Prefix(this, MagazineStateHook.Prefix);
            Hooks.EnterState.Postfix(this, MagazineStateHook.Postfix);
            Hooks.ProcessEvent.Prefix(this, UnloadEventHook.Prefix);

            // targets only this module uses
            Patch(typeof(GetLayer), "OnEnter", "AfterGetLayer", false);
            Patch(typeof(SetAudioClip), "OnEnter", "AfterSetAudioClip", false);
            Patch(typeof(TranslatePosition), "OnFixedUpdate", "BeforeTranslatePosition", true);
            Core.Patches.ApplyClass(this, typeof(MagazineLoadPatch));
            Core.Patches.ApplyClass(this, typeof(AmmoCapacityPatch));
            Core.Patches.ApplyClass(this, typeof(ArmorCapacityPatch));
            Core.Patches.ApplyClass(this, typeof(BackpackSlotPatch));

            // ex HUD: the vanilla player-hit observer feeding hit feedback while projectiles are off (prefix, observe only)
            Hooks.SetFsmFloatEnter.Prefix(this, VanillaHits.BeforeSetFsmFloat);
            Scheduler.EveryFrame(this, "Gunplay.Vanilla hit results", VanillaHits.Flush, Phase.LateUpdate);

            // the two old runner components (Gunplay.Runner, Gunplay.Ammo.AmmoRunner), same order; then the ex NPC sweep
            Scheduler.EveryFrame(this, "Gunplay.Tracers", TracersTick);
            Scheduler.EveryFrame(this, "Gunplay.Ammo", AmmoRunner.Tick);

            _running = true;
            SyncHud();
            if (Log.L1) Log.Main(this, "Gunplay started; ammunition management integrated, NPC/HUD services optional.");
        }

        /// <summary>Provides IGunsApi (old contract), IWeaponCatalog, IWeaponStats and IWeaponState.</summary>
        public override void ProvideApis()
        {
            ModApi.Provide<IRiderWeapons>(this,new RiderWeapons());
            ModApi.Provide<IGunsApi>(this, new GunsApi());
            ModApi.Provide<IWeaponCatalog>(this, WeaponCatalog.Instance);
            ModApi.Provide<IWeaponStats>(this, WeaponStats.Instance);
            ModApi.Provide<IWeaponState>(this, WeaponState.Instance);
        }

        /// <summary>Patches one PlayMaker action method with a Tracers handler (prefix or postfix).</summary>
        void Patch(Type action, string method, string handler, bool prefix)
        {
            var mi = AccessTools.Method(typeof(Tracers), handler);
            Core.Patches.Patch(this, AccessTools.Method(action, method), prefix: prefix ? mi : null, postfix: prefix ? null : mi);
        }

        /// <summary>Statics of every part back to their initial values, so Init works again after Shutdown.</summary>
        static void ResetAll()
        {
            Tracers.ResetAll();
            BackpackOverflow.Reset();
            ArmorOverflow.Reset();
            AmmoRunner.ResetAll();
            PerGunAmmo.ResetAll();
            Ironsights.Reset();
            WeaponTransition.Reset();
            VanillaEvents.OnSceneLoaded();
            VanillaHits.Clear();
        }

        /// <summary>Stops every part: camera/input parts, an unload in progress completes, bullets vanish, HUD observer resumes.</summary>
        public override void Shutdown()
        {
            _running = false;
            VanillaHits.Clear();
            Ironsights.Shutdown();
            WeaponTransition.Shutdown();
            try { AmmoRunner.Unloader.CompleteForSwitch(); } catch (Exception e) { Log.Error(this, "shutdown: unload", e); }
            Tracers.Shutdown();
            BackpackOverflow.Reset();
            ArmorOverflow.Reset();
            AmmoRunner.ResetAll();
            WeaponCatalog.Instance.ClearRegistered();
            WeaponStats.Instance.Clear();
            WeaponState.Instance.Clear();
            try { VanillaEvents.Shutdown(); } catch (Exception e) { Log.Error(this, "shutdown: VanillaEvents", e); }
            var h = Services.Hud;
            if (h != null) { try { h.ExternalReporter = false; } catch (Exception e) { Log.Warn(this, "HUD ExternalReporter: " + e.Message); } }
        }

        /// <summary>Snapshots the actual magazine ledger into the verified active world-save transaction.</summary>
        public override void OnSaveBegin(string slot){try{PerGunAmmo.SaveWorld(slot);}catch(Exception e){SaveBus.ReportFailure(this,"Magazine ledger save failed: "+e.Message);throw;}}

        /// <summary>Scene change: per-scene caches of every part are dropped.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Ironsights.Reset();
            WeaponTransition.Reset();
            Tracers.OnSceneLoaded();
            BackpackOverflow.Reset();
            ArmorOverflow.Reset();
            PerGunAmmo.SceneChanged();
            ReloadTapHook.Reset();     // keyed by the old scene's actions
            WeaponState.Instance.Clear();
            VanillaEvents.OnSceneLoaded();
            VanillaHits.Clear();
        }

        /// <summary>World ready: weapons holstered and ironsights off once the native FSMs run; impact effect prefabs looked up.</summary>
        public override void OnWorldReady() { WeaponTransition.StartWorld(); Ironsights.StartWorld(); Tracers.WarmEffects(); }

        /// <summary>FloatingOrigin jump: bullets in flight and landed bolts move with the world.</summary>
        public override void OnOriginShift(Vector3 delta) { Tracers.OriginShift(delta); VanillaHits.Shift(delta); }

        /// <summary>F7 pages (group Weapons &amp; HUD).</summary>
        public override IEnumerable<ToolPage> ToolPages() { return GunplayPages.Create(this); }

        /// <summary>Settings shell row: "Toggle Ironsights" on the HUD page at its original position.</summary>
        public override void Settings(SettingsShell shell) { shell.Custom(this, SettingsShell.Page.Hud, "", DrawSettingsRow, 160); }

        /// <summary>Draws the "Toggle Ironsights" switch.</summary>
        void DrawSettingsRow(ToolLayout l) { SettingsShell.Switch(l, "Toggle Ironsights", GunplaySettings.ToggleIronsights); }

        // ---------------------------------------------------------------- jobs

        /// <summary>Job "Gunplay.Tracers" (every frame): HUD reporter flag, then the projectile simulation and drawing.</summary>
        static void TracersTick()
        {
            SyncHud();
            try { Tracers.Tick(); } catch (Exception e) { Log.Error(Instance, "Tracers", e); }
        }

        /// <summary>HUD's vanilla observer stays quiet while projectiles report hits (Services.Hud may appear after Init).</summary>
        internal static void SyncHud()
        {
            var h = Services.Hud;
            if (h == null) return;
            bool want = GunplaySettings.ProjectilesEnabled;
            if (h.ExternalReporter != want) h.ExternalReporter = want;
        }

        /// <summary>CreateObject.OnEnter postfix: registers monster projectiles (orbs) and moves landed bolts onto spawned corpses.</summary>
        static void AfterCreateObject(CreateObject __instance)
        {
            if (__instance.storeObject == null || __instance.gameObject == null) return;
            var made = __instance.storeObject.Value;
            Tracers.OrbSpawned(__instance, made);
            Tracers.BoltCorpseSpawned(__instance, made);
        }
    }
}
