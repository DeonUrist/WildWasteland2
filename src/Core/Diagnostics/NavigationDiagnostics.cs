using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>One copied world-space overlay primitive. Null text selects a diamond marker; text selects a centered label.</summary>
    public struct DiagnosticMarker
    {
        public readonly Vector3 World;
        public readonly Color Color;
        public readonly float Size;
        public readonly string Text;
        /// <summary>Copies a point or label without retaining NPCs, components or scene transforms.</summary>
        public DiagnosticMarker(Vector3 world, Color color, float size, string text) { World = world; Color = color; Size = size; Text = text; }
    }

    /// <summary>Debug-owned live flags read by private navigation selectors. They never own gameplay decisions or saved state.</summary>
    public static class NavigationDiagnosticState
    {
        public const float Range = 200f;
        public static bool Overlay, Capture;
        /// <summary>Releases all diagnostic work when Debug shuts down.</summary>
        public static void Reset() { Overlay = Capture = false; }
    }

    /// <summary>NPCAI's copied diagnostic view; Debug never reads private navigation or actor implementation types.</summary>
    public interface INpcDiagnosticSource
    {
        /// <summary>Appends copied world-space overlay primitives, retaining the existing culling range and geometry.</summary>
        void CollectMarkers(List<DiagnosticMarker> into);
        /// <summary>Current detection, navigation, movement and population status.</summary>
        string Status { get; }
        /// <summary>Captures every currently baked navigation grid through the registered diagnostic sink.</summary>
        void CaptureMaps();
    }

    /// <summary>Copied navigation grid and optional trace overlays. Arrays are owned by this snapshot and treated as immutable
    /// after publication; no scene references or navigation structures cross the Core boundary.</summary>
    public sealed class NavigationMapSnapshot
    {
        public string Name, Tag;
        public int Width, Height, Npc, Goal, Exit;
        public float ReferenceY, Cell, MaxStep;
        public Vector3 RootWorld, RootScene;
        public Vector3 GridOrigin;
        public float[] FloorY, Area;
        public byte[] Why, TrailKind;
        public bool[] Open, Wall, BigArea;
        public int[] Route, Trail;
    }

    /// <summary>Debug-owned map/BMP output. NPCAI publishes copied values only while a diagnostic capture is requested.</summary>
    public interface INavigationDiagnosticSink
    {
        /// <summary>Writes one copied navigation map using the existing BMP raster/overlay conventions.</summary>
        void Write(NavigationMapSnapshot snapshot);
    }
}
