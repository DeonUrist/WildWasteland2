using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
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
        private sealed class Panel { internal Texture2D Texture; internal Color32[] Original, Pixels; internal int Position, Choice; }
        private static readonly List<Panel> Panels = new List<Panel>();

        internal static void Init()
        {
            Index = Math.Max(0, Array.IndexOf(Names, Setting.Value));
            Setting.SettingChanged += Changed;
        }
        internal static void Clear() { Setting.SettingChanged -= Changed; Panels.Clear(); }
        internal static void Register(AccentTint tint) { Graphics.Add(tint); }
        internal static void Remove(AccentTint tint) { Graphics.Remove(tint); }
        internal static T AddAccent<T>(this GameObject go) where T : Graphic
        {
            var graphic = go.AddComponent<T>(); go.AddComponent<AccentTint>(); return graphic;
        }
        private static void Changed(object sender, EventArgs args)
        {
            Index = Math.Max(0, Array.IndexOf(Names, Setting.Value));
            foreach (var graphic in Graphics) if (graphic != null) graphic.Refresh();
            foreach (var panel in Panels) { panel.Position = 0; panel.Choice = -1; }
        }
        internal static Color Resolve(Color original)
        {
            if (Index == 0) return original;
            Color32 c = original;
            bool gold = Math.Abs(c.r - 226) <= 1 && Math.Abs(c.g - 191) <= 1 && Math.Abs(c.b - 108) <= 1;
            bool border = c.r == 214 && c.g == 203 && c.b == 176;
            if (!gold && !border) return original;
            var color = border ? Color.Lerp(Colors[Index], Color.white, .55f) : Colors[Index]; color.a = original.a; return color;
        }
        internal static void RegisterPanel(Texture2D texture)
        {
            var original = texture.GetPixels32();
            Panels.Add(new Panel { Texture = texture, Original = original, Pixels = new Color32[original.Length], Choice = 0 });
        }
        // Only owned panel textures are recolored, in the existing scheduler's bounded slices.
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
        protected override void OnEnable() { base.OnEnable(); AccentTheme.Register(this); }
        protected override void OnDisable() { AccentTheme.Remove(this); base.OnDisable(); }
        internal void Refresh() { if (graphic != null) graphic.SetVerticesDirty(); }
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || AccentTheme.Index == 0) return;
            var vertex = UIVertex.simpleVert;
            for (int i = 0; i < mesh.currentVertCount; i++)
            { mesh.PopulateUIVertex(ref vertex, i); vertex.color = AccentTheme.Resolve(vertex.color); mesh.SetUIVertex(vertex, i); }
        }
    }
}
