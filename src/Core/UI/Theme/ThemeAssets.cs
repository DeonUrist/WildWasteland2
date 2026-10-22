using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace WildWasteland.Core.UI
{
    /// <summary>Theme textures of the CleanHUD look used by Core's F3 window and notices too (Wave A split, B01 -> Core/UI theme): embedded PNG cache, white/gradient/compass/caret textures, preload and dispose.</summary>
    internal static class ThemeAssets
    {
        /// <summary>Shared title/pause/settings background from the Core theme payload.</summary>
        internal static Texture2D MenuBackground() { return Textures.LoadResource(null, "Core.Theme.BG_MAIN.png", TextureSettings.Ui, "Core.BG_MAIN"); }
        private static readonly Dictionary<string, Texture2D> Embedded = new Dictionary<string, Texture2D>();

        private static readonly HashSet<string> Missing = new HashSet<string>();
        private static Texture2D _white;

        /// <summary>Loads and caches an owned theme texture from the external resource index.</summary>
        internal static Texture2D Tex(string name)
        {
            Texture2D t;
            if (Embedded.TryGetValue(name, out t) && t != null) return t;
            if (Missing.Contains(name)) return null;
            var asm = Assembly.GetExecutingAssembly();
            using (var s = WildWasteland.Core.DiskResources.Open("Core.Theme." + name + ".png"))
            {
                if (s == null) { Missing.Add(name); return null; }
                var buf = new byte[s.Length];
                int read = 0; while (read < buf.Length) { int r = s.Read(buf, read, buf.Length - read); if (r <= 0) break; read += r; }
                t = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "Core.Theme." + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
                t.LoadImage(buf, false);
                t.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            Embedded[name] = t;
            if (name.StartsWith("panel_", StringComparison.Ordinal)) AccentTheme.RegisterPanel(t);
            return t;
        }

        internal static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "CleanHUD.white", hideFlags = HideFlags.DontUnloadUnusedAsset };
                    var px = new Color32[16]; for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                    _white.SetPixels32(px); _white.Apply(false, true);
                }
                return _white;
            }
        }

        // Vertical gradient, white at the top to `bottom` grey (bar fill = colour * gradient, as linear-gradient(c, shade(c)) in the mockup).
        /// <summary>Creates a cached vertical alpha gradient for theme shading.</summary>
        internal static Texture2D VGradient(float bottom)
        {
            var key = "vgrad" + bottom.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            Texture2D t; if (Embedded.TryGetValue(key, out t) && t != null) return t;
            t = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            for (int y = 0; y < 32; y++) { float v = Mathf.Lerp(bottom, 1f, y / 31f); t.SetPixel(0, y, new Color(v, v, v, 1)); }
            t.Apply(false, true); Embedded[key] = t; return t;
        }

        // Compass line: horizontal fade (0..12% / 88..100%) x vertical colour ramp.
        /// <summary>Builds the compass tick-line texture used by theme presentation.</summary>
        internal static Texture2D CompassLine()
        {
            Texture2D t; if (Embedded.TryGetValue("cmpline", out t) && t != null) return t;
            const int W = 512, H = 4;
            t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var top = new Color(1f, 1f, 1f, .9f); var bot = new Color(205 / 255f, 196 / 255f, 170 / 255f, .85f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = Color.Lerp(bot, top, y / (float)(H - 1));
                    c.a *= Fade((x + .5f) / W);
                    t.SetPixel(x, y, c);
                }
            t.Apply(false, true); Embedded["cmpline"] = t; return t;
        }
        // Soft shadow under the line (box-shadow 0 2px 4px rgba(0,0,0,.6)).
        /// <summary>Builds the compass background shadow texture.</summary>
        internal static Texture2D CompassShadow()
        {
            Texture2D t; if (Embedded.TryGetValue("cmpshadow", out t) && t != null) return t;
            const int W = 512, H = 16;
            t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            for (int y = 0; y < H; y++)
            {
                float d = Mathf.Abs((H - 1 - y) - 7.5f) / 4f; // rows from the top; centre 2px below the 3px line
                float a = .6f * Mathf.Exp(-d * d);
                for (int x = 0; x < W; x++) t.SetPixel(x, y, new Color(0, 0, 0, a * Fade((x + .5f) / W)));
            }
            t.Apply(false, true); Embedded["cmpshadow"] = t; return t;
        }
        /// <summary>Applies the existing smooth fade curve to a normalized position.</summary>
        internal static float Fade(float t)
        {
            if (t < .12f) return Mathf.Clamp01(t / .12f);
            if (t > .88f) return Mathf.Clamp01((1f - t) / .12f);
            return 1f;
        }
        /// <summary>Creates the shared caret indicator texture.</summary>
        internal static Texture2D Caret()
        {
            Texture2D t; if (Embedded.TryGetValue("caret", out t) && t != null) return t;
            const int W = 28, H = 18;
            t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float fy = (H - 1 - y + .5f) / H; // 0 top .. 1 bottom
                    float half = (1f - fy) * .5f;
                    float a = Mathf.Clamp01((half - Mathf.Abs((x + .5f) / W - .5f)) * W);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply(false, true); Embedded["caret"] = t; return t;
        }

        /// <summary>Destroys cached owned textures and clears generated texture handles.</summary>
        internal static void Dispose()
        {
            foreach (var t in Embedded.Values) if (t != null) UnityEngine.Object.Destroy(t);
            Embedded.Clear(); Missing.Clear();
            if (_white != null) UnityEngine.Object.Destroy(_white); _white = null;
        }

        /// <summary>Loads the standard menu theme textures before their first presentation.</summary>
        internal static void Preload()
        {
            const string prefix = "Core.Theme.";
            foreach (string name in WildWasteland.Core.DiskResources.Names)
                if (name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".png", StringComparison.Ordinal))
                    Tex(name.Substring(prefix.Length, name.Length - prefix.Length - 4));
            var white = White; VGradient(.75f); VGradient(.72f); CompassLine(); CompassShadow(); Caret();
            GlyphFont.Get();
        }
    }
}
