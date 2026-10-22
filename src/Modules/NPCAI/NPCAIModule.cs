using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>
    /// NPCAI module entry (README 3.1, 6.2): NPC behaviour, perception, decisions, animals, population, followers. Main owner B07 (Behaviour/Population), Navigation part owner B08 (grid nav, PoiNav, town routing).
    /// Split into partial files so two Wave B packages can own lifecycle parts independently: this file (main owner) forwards every
    /// lifecycle call to the "Navigation" partial methods implemented in <c>src/Modules/NPCAI/Navigation/NPCAIModule.Navigation.cs</c> (second owner). Order inside each call:
    /// main part first, then Navigation. Keep that order unless the reports say otherwise.
    /// </summary>
    public sealed partial class NPCAIModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "NPCAI"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "NPC AI"; } }

        /// <summary>Binds every entry of the module (main part, then Navigation).</summary>
        public override void BindConfig(ModuleConfig cfg) { BindNavigation(cfg); }

        /// <summary>Starts the module (main part, then Navigation).</summary>
        public override void Init() { InitNavigation(); }

        /// <summary>Stops the module (Navigation first, then main part).</summary>
        public override void Shutdown() { ShutdownNavigation(); }

        /// <summary>Scene load fan-out.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { SceneLoadedNavigation(scene, mode); }

        /// <summary>First isPlay after load/new game.</summary>
        public override void OnWorldReady() { WorldReadyNavigation(); }

        /// <summary>Native save started.</summary>
        public override void OnSaveBegin(string slot) { SaveBeginNavigation(slot); }

        /// <summary>Native save finished.</summary>
        public override void OnSaveEnd(string slot) { SaveEndNavigation(slot); }

        /// <summary>Native load started.</summary>
        public override void OnLoadBegin(string slot) { LoadBeginNavigation(slot); }

        /// <summary>Floating-origin shift (delta = old - new offset).</summary>
        public override void OnOriginShift(Vector3 delta) { OriginShiftNavigation(delta); }

        /// <summary>Registers ModAPI providers.</summary>
        public override void ProvideApis() { ProvideApisNavigation(); }

        /// <summary>Contributes rows to the shared F3 pages.</summary>
        public override void Settings(SettingsShell shell) { SettingsNavigation(shell); }

        /// <summary>F7/F3 tool pages of the module.</summary>
        public override IEnumerable<ToolPage> ToolPages() { var pages = new List<ToolPage>(); ToolPagesNavigation(pages); return pages; }

        /// <summary>Navigation part: config binds.</summary>
        partial void BindNavigation(ModuleConfig cfg);
        /// <summary>Navigation part: start.</summary>
        partial void InitNavigation();
        /// <summary>Navigation part: stop.</summary>
        partial void ShutdownNavigation();
        /// <summary>Navigation part: scene loaded.</summary>
        partial void SceneLoadedNavigation(Scene scene, LoadSceneMode mode);
        /// <summary>Navigation part: world ready.</summary>
        partial void WorldReadyNavigation();
        /// <summary>Navigation part: save begin.</summary>
        partial void SaveBeginNavigation(string slot);
        /// <summary>Navigation part: save end.</summary>
        partial void SaveEndNavigation(string slot);
        /// <summary>Navigation part: load begin.</summary>
        partial void LoadBeginNavigation(string slot);
        /// <summary>Navigation part: origin shift.</summary>
        partial void OriginShiftNavigation(Vector3 delta);
        /// <summary>Navigation part: ModAPI providers.</summary>
        partial void ProvideApisNavigation();
        /// <summary>Navigation part: settings rows.</summary>
        partial void SettingsNavigation(SettingsShell shell);
        /// <summary>Navigation part: tool pages.</summary>
        partial void ToolPagesNavigation(List<ToolPage> pages);
    }
}
