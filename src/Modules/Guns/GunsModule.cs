using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// Old Guns entry, now a shell (B09): every Gunplay feature moved to GunplayModule ([Gunplay] config). What remains are the partial
    /// parts other packages still own inside this entry: NPCAI human boss health (config [Guns] BossHealth / [Guns / Bosses], hook, job,
    /// CreateObject registration, F7 page), Compatibility bridges (Apocaspawner, Weapons Pack, BloodFX), the HUD crossbow tooltip patch and
    /// the Saves ES3 magazine patches. Each owner moves its part to its own entry and empties the partial method; Wave C deletes the shell.
    /// </summary>
    public sealed partial class GunsModule : ModuleBase
    {
        /// <summary>GUID of the standalone Gunplay plugin (conflict).</summary>
        public const string LegacyGuid = "com.denis.apocalypter.gunplay";
        /// <summary>GUID of the standalone Apocaunloader plugin (conflict).</summary>
        public const string UnloaderGuid = "com.denis.apocalypter.apocaunloader";
        static readonly string[] _conflicts = { LegacyGuid, UnloaderGuid };

        /// <summary>Module id (config section prefix of the remaining partial parts).</summary>
        public override string Id { get { return "Guns"; } }
        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Guns (legacy parts: bosses, compatibility)"; } }
        /// <summary>The standalone Gunplay / Apocaunloader plugins replace this entry as before.</summary>
        public override string[] ConflictsWith { get { return _conflicts; } }

        /// <summary>The running shell instance: owner of the parts above (Compatibility bridges patch/schedule with it).</summary>
        internal static GunsModule Instance;

        /// <summary>Level-1 log line of the remaining parts (NPCAI bosses).</summary>
        internal static void Verbose(string message) { if (Log.L1) Log.Main(Instance, message); }

        /// <summary>Binds the NPCAI boss entries at their old positions.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            BindNpcaiToggles(cfg);
            BindNpcaiValues(cfg);
        }

        /// <summary>Starts the remaining partial parts in their old relative order.</summary>
        public override void Init()
        {
            Instance = this;
            ResetNpcai();
            InitNpcaiHooks();
            Hooks.CreateObjectEnter.Postfix(this, AfterCreateObject);
            InitCompatibility();
            InitHud();
            InitSaves();
            InitNpcaiJobs();
        }

        /// <summary>Stops the remaining partial parts.</summary>
        public override void Shutdown()
        {
            ShutdownCompatibility();
            ResetNpcai();
        }

        /// <summary>Scene change: the boss registry is dropped.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ResetNpcai(); }

        /// <summary>CreateObject.OnEnter postfix: boss registration (NPCAI part).</summary>
        static void AfterCreateObject(CreateObject __instance)
        {
            if (__instance.storeObject == null || __instance.gameObject == null) return;
            BossCreated(__instance.gameObject.Value, __instance.storeObject.Value);
        }

        // ---------------------------------------------------------------- Wave A partial parts (implemented in the target module folders)
        /// <summary>NPCAI: binds the boss health toggle (F6 feature-toggle position).</summary>
        partial void BindNpcaiToggles(ModuleConfig cfg);
        /// <summary>NPCAI: binds [Guns / Bosses] AdjustHumanBossHP (F6 value position).</summary>
        partial void BindNpcaiValues(ModuleConfig cfg);
        /// <summary>NPCAI: boss SensorGetDetections hook.</summary>
        partial void InitNpcaiHooks();
        /// <summary>NPCAI: the "Bosses" interval job.</summary>
        partial void InitNpcaiJobs();
        /// <summary>NPCAI: resets the boss registry (Init, Shutdown, scene load).</summary>
        static partial void ResetNpcai();
        /// <summary>NPCAI: boss registration from the CreateObject postfix.</summary>
        static partial void BossCreated(GameObject prefab, GameObject made);
        /// <summary>Compatibility: Apocaspawner damage patch.</summary>
        partial void InitCompatibility();
        /// <summary>Compatibility: lazy Weapons Pack / BloodFX bridges (from the Bosses job).</summary>
        static partial void EnsureCompatibility();
        /// <summary>Compatibility: bridge teardown.</summary>
        static partial void ShutdownCompatibility();
        /// <summary>HUD: crossbow tooltip damage text patch.</summary>
        partial void InitHud();
        /// <summary>Saves: ES3 patches of the per-gun magazine ledger.</summary>
        partial void InitSaves();

        /// <summary>F7 pages of the remaining parts (NPCAI "Bosses &amp; orbs").</summary>
        public override IEnumerable<ToolPage> ToolPages() { return GunsPages.Create(this); }
    }
}
