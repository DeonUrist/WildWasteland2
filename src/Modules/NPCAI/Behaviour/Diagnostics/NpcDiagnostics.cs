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
        /// <summary>[Obsolete] Old NPCAI log adapter, kept for readers outside NPCAI; forwards to the NPCAI-owned level lines.</summary>
        [Obsolete("refactor: Log.Deep/Warn/Error with the NPCAI owner")]
        internal static readonly LogAdapter Log = new LogAdapter();

        /// <summary>[Obsolete] Level-2 NPCAI line.</summary>
        [Obsolete("refactor: Log.Deep with the NPCAI owner")]
        internal static void Verbose(string s) { NpcaiRuntime.Deep(s); }
        /// <summary>[Obsolete] NPCAI warning (always written, rate-limited by Core).</summary>
        [Obsolete("refactor: Log.Warn with the NPCAI owner")]
        internal static void Warn(string s) { NpcaiRuntime.Warn(s); }

        /// <summary>Plugin.Log of NPCAI: forwards to the NPCAI-owned level lines.</summary>
        internal sealed class LogAdapter
        {
            /// <summary>Level-2 line.</summary>
            public void LogInfo(string s) { NpcaiRuntime.Deep(s); }
            /// <summary>Warning line.</summary>
            public void LogWarning(string s) { NpcaiRuntime.Warn(s); }
            /// <summary>Error line.</summary>
            public void LogError(string s) { NpcaiRuntime.Error(s); }
        }
    }

    /// <summary>Copies private NPC state only when Debug explicitly asks for it.</summary>
    internal sealed class NpcDiagnosticSource : INpcDiagnosticSource
    {
        /// <summary>Collects the existing ghost, idle and navigation primitives into a caller-owned buffer.</summary>
        public void CollectMarkers(System.Collections.Generic.List<DiagnosticMarker> into)
        {
            if (!NpcaiRuntime.Running || !NavigationDiagnosticState.Overlay) return;
            Senses.CollectDebug(into); Idle.CollectDebug(into); Nav.CollectDebug(into);
        }
        /// <summary>Formats current state for explicit status requests.</summary>
        public string Status { get { return "senses " + Senses.Status() + "; brain " + Brain.Status() + "; nav " + Nav.Status() + "; baking " + Nav.BakingText + "; nests " + Nests.Count + "; webs " + Webs.Count; } }
        /// <summary>Requests copies of currently baked navigation grids.</summary>
        public void CaptureMaps() { if (NpcaiRuntime.Running) Nav.CaptureDebugMaps(); }
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
