using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Core.UI
{
    internal static class AccentTheme
    {
        internal static readonly string[] Names = { "Yellow", "Green", "Blue", "Red", "Purple", "White", "Gray", "Mint" };
        internal static readonly int[] DisplayOrder = { 5, 6, 3, 0, 1, 7, 2, 4 };
        internal static readonly Color[] Colors = { new Color32(226, 191, 108, 255), new Color32(96, 218, 96, 255),
            new Color32(120, 181, 240, 255), new Color32(235, 128, 124, 255), new Color32(187, 142, 236, 255),
            new Color32(255, 255, 255, 255), new Color32(160, 160, 160, 255), new Color32(0, 255, 145, 255) };
        internal static ConfigEntry<string> Setting;
        internal static int Index;
        private static readonly HashSet<AccentTint> Graphics = new HashSet<AccentTint>();
        /// <summary>Owned panel texture and bounded recoloring progress with its immutable original pixels.</summary>
        private sealed class Panel { internal Texture2D Texture; internal Color32[] Original, Pixels; internal int Position, Choice; }
        private static readonly List<Panel> Panels = new List<Panel>();

        /// <summary>Resolves the configured accent and subscribes to later changes.</summary>
        internal static void Init()
        {
            Index = Math.Max(0, Array.IndexOf(Names, Setting.Value));
            Setting.SettingChanged += Changed;
        }
        /// <summary>Unsubscribes the accent setting and releases panel recoloring references.</summary>
        internal static void Clear() { Setting.SettingChanged -= Changed; Panels.Clear(); }
        /// <summary>Tracks a live accent mesh effect for setting-change invalidation.</summary>
        internal static void Register(AccentTint tint) { Graphics.Add(tint); }
        /// <summary>Stops tracking a disabled accent mesh effect.</summary>
        internal static void Remove(AccentTint tint) { Graphics.Remove(tint); }
        /// <summary>Creates a UI graphic together with the shared accent-color mesh effect.</summary>
        internal static T AddAccent<T>(this GameObject go) where T : Graphic
        {
            var graphic = go.AddComponent<T>(); go.AddComponent<AccentTint>(); return graphic;
        }
        /// <summary>Selects the new accent, invalidates live graphics and restarts owned panel recoloring.</summary>
        private static void Changed(object sender, EventArgs args)
        {
            Index = Math.Max(0, Array.IndexOf(Names, Setting.Value));
            foreach (var graphic in Graphics) if (graphic != null) graphic.Refresh();
            foreach (var panel in Panels) { panel.Position = 0; panel.Choice = -1; }
        }
        /// <summary>Maps the original gold or border color into the selected accent while preserving alpha.</summary>
        internal static Color Resolve(Color original)
        {
            if (Index == 0) return original;
            Color32 c = original;
            bool gold = Math.Abs(c.r - 226) <= 1 && Math.Abs(c.g - 191) <= 1 && Math.Abs(c.b - 108) <= 1;
            bool border = c.r == 214 && c.g == 203 && c.b == 176;
            if (!gold && !border) return original;
            var color = border ? Color.Lerp(Colors[Index], Color.white, .55f) : Colors[Index]; color.a = original.a; return color;
        }
        /// <summary>Retains original pixels for reversible recoloring of an owned panel texture.</summary>
        internal static void RegisterPanel(Texture2D texture)
        {
            var original = texture.GetPixels32();
            Panels.Add(new Panel { Texture = texture, Original = original, Pixels = new Color32[original.Length], Choice = 0 });
        }
        // Only owned panel textures are recolored, in the existing scheduler's bounded slices.
        /// <summary>Recolors at most 256 pixels of one pending panel in the current scheduler slice.</summary>
        internal static bool Step()
        {
            foreach (var panel in Panels)
            {
                if (panel.Texture == null || panel.Choice == Index) continue;
                int end = Math.Min(panel.Position + 256, panel.Original.Length); var accent = Colors[Index];
                for (int i = panel.Position; i < end; i++)
                {
                    var c = panel.Original[i];
                    if (Index != 0 && c.a != 0 && c.r > c.g * 1.05f && c.g > c.b * 1.1f)
                        c = (Color32)new Color(Mathf.Clamp01(c.r / 226f * accent.r), Mathf.Clamp01(c.r / 226f * accent.g), Mathf.Clamp01(c.r / 226f * accent.b), c.a / 255f);
                    panel.Pixels[i] = c;
                }
                panel.Position = end;
                if (end == panel.Original.Length)
                {
                    panel.Texture.SetPixels32(panel.Pixels); panel.Texture.Apply(true, false); panel.Choice = Index;
                }
                return true;
            }
            return false;
        }
    }

    public sealed class AccentTint : BaseMeshEffect
    {
        /// <summary>Registers the live mesh effect after the native UI enable callback.</summary>
        protected override void OnEnable() { base.OnEnable(); AccentTheme.Register(this); }
        /// <summary>Unregisters the mesh effect before the native UI disable callback.</summary>
        protected override void OnDisable() { AccentTheme.Remove(this); base.OnDisable(); }
        /// <summary>Invalidates graphic vertices after an accent change.</summary>
        internal void Refresh() { if (graphic != null) graphic.SetVerticesDirty(); }
        /// <summary>Recolors existing UI vertices using the shared accent mapping.</summary>
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || AccentTheme.Index == 0) return;
            var vertex = UIVertex.simpleVert;
            for (int i = 0; i < mesh.currentVertCount; i++)
            { mesh.PopulateUIVertex(ref vertex, i); vertex.color = AccentTheme.Resolve(vertex.color); mesh.SetUIVertex(vertex, i); }
        }
    }
}
