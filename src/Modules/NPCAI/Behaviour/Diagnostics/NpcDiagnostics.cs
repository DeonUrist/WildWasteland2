using System;
using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// NpcModule part owned by Debug (Wave A split): the NPCAI log adapter and helpers, the ShowNavigation overlay job and the debug /
    /// log switches of tools.cfg. Partial methods are called from the NPC entry where the moved lines were.
    /// </summary>
    public sealed partial class NpcModule
    {
        internal static readonly LogAdapter Log = new LogAdapter();

        /// <summary>Info line: always written when [NPC / Debug] VerboseLog is on, else through the global verbose switch.</summary>
        internal static void Verbose(string s) { if (Core.Log.L2) Core.Log.Deep(null, "[NPCAI] " + s);  }
        /// <summary>Warning line (always written, rate-limited by Core).</summary>
        internal static void Warn(string s) { Core.Log.Warn("[NPC] " + s); }

        /// <summary>Plugin.Log of NPCAI: info lines are written when the module's own VerboseLog or the global one is on.</summary>
        internal sealed class LogAdapter
        {
            /// <summary>Info line (module VerboseLog: always; else only with the global verbose switch).</summary>
            public void LogInfo(string s) { if (Core.Log.L2) Core.Log.Deep(null, "[NPCAI] " + s);  }
            /// <summary>Warning line.</summary>
            public void LogWarning(string s) { Core.Log.Warn("[NPC] " + s); }
            /// <summary>Error line.</summary>
            public void LogError(string s) { Core.Log.Error("[NPC] " + s); }
        }

        /// <summary>Provides copied NPC diagnostics to Debug without exposing private runtime types.</summary>
        public override void ProvideApis() { ModApi.Provide<INpcDiagnosticSource>(this, new NpcDiagnosticSource()); }
    }

    /// <summary>Copies private NPC state only when Debug explicitly asks for it.</summary>
    internal sealed class NpcDiagnosticSource : INpcDiagnosticSource
    {
        /// <summary>Collects the existing ghost, idle and navigation primitives into a caller-owned buffer.</summary>
        public void CollectMarkers(System.Collections.Generic.List<DiagnosticMarker> into)
        {
            if (!NpcModule.Running || !NavigationDiagnosticState.Overlay) return;
            Senses.CollectDebug(into); Idle.CollectDebug(into); Nav.CollectDebug(into);
        }
        /// <summary>Formats current state for explicit status requests.</summary>
        public string Status { get { return "senses " + Senses.Status() + "; brain " + Brain.Status() + "; nav " + Nav.Status() + "; baking " + Nav.BakingText + "; nests " + Nests.Count + "; webs " + Webs.Count; } }
        /// <summary>Requests copies of currently baked navigation grids.</summary>
        public void CaptureMaps() { if (NpcModule.Running) Nav.CaptureDebugMaps(); }
    }

    /// <summary>Creates copied overlay values while private selectors retain their existing range and label rules.</summary>
    internal static class DiagnosticMarkers
    {
        /// <summary>Appends a diamond marker.</summary>
        internal static void Mark(System.Collections.Generic.List<DiagnosticMarker> into, UnityEngine.Vector3 world, UnityEngine.Color color, float size) { into.Add(new DiagnosticMarker(world, color, size, null)); }
        /// <summary>Appends a world-space text label.</summary>
        internal static void Label(System.Collections.Generic.List<DiagnosticMarker> into, UnityEngine.Vector3 world, string text, UnityEngine.Color color) { into.Add(new DiagnosticMarker(world, color, 0, text)); }
    }
}
