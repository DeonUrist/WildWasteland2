using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Modules.NPC;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Navigation part of <see cref="NPCAIModule"/> (second Wave B owner). Empty implementations so check_partials passes; fill them in.</summary>
    public sealed partial class NPCAIModule
    {
        /// <summary>Navigation: binds [NPCAI / Navigation], [NPCAI / Pathfinding] and [NPCAI / POI pathfinding] (NavSettings).</summary>
        partial void BindNavigation(ModuleConfig cfg) { NavSettings.Bind(cfg); }
        /// <summary>Navigation: start.</summary>
        partial void InitNavigation() { }
        /// <summary>Navigation: stop.</summary>
        partial void ShutdownNavigation() { }
        /// <summary>Navigation: scene loaded.</summary>
        partial void SceneLoadedNavigation(Scene scene, LoadSceneMode mode) { }
        /// <summary>Navigation: world ready.</summary>
        partial void WorldReadyNavigation() { }
        /// <summary>Navigation: save begin.</summary>
        partial void SaveBeginNavigation(string slot) { }
        /// <summary>Navigation: save end.</summary>
        partial void SaveEndNavigation(string slot) { }
        /// <summary>Navigation: load begin.</summary>
        partial void LoadBeginNavigation(string slot) { }
        /// <summary>Navigation: origin shift.</summary>
        partial void OriginShiftNavigation(Vector3 delta) { }
        /// <summary>Navigation: ModAPI providers.</summary>
        partial void ProvideApisNavigation() { }
        /// <summary>Navigation: settings rows.</summary>
        partial void SettingsNavigation(SettingsShell shell) { }
        /// <summary>Navigation: tool pages.</summary>
        partial void ToolPagesNavigation(List<ToolPage> pages) { }
    }
}
