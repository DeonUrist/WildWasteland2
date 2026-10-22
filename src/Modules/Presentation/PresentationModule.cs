using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// Presentation module entry (README 3.1, 6.1): one character pipeline for player, NPCs, female NPCs and animals; traversal, seat poses, weapon poses, camera rig, vehicle skins. Main owner B05 (Character/Player), World part owner B06 (Camera/Traversal/Seats/VehicleSkins).
    /// Split into partial files so two Wave B packages can own lifecycle parts independently: this file (main owner) forwards every
    /// lifecycle call to the "World" partial methods implemented in <c>src/Modules/Presentation/PresentationModule.World.cs</c> (second owner). Order inside each call:
    /// main part first, then World. Keep that order unless the reports say otherwise.
    /// </summary>
    public sealed partial class PresentationModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Presentation"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Presentation"; } }

        /// <summary>Binds every entry of the module (main part, then World).</summary>
        public override void BindConfig(ModuleConfig cfg) { BindWorld(cfg); }

        /// <summary>Starts the module (main part, then World).</summary>
        public override void Init() { InitWorld(); }

        /// <summary>Stops the module (World first, then main part).</summary>
        public override void Shutdown() { ShutdownWorld(); }

        /// <summary>Scene load fan-out.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { SceneLoadedWorld(scene, mode); }

        /// <summary>First isPlay after load/new game.</summary>
        public override void OnWorldReady() { WorldReadyWorld(); }

        /// <summary>Native save started.</summary>
        public override void OnSaveBegin(string slot) { SaveBeginWorld(slot); }

        /// <summary>Native save finished.</summary>
        public override void OnSaveEnd(string slot) { SaveEndWorld(slot); }

        /// <summary>Native load started.</summary>
        public override void OnLoadBegin(string slot) { LoadBeginWorld(slot); }

        /// <summary>Floating-origin shift (delta = old - new offset).</summary>
        public override void OnOriginShift(Vector3 delta) { OriginShiftWorld(delta); }

        /// <summary>Registers ModAPI providers.</summary>
        public override void ProvideApis() { ProvideApisWorld(); }

        /// <summary>Contributes rows to the shared F3 pages.</summary>
        public override void Settings(SettingsShell shell) { SettingsWorld(shell); }

        /// <summary>F7/F3 tool pages of the module.</summary>
        public override IEnumerable<ToolPage> ToolPages() { var pages = new List<ToolPage>(); ToolPagesWorld(pages); return pages; }

        /// <summary>World part: config binds.</summary>
        partial void BindWorld(ModuleConfig cfg);
        /// <summary>World part: start.</summary>
        partial void InitWorld();
        /// <summary>World part: stop.</summary>
        partial void ShutdownWorld();
        /// <summary>World part: scene loaded.</summary>
        partial void SceneLoadedWorld(Scene scene, LoadSceneMode mode);
        /// <summary>World part: world ready.</summary>
        partial void WorldReadyWorld();
        /// <summary>World part: save begin.</summary>
        partial void SaveBeginWorld(string slot);
        /// <summary>World part: save end.</summary>
        partial void SaveEndWorld(string slot);
        /// <summary>World part: load begin.</summary>
        partial void LoadBeginWorld(string slot);
        /// <summary>World part: origin shift.</summary>
        partial void OriginShiftWorld(Vector3 delta);
        /// <summary>World part: ModAPI providers.</summary>
        partial void ProvideApisWorld();
        /// <summary>World part: settings rows.</summary>
        partial void SettingsWorld(SettingsShell shell);
        /// <summary>World part: tool pages.</summary>
        partial void ToolPagesWorld(List<ToolPage> pages);
    }
}
