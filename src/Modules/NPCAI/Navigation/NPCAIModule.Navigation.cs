using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Modules.NPCAI;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Navigation part of <see cref="NPCAIModule"/> (Wave B owner B08): config (NavSettings), lifecycle and jobs (NavRuntime), the INavApi provider.</summary>
    public sealed partial class NPCAIModule
    {
        /// <summary>Navigation: binds [NPCAI / Navigation], [NPCAI / Pathfinding] and [NPCAI / POI pathfinding] (NavSettings).</summary>
        partial void BindNavigation(ModuleConfig cfg) { NavSettings.Bind(cfg); }
        /// <summary>Navigation: start - registers the navigation jobs (NavRuntime.Start).</summary>
        partial void InitNavigation() { NavRuntime.Start(this); }
        /// <summary>Navigation: stop - releases bodies and colliders, clears every navigation registry.</summary>
        partial void ShutdownNavigation() { NavigationAuthoringApi.Reset(); NavRuntime.Stop(); }
        /// <summary>Navigation: scene loaded - every navigation registry starts empty.</summary>
        partial void SceneLoadedNavigation(Scene scene, LoadSceneMode mode) { NavRuntime.ResetAll(); }
        /// <summary>Navigation: world ready.</summary>
        partial void WorldReadyNavigation() { }
        /// <summary>Navigation: save begin.</summary>
        partial void SaveBeginNavigation(string slot) { }
        /// <summary>Navigation: save end.</summary>
        partial void SaveEndNavigation(string slot) { }
        /// <summary>Navigation: load begin.</summary>
        partial void LoadBeginNavigation(string slot) { }
        /// <summary>Navigation: origin shift - grid maps, POI instances, routes and traversals move with the world.</summary>
        partial void OriginShiftNavigation(Vector3 delta) { NavRuntime.Shift(delta); }
        /// <summary>Navigation: provides <see cref="INavApi"/> (Core removes it when the module stops).</summary>
        partial void ProvideApisNavigation() { ModApi.Provide<INavApi>(this, new NavApi(this)); ModApi.Provide<INavigationAuthoring>(this, new NavigationAuthoringApi()); }
        /// <summary>Navigation: settings rows.</summary>
        partial void SettingsNavigation(SettingsShell shell) { }
        /// <summary>Navigation: tool pages.</summary>
        partial void ToolPagesNavigation(List<ToolPage> pages) { }
    }
}
