using System;
using UnityEngine;
namespace WildWasteland.Modules.Debug
{
    internal static class NavigationOverlay
    {
        private static Texture2D _white;
        private static GUIStyle _floatStyle, _lineStyle;
        /// <summary>Creates the owned one-pixel marker texture and centered label styles on first draw.</summary>
        private static void EnsureStyles()
        {
            if (_white != null) return;
            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            _white.SetPixel(0, 0, Color.white); _white.Apply();
            _floatStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _lineStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
        }

        // debug helpers (Senses ghosts): a diamond and a label at a world point; nothing beyond Range m (WW perf: a raid's labels cost 1-4 ms/frame)
        internal const float Range = 200f;
        /// <summary>Draws a diamond only when the point projects within the existing 200-metre overlay range.</summary>
        internal static void Mark(Vector3 world, Color c, float size)
        {
            var cam = Core.GameRefs.MainCamera; if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(world); if (sp.z <= 0f || sp.z > Range) return;
            EnsureStyles();
            Vector2 centre = new Vector2(sp.x, Screen.height - sp.y);
            var old = GUI.color; GUI.color = c;
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, centre);
            GUI.DrawTexture(new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), _white);
            GUI.matrix = m;
            GUI.color = old;
        }

        /// <summary>Draws a centered world-point label within the existing overlay range.</summary>
        internal static void Label(Vector3 world, string text, Color c)
        {
            var cam = Core.GameRefs.MainCamera; if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(world); if (sp.z <= 0f || sp.z > Range) return;
            EnsureStyles();
            _floatStyle.fontSize = 12;
            DrawText(new Rect(sp.x - 200f, Screen.height - sp.y - 10f, 400f, 20f), text, _floatStyle, c);
        }


        private static readonly System.Collections.Generic.List<Core.DiagnosticMarker> Markers = new System.Collections.Generic.List<Core.DiagnosticMarker>();
        /// <summary>Renders copied NPC primitives on repaint only; no NPC implementation type crosses the Core API.</summary>
        internal static void Draw()
        {
            if (!Core.NavigationDiagnosticState.Overlay || Event.current.type != EventType.Repaint) return;
            var source = Core.ModApi.Get<Core.INpcDiagnosticSource>(); if (source == null) return;
            Markers.Clear(); source.CollectMarkers(Markers);
            foreach (var marker in Markers)
                if (marker.Text == null) Mark(marker.World, marker.Color, marker.Size); else Label(marker.World, marker.Text, marker.Color);
        }
        /// <summary>Releases the owned marker texture and cached frame buffer.</summary>
        internal static void Release()
        {
            if (_white != null) UnityEngine.Object.Destroy(_white);
            _white = null; _floatStyle = _lineStyle = null; Markers.Clear();
        }
        /// <summary>Draws a one-pixel text shadow and restores the caller's GUI color.</summary>
        private static void DrawText(Rect r, string text, GUIStyle style, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, c.a * 0.8f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, style);    // shadow
            GUI.color = c;
            GUI.Label(r, text, style);
            GUI.color = old;
        }
    
    }
}
