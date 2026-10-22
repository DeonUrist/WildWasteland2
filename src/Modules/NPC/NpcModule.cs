using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.NPC
{
    // NPC Overhaul (NPCAI 1.9.15) as a Wild Wasteland module: senses / detection, structure navigation (map bakes + routing), the brain,
    // idle behaviour, animals (burrowing scorpions, pouncing spiders, dogs, Dog Companion compatibility), hazards (spider nests and webs,
    // sandstorms) and the debug overlay. Every subsystem is still a static class with its own registry; the old NPCAI.Runner's Update /
    // LateUpdate / OnGUI are scheduler jobs registered in the old order (Senses -> Nav -> Brain -> Idle -> Passthrough -> Burrow -> Pounce ->
    // Webs -> Nests -> Ambush -> DogBite; late: Burrow -> Pounce -> DogBite), and the old Harmony patches are Core hooks or module patches.
    public sealed partial class NpcModule : ModuleBase
    {
        public const string GUID = "com.denis.apocalypter.npcai";      // the standalone mod this module replaces
        public override string Id { get { return "NPC"; } }
        public override string DisplayName { get { return "NPC AI & navigation"; } }
        public override string[] ConflictsWith { get { return new[] { GUID }; } }

        internal static NpcModule Instance;
        /// <summary>The module runs (Init done, Shutdown not yet). Every hook / patch handler and every On property checks it first.</summary>
        internal static bool Running;
        internal static string ModelDir;       // ex Plugin.Dir + "Models": BepInEx\plugins\WildWasteland\NPC\Models
        internal static string DataDir;        // BepInEx\config\WildWasteland\NPC

        bool _subscribed;

        // ------------------------------------------------------------------ parts owned by other target modules (Wave A split)
        // Each partial method below is implemented in a NpcModule.<Target>.cs file inside that target's _fromNPC folder; the calls sit
        // exactly where the moved lines were, so job / hook / patch / config order is unchanged. A Wave B owner takes over its part by
        // moving the body into its own module and leaving the partial method empty.

        /// <summary>Presentation: dog animation bundle, its hooks and patch (DogAnimations.Initialize).</summary>
        partial void InitPresentationDogs();
        /// <summary>Compatibility: Dog Companion bridge (DogCompanionCompat.Initialize).</summary>
        partial void InitCompatibilityDogCompanion();
        /// <summary>Presentation: Women of Wasteland (female bodies, voices, appearance providers, their jobs).</summary>
        partial void InitPresentationWomen();
        /// <summary>Compatibility: the Weapons Pack bridge job (registered between Brain and NPC.Rigs, as before).</summary>
        partial void InitCompatibilityJobs();
        /// <summary>Presentation: the NPC.Rigs sliced job and the LateUpdate visual ticks of Burrow / Pounce / DogBite.</summary>
        partial void InitPresentationJobs();
        /// <summary>Gunplay: the WeaponRanges.Sweep job (after Aim.Sweep, as before).</summary>
        partial void InitGunplayJobs();
        /// <summary>Core (Hotkeys): the player's shout key.</summary>
        partial void InitCoreShout();
        /// <summary>Tools: the POI pathfinding setup job (before PoiNav.Build, as before).</summary>
        partial void InitToolsPoiNavSetup();
        /// <summary>Tools: the POI navigation data view, its editor jobs and the editor hotkey (after PoiNav.Traverse, as before).</summary>
        partial void InitToolsPoiNavEditor();
        /// <summary>Presentation: Women of Wasteland shutdown step.</summary>
        static partial void ShutdownPresentationWomen();
        /// <summary>Compatibility: Weapons Pack shutdown step.</summary>
        static partial void ShutdownCompatibility();
        /// <summary>Gunplay: VanillaEvents shutdown step.</summary>
        static partial void ShutdownGunplay();
        /// <summary>Tools: closes the POI navigation editor, unhooks the data view and resets the setup mode.</summary>
        static partial void ShutdownTools();
        /// <summary>Compatibility: Weapons Pack reset (first step of ResetAll).</summary>
        static partial void ResetCompatibility();
        /// <summary>Gunplay: WeaponRanges / VanillaEvents scene caches (after Aim, as before).</summary>
        static partial void ResetGunplay();
        /// <summary>Tools: POI navigation editor close + setup reset (before PoiNav.Reset, as before).</summary>
        static partial void ResetTools();
        /// <summary>Presentation: Gungirl scene caches (after ResetAll on scene load).</summary>
        static partial void SceneLoadedPresentation();
        /// <summary>Presentation: Gungirl world scan when the world is ready.</summary>
        static partial void WorldReadyPresentation();
        /// <summary>Compatibility: Weapons Pack reset when a save starts loading.</summary>
        static partial void LoadBeginCompatibility();
        /// <summary>Tools: origin shift of the POI navigation setup copies and the editor.</summary>
        static partial void ShiftTools(Vector3 delta);
        /// <summary>Gunplay: vanilla shot / damage observers (SetFsmFloat first: before DogBite's subscriber, as before).</summary>
        partial void InstallHooksGunplay();
        /// <summary>Presentation: CreateObject postfix of Pounce (after Senses / Idle, as before).</summary>
        partial void InstallHooksPresentation();
        /// <summary>Compatibility: CreateObject postfix of the Weapons Pack bridge (after Pounce, as before).</summary>
        partial void InstallHooksCompatibility();
        /// <summary>Core (Hotkeys): the shout modifier blocks the game's buttons (GetButton* prefixes).</summary>
        partial void InstallHooksCore();
        /// <summary>Presentation: the AudioPlay prefix that swaps NPC combat taunts.</summary>
        partial void InstallPatchesPresentation();

        // ------------------------------------------------------------------ lifecycle

        public override void Init()
        {
            try { Start(); }
            catch (Exception)
            {
                // a half-started module must not leave Services.Npc / event handlers / changed NPCs behind (Core only removes patches, jobs, hooks)
                try { Shutdown(); } catch (Exception) { }
                throw;
            }
        }

        /// <summary>Init body: services, events, hooks, patches, feature parts and every job in the old NPCAI.Runner order.</summary>
        void Start()
        {
            Instance = this;
            ModelDir = Path.Combine(Dir, "Models");
            DataDir = Assets.ModuleConfigDir(Id);
            ResetAll();                                    // re-runnable after Shutdown: every registry starts empty

            Services.Npc = new NpcApi();
            Services.CrewBailedOut += OnCrewBailedOut;     // ex Senses.HookApocapatrol (Harmony on Apocapatrol.Patrol.BailOut / Explode.Blast)
            Services.VehicleExploded += OnVehicleExploded;
            SaveBus.StateChanged += Senses.OnSaveLoadState;   // ex Senses.Persist.Tick polling the SaveLoadGame FSM
            _subscribed = true;

            InstallHooks();
            InstallPatches();
            InitPresentationDogs();                        // bundle + its own hooks / patch (only when the bundle loads)
            InitCompatibilityDogCompanion();               // only when Dog Companion is installed; [NPC] DogCompanionCompat switches it live
            InitPresentationWomen();

            // the old NPCAI.Runner, in its order (load-bearing: Brain/Idle body hand-over, DogAnimations restore in Update / sample in LateUpdate)
            Scheduler.EveryFrame(this, "Senses", Senses.Tick);
            Scheduler.EveryFrame(this, "Nav", Nav.Tick);
            Scheduler.EveryFrame(this, "Brain", Brain.Tick);
            InitCompatibilityJobs();                       // "Weapons Pack compatibility"
            InitPresentationJobs();                        // "NPC.Rigs" + the LateUpdate visual ticks (only LateUpdate jobs: order within the phase kept)
            Scheduler.EveryFrame(this, "Idle", Idle.Tick);
            Scheduler.Interval(this, "Passthrough", 0.25f, Passthrough.Tick, Phase.Update, true);
            Scheduler.EveryFrame(this, "Burrow", Burrow.Tick);
            Scheduler.EveryFrame(this, "Pounce", Pounce.Tick);
            Scheduler.EveryFrame(this, "Webs", Webs.Tick);
            Scheduler.EveryFrame(this, "Nests", Nests.Tick);
            Scheduler.Interval(this, "Ambush", 0.5f, Ambush.Tick, Phase.Update, true);
            Scheduler.EveryFrame(this, "DogBite", DogBite.Tick);
            Scheduler.Interval(this, "Aim.Sweep", 10f, Aim.Sweep);
            InitGunplayJobs();                             // "WeaponRanges.Sweep"


            InitCoreShout();                               // [Keys] Shout

            // (WW) POI navigation: setup mode + bake queue, link traversals, the data view and its editor
            InitToolsPoiNavSetup();                        // "PoiNav.Setup"
            Scheduler.Sliced(this, "PoiNav.Build", 500, PoiNav.BuildStep);
            Scheduler.EveryFrame(this, "PoiNav.Traverse", PoiNavTraverse.Tick, Phase.EarlyFixedUpdate);
            // (A3) the runtime walker sweep ran inside the "PoiNav.View" job (Tools); now its own NPCAI job, registered right before it
            Scheduler.EveryFrame(this, "PoiNav.Walkers", PoiNav.WalkerTick);
            InitToolsPoiNavEditor();                       // "PoiNav.View", "PoiNav.Editor", "PoiNav.EditorCamera", "PoiNav.EditorGui", editor key

            Running = true;
            Log.LogInfo("NPC Overhaul 1.9.15 port started");
        }

        public override void Shutdown()
        {
            // first: every handler / On property sees the module off (FSM events sent below re-enter our hooks synchronously)
            Running = false;

            if (_subscribed)
            {
                Services.CrewBailedOut -= OnCrewBailedOut;
                Services.VehicleExploded -= OnVehicleExploded;
                SaveBus.StateChanged -= Senses.OnSaveLoadState;
                _subscribed = false;
            }
            if (Services.Npc is NpcApi) Services.Npc = null;
            Step("POI navigation", PoiNavShutdown);
            ShutdownPresentationWomen();
            // hand every NPC back to the game, in reverse dependency order
            Step("Pounce", Pounce.Shutdown);
            Step("Burrow", Burrow.Shutdown);
            Step("DogBite", DogBite.Shutdown);          // + DogAnimations + Dog Companion drivers
            Step("Nests", Nests.Shutdown);
            Step("Webs", Webs.Shutdown);
            Step("Passthrough", Passthrough.Shutdown);
            Step("Idle", Idle.Shutdown);
            Step("Brain", Brain.Shutdown);
            ShutdownCompatibility();
            Step("Aim", Aim.Shutdown);
            ShutdownGunplay();
            Step("Senses", Senses.Shutdown);
            ResetAll();

            Instance = null;
        }

        /// <summary>The "POI navigation" shutdown step: the Tools part (editor, view, setup) then the runtime graph.</summary>
        static void PoiNavShutdown() { ShutdownTools(); PoiNav.Reset(); }

        /// <summary>Runs one shutdown step; an exception is logged and the next step still runs.</summary>
        static void Step(string what, Action a)
        {
            try { a(); } catch (Exception e) { Core.Log.Error("[NPC] shutdown " + what, e); }
        }

        /// <summary>Clears every registry / cache (the old Plugin.SceneLoaded chain). Init and Shutdown call it too.</summary>
        static void ResetAll()
        {
            ResetCompatibility();
            Aim.OnSceneLoaded(); ResetGunplay(); Brain.OnSceneLoaded(); Senses.OnSceneLoaded(); Nav.OnSceneLoaded(); Idle.OnSceneLoaded();
            Passthrough.OnSceneLoaded(); Burrow.OnSceneLoaded(); Pounce.OnSceneLoaded(); Webs.OnSceneLoaded(); Nests.OnSceneLoaded(); Ambush.OnSceneLoaded(); DogBite.OnSceneLoaded();
            ResetTools(); PoiNav.Reset();
        }

        /// <summary>Scene load: every registry reset, then the Presentation (Gungirl) caches.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ResetAll(); SceneLoadedPresentation(); }
        /// <summary>World ready: the Presentation (Gungirl) world scan starts.</summary>
        public override void OnWorldReady() { WorldReadyPresentation(); }
        /// <summary>A save starts loading: the Compatibility (Weapons Pack) state is reset.</summary>
        public override void OnLoadBegin(string slot) { LoadBeginCompatibility(); }

        // ------------------------------------------------------------------ floating origin
        // The game's FloatingOrigin moves the root objects of every loaded scene by delta (not DontDestroyOnLoad). NPCAI kept scene-space
        // positions across frames; they are translated here (plain Vector3 / Bounds / float data) or dropped where a rebuild is cheap
        // (route caches). See notes/port-NPC.md "Origin shift".
        public override void OnOriginShift(Vector3 delta)
        {
            Step("Senses.Shift", () => Senses.Shift(delta));
            Step("Nav.Shift", () => Nav.Shift(delta));
            Step("Brain.Shift", () => Brain.Shift(delta));
            Step("Idle.Shift", () => Idle.Shift(delta));
            Step("Burrow.Shift", () => Burrow.Shift(delta));
            Step("Pounce.Shift", () => Pounce.Shift(delta));
            Step("Nests.Shift", () => Nests.Shift(delta));
            Step("Webs.Shift", () => Webs.Shift(delta));
            Step("PoiNav.Shift", () => { PoiNav.Shift(delta); ShiftTools(delta); });
        }

        // ------------------------------------------------------------------ Patrols events (ex Apocapatrol postfixes)
        static void OnCrewBailedOut(GameObject car, GameObject pax, GameObject npc) { if (Running) Senses.AfterBailOut(car, pax, npc); }
        static void OnVehicleExploded(GameObject car) { if (Running) Senses.AfterBlast(car); }

        // ------------------------------------------------------------------ Harmony
        /// <summary>Core.Hooks subscriptions; per dispatcher the subscriber order is the old one (target parts are called so it stays).</summary>
        void InstallHooks()
        {
            InstallHooksGunplay();     // SetFsmFloat (VanillaEvents.AfterDamage before DogBite), Raycast.OnEnter, the NPC burst ray prefix + finalizer
            // SendEvent.OnEnter: Brain (Animal_Run while holding / Animal_rotateRandom) then Aim (Activate = the burst) - NPCAI's patch order
            Hooks.SendEventEnter.Prefix(this, Brain.BeforeSendEvent);
            Hooks.SendEventEnter.Prefix(this, Aim.BeforeSendEvent);
            // SetVelocity.DoSetVelocity: Brain (the pedal; registers NPCs) then DogAnimations (held / dashing dogs; only with the bundle, see DogAnimations.Initialize)
            Hooks.SetVelocityDo.Prefix(this, Brain.BeforeSetVelocity);
            // CreateObject.OnEnter postfixes: Senses (structures, pending rigs, blasts), Idle (camp spawn points); Pounce + Weapons Pack below
            Hooks.CreateObjectEnter.Postfix(this, Senses.AfterCreateObject);
            Hooks.CreateObjectEnter.Postfix(this, Idle.AfterCreateObject);
            // SetFsmFloat.OnEnter postfixes: vanilla hits -> Senses.Hurt (Gunplay part, subscribed first), then a real dog bite -> DogBite's lunge
            Hooks.SetFsmFloatEnter.Postfix(this, DogBite.AfterSetFsmFloat);
            // Raycast: DoRaycast = the Attack FSM's bumper rays (Brain), OnEnter = the player's vanilla shot (VanillaEvents, Gunplay part)
            Hooks.RaycastDo.Prefix(this, Brain.BeforeRaycast);
            Hooks.SmoothLookAtDo.Prefix(this, Brain.BeforeSmoothLookAt);
            Hooks.ProcessEvent.Prefix(this, Brain.BeforeDrivingHide);
            Hooks.ActivateGameObjectEnter.Postfix(this, Brain.AfterActivate);
            // SensorToolkit: the Detection FSM (registers agents), the burst LOS, the NPC burst ray (prefix + finalizer restores the ray length)
            Hooks.SensorDetectionsDo.Prefix(this, Senses.BeforeGetDetections);
            Hooks.LosEnter3D.Prefix(this, Senses.BeforeLosResult);
            Hooks.LosUpdate3D.Prefix(this, Senses.BeforeLosResult);
            InstallHooksPresentation();   // CreateObject postfix: Pounce's airborne carcass (after Senses / Idle)
            InstallHooksCompatibility();  // CreateObject postfix: Weapons Pack chainsaw corpse (after Pounce)
            InstallHooksCore();           // the shout modifier blocks the game's Kick / ShiftDown buttons
        }

        /// <summary>Module-only patches (targets no other module patches).</summary>
        void InstallPatches()
        {
            // targets only NPC patches: module patches (listed in the F7 "Harmony patches" page)
            Patch(typeof(Rotate), "DoRotate", typeof(Brain), "BeforeRotate", true);
            Patch(typeof(LookAt), "DoLookAt", typeof(Brain), "BeforeLookAt", true);
            Patch(typeof(AddForce), "DoAddForce", typeof(Brain), "BeforeAddForce", true);
            InstallPatchesPresentation();   // AudioPlay.OnEnter -> Senses.BeforeAudioPlay (taunt audio)
            Patch(typeof(RandomWait), "OnEnter", typeof(Aim), "BeforeRandomWait", true);
            var multi = AccessTools.TypeByName("HutongGames.PlayMaker.Actions.ActivateGameObjects");      // optional (not in every PlayMaker build)
            if (multi != null) Patch(multi, "OnEnter", typeof(Brain), "AfterActivate", false);
        }

        /// <summary>Patches target.method with patchType.patchMethod as a prefix or postfix through Core.Patches (recorded per module).</summary>
        internal bool Patch(Type target, string method, Type patchType, string patchMethod, bool prefix)
        {
            var original = AccessTools.Method(target, method);
            var mi = AccessTools.Method(patchType, patchMethod);
            return Core.Patches.Patch(this, original, prefix: prefix ? mi : null, postfix: prefix ? null : mi);
        }

        // ------------------------------------------------------------------ F7
        public override IEnumerable<ToolPage> ToolPages() { return NpcPages.Build(this); }
    }
}
