using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// Old NPC entry, now a shell (B07): the NPC AI (senses, brain, idle, animals, hazards) moved to NPCAIModule ([NPCAI] config). What
    /// remains are the partial parts other packages still own inside this entry: Presentation (dog animation bundle, Women of Wasteland,
    /// rig / visual LateUpdate jobs, Pounce carcass hook, taunt audio patch, their [NPC / ...] config and pages), Tools (POI navigation
    /// setup / view / editor jobs and page), and the emptied Compatibility / Gunplay / Core parts. Each owner moves its part to its own
    /// entry and empties the partial method; Wave C deletes the shell.
    /// </summary>
    public sealed partial class NpcModule : ModuleBase
    {
        /// <summary>GUID of the standalone NPCAI plugin (the shell stays off while it is installed, as before).</summary>
        public const string GUID = "com.denis.apocalypter.npcai";
        /// <summary>Module id (config section prefix and asset folder of the remaining parts: plugins/WildWasteland/NPC).</summary>
        public override string Id { get { return "NPC"; } }
        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "NPC (legacy parts: visuals, Women of Wasteland, POI nav editor)"; } }
        /// <summary>The standalone NPCAI plugin replaces this entry as before.</summary>
        public override string[] ConflictsWith { get { return new[] { GUID }; } }

        /// <summary>The running shell instance (owner of the remaining parts; Presentation reads its asset folder).</summary>
        internal static NpcModule Instance;
        /// <summary>[Obsolete] The NPC AI runs: forwards to the NPCAI module's running flag (Presentation/Gunplay/Compatibility readers).</summary>
        [Obsolete("refactor: NPCAI runtime state; use INpcApi/INpcControl through ModApi")]
        internal static bool Running { get { return NpcaiRuntime.Running; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>BepInEx\plugins\WildWasteland\NPC\Models (dog animation bundle, Presentation).</summary>
        internal static string ModelDir;
        /// <summary>[Obsolete] NPCAI config folder (config/WildWasteland/NPCAI): custom nav maps, exports (Navigation/Tools readers).</summary>
        [Obsolete("refactor: NPCAI data folder")]
        internal static string DataDir { get { return NpcaiRuntime.DataDir ?? Assets.ModuleConfigDir("NPCAI"); } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)

        // ------------------------------------------------------------------ parts owned by other target modules (Wave A split)
        // Each partial method below is implemented in a NpcModule.<Target>.cs file inside that target's folder. Empty ones stay until Wave C.

        /// <summary>Presentation: dog animation bundle, its hooks and patch (DogAnimations.Initialize).</summary>
        partial void InitPresentationDogs();
        /// <summary>Compatibility: Dog Companion bridge (empty since B17).</summary>
        partial void InitCompatibilityDogCompanion();
        /// <summary>Presentation: Women of Wasteland (female bodies, voices, appearance providers, their jobs).</summary>
        partial void InitPresentationWomen();
        /// <summary>Compatibility: the Weapons Pack bridge job (empty since B17).</summary>
        partial void InitCompatibilityJobs();
        /// <summary>Presentation: the NPC.Rigs sliced job and the LateUpdate visual ticks of Burrow / Pounce / DogBite.</summary>
        partial void InitPresentationJobs();
        /// <summary>Gunplay: the WeaponRanges.Sweep job (empty since B09).</summary>
        partial void InitGunplayJobs();
        /// <summary>Core (Hotkeys): the player's shout key (empty since B07: NPCAIModule registers it).</summary>
        partial void InitCoreShout();
        /// <summary>Tools: the POI pathfinding setup job.</summary>
        partial void InitToolsPoiNavSetup();
        /// <summary>Tools: the POI navigation data view, its editor jobs and the editor hotkey.</summary>
        partial void InitToolsPoiNavEditor();
        /// <summary>Presentation: Women of Wasteland shutdown step.</summary>
        static partial void ShutdownPresentationWomen();
        /// <summary>Compatibility: Weapons Pack shutdown step (empty since B17).</summary>
        static partial void ShutdownCompatibility();
        /// <summary>Gunplay: VanillaEvents shutdown step (empty since B09).</summary>
        static partial void ShutdownGunplay();
        /// <summary>Tools: closes the POI navigation editor, unhooks the data view and resets the setup mode.</summary>
        static partial void ShutdownTools();
        /// <summary>Compatibility: Weapons Pack reset (empty since B17).</summary>
        static partial void ResetCompatibility();
        /// <summary>Gunplay: WeaponRanges / VanillaEvents scene caches (empty since B09).</summary>
        static partial void ResetGunplay();
        /// <summary>Tools: POI navigation editor close + setup reset.</summary>
        static partial void ResetTools();
        /// <summary>Presentation: Gungirl scene caches.</summary>
        static partial void SceneLoadedPresentation();
        /// <summary>Presentation: Gungirl world scan when the world is ready.</summary>
        static partial void WorldReadyPresentation();
        /// <summary>Compatibility: Weapons Pack reset when a save starts loading (empty since B17).</summary>
        static partial void LoadBeginCompatibility();
        /// <summary>Tools: origin shift of the POI navigation setup copies and the editor.</summary>
        static partial void ShiftTools(Vector3 delta);
        /// <summary>Gunplay: vanilla shot / damage observers (empty since B09).</summary>
        partial void InstallHooksGunplay();
        /// <summary>Presentation: CreateObject postfix of Pounce.</summary>
        partial void InstallHooksPresentation();
        /// <summary>Compatibility: CreateObject postfix of the Weapons Pack bridge (empty since B17).</summary>
        partial void InstallHooksCompatibility();
        /// <summary>Core (Hotkeys): button blocking while the shout modifier is held (empty: PlayerShoutInput does it).</summary>
        partial void InstallHooksCore();
        /// <summary>Presentation: the AudioPlay prefix that swaps NPC combat taunts.</summary>
        partial void InstallPatchesPresentation();

        // ------------------------------------------------------------------ lifecycle

        /// <summary>Starts the remaining parts in their old relative order; a failed start is undone by Shutdown.</summary>
        public override void Init()
        {
            try
            {
                Instance = this;
                ModelDir = Path.Combine(Dir, "Models");
                ResetAll();
                InstallHooksGunplay();
                InstallHooksPresentation();     // CreateObject postfix: Pounce's airborne carcass
                InstallHooksCompatibility();
                InstallHooksCore();
                InstallPatchesPresentation();   // AudioPlay.OnEnter -> Senses.BeforeAudioPlay (taunt audio)
                InitPresentationDogs();         // bundle + its own hooks / patch (only when the bundle loads)
                InitCompatibilityDogCompanion();
                InitPresentationWomen();
                InitCompatibilityJobs();
                InitPresentationJobs();         // "NPC.Rigs" + the LateUpdate visual ticks
                InitGunplayJobs();
                InitCoreShout();
                InitToolsPoiNavSetup();         // "PoiNav.Setup"
                InitToolsPoiNavEditor();        // "PoiNav.View", "PoiNav.Editor", "PoiNav.EditorCamera", "PoiNav.EditorGui", editor key
            }
            catch (Exception)
            {
                try { Shutdown(); } catch (Exception) { }
                throw;
            }
        }

        /// <summary>Stops the remaining parts (Tools editor first, then Women of Wasteland, then the emptied parts).</summary>
        public override void Shutdown()
        {
            Step("POI navigation tools", ShutdownToolsStep);
            ShutdownPresentationWomen();
            ShutdownCompatibility();
            ShutdownGunplay();
            ResetAll();
            Instance = null;
        }

        /// <summary>The "POI navigation tools" shutdown step (editor, view, setup).</summary>
        static void ShutdownToolsStep() { ShutdownTools(); }

        /// <summary>Runs one shutdown step; an exception is logged and the next step still runs (also used by the Presentation part).</summary>
        static void Step(string what, Action a)
        {
            try { a(); } catch (Exception e) { Core.Log.Error(Instance, "shutdown " + what, e); }
        }

        /// <summary>Clears the remaining parts' caches (Init, Shutdown, scene load).</summary>
        static void ResetAll() { ResetCompatibility(); ResetGunplay(); ResetTools(); }

        /// <summary>Scene load: caches reset, then the Presentation (Gungirl) caches.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ResetAll(); SceneLoadedPresentation(); }
        /// <summary>World ready: the Presentation (Gungirl) world scan starts.</summary>
        public override void OnWorldReady() { WorldReadyPresentation(); }
        /// <summary>A save starts loading: the Compatibility part is reset.</summary>
        public override void OnLoadBegin(string slot) { LoadBeginCompatibility(); }
        /// <summary>Floating origin: the Tools part's cached positions.</summary>
        public override void OnOriginShift(Vector3 delta) { ShiftTools(delta); }

        /// <summary>Patches target.method with patchType.patchMethod as a prefix or postfix through Core.Patches (used by the Presentation part).</summary>
        internal bool Patch(Type target, string method, Type patchType, string patchMethod, bool prefix)
        {
            var original = AccessTools.Method(target, method);
            var mi = AccessTools.Method(patchType, patchMethod);
            return Core.Patches.Patch(this, original, prefix: prefix ? mi : null, postfix: prefix ? null : mi);
        }

        /// <summary>F7 pages of the remaining parts (NPC population, female appearance, POI pathfinding).</summary>
        public override IEnumerable<ToolPage> ToolPages() { return NpcPages.BuildShell(this); }
    }
}
