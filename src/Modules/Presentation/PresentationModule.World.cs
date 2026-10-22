using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>World part of <see cref="PresentationModule"/> (second Wave B owner). Empty implementations so check_partials passes; fill them in.</summary>
    public sealed partial class PresentationModule
    {
        /// <summary>World: config binds.</summary>
        partial void BindWorld(ModuleConfig cfg) { }
        /// <summary>World: start.</summary>
        partial void InitWorld() { }
        /// <summary>World: stop.</summary>
        partial void ShutdownWorld() { }
        /// <summary>World: scene loaded.</summary>
        partial void SceneLoadedWorld(Scene scene, LoadSceneMode mode) { }
        /// <summary>World: world ready.</summary>
        partial void WorldReadyWorld() { }
        /// <summary>World: save begin.</summary>
        partial void SaveBeginWorld(string slot) { }
        /// <summary>World: save end.</summary>
        partial void SaveEndWorld(string slot) { }
        /// <summary>World: load begin.</summary>
        partial void LoadBeginWorld(string slot) { }
        /// <summary>World: origin shift.</summary>
        partial void OriginShiftWorld(Vector3 delta) { }
        /// <summary>World: ModAPI providers.</summary>
        partial void ProvideApisWorld() { }
        /// <summary>World: settings rows.</summary>
        partial void SettingsWorld(SettingsShell shell) { }
        /// <summary>World: tool pages.</summary>
        partial void ToolPagesWorld(List<ToolPage> pages) { }
    }
}
