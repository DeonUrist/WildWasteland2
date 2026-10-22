using System;
using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
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
