using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrols debug overlay settings (AiOverlay, traffic DebugMap); Wave A split of PatrolsSettings.cs.</summary>
    public sealed partial class PatrolsModule
    {
        internal static ConfigEntry<bool> AiOverlay;
        internal static ConfigEntry<bool> TrafficDebugMap;

        /// <summary>Binds the tools.cfg Debug entry AiOverlay (on-screen AI car lines).</summary>
        partial void BindDebugOverlay(ModuleConfig cfg)
        {
            AiOverlay = cfg.Tool(TDebug, "AiOverlay", false, "On-screen line per AI car: state, speed, target angle, steering, feeler distances. With no raider car driving: the time until the next spawn roll");
        }

        /// <summary>Binds the Traffic tuning entry DebugMap (routes on the game debug map).</summary>
        partial void BindDebugMap(ModuleConfig cfg)
        {
            // world traffic tuning + debug map
            TrafficDebugMap = cfg.Tool(TTraffic, "DebugMap", true, "Draw traffic routes only on the game's debug map when it is open");
        }
    }
}
