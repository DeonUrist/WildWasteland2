using System;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Debug
{
    /// <summary>Rasterizes copied navigation snapshots to the existing two-pixel-per-cell BMP format.</summary>
    internal sealed class NavigationMapExporter : INavigationDiagnosticSink
    {
        /// <summary>Writes one copied map and its optional route/trail overlays under Debug/NavDump.</summary>
        public void Write(NavigationMapSnapshot s)
        {
            string tag = s.Tag; int npc = s.Npc, goalCell = s.Goal, exit = s.Exit;
            var area = s.Area; var route = s.Route; var trail = s.Trail; var trailKind = s.TrailKind;
            try
            {
                int W = s.Width, H = s.Height, sc = 2, pw = W * sc, ph = H * sc, row = (pw * 3 + 3) & ~3;
                var px = new byte[row * ph];
                float step = Mathf.Max(0.1f, s.MaxStep);
                for (int z = 0; z < H; z++)
                    for (int x = 0; x < W; x++)
                    {
                        int i = z * W + x; byte r, g, b;
                        if (float.IsNaN(s.FloorY[i]))
                        {
                            switch (s.Why[i]) { case 2: r = 200; g = 30; b = 30; break; case 3: r = 110; g = 70; b = 30; break; case 4: r = 20; g = 20; b = 110; break; default: r = 0; g = 0; b = 0; break; }
                            if (area != null && !float.IsInfinity(area[i])) { r = (byte)Math.Min(255, r + 60); g = (byte)Math.Min(255, g + 90); b = (byte)Math.Min(255, b + 80); }
                        }
                        else
                        {
                            float t = Mathf.Clamp01((s.FloorY[i] - s.ReferenceY + 3f) / 6f);
                            int v = (int)(90 + 140 * t);
                            if (s.Open[i]) { r = (byte)(v * 0.85f); g = (byte)v; b = (byte)(v * 0.8f); } else { r = (byte)(v * 0.75f); g = (byte)(v * 0.8f); b = (byte)v; }
                            bool cliff = false;
                            for (int k = 0; k < 4 && !cliff; k++)
                            {
                                int cx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), cz = z + (k == 2 ? 1 : k == 3 ? -1 : 0);
                                if (cx < 0 || cz < 0 || cx >= W || cz >= H) continue;
                                float o = s.FloorY[cz * W + cx];
                                if (!float.IsNaN(o) && Mathf.Abs(o - s.FloorY[i]) > step) cliff = true;
                            }
                            if (cliff) { r = 240; g = 220; b = 40; }
                            if (s.Wall[i]) { r = 255; g = 130; b = 0; }
                            if (!s.BigArea[i]) { r = 150; g = 60; b = 170; }
                            if (area != null && !float.IsInfinity(area[i])) { r = (byte)(r * 0.6f); g = (byte)(g * 0.6f + 80); b = (byte)(b * 0.6f + 60); }
                        }
                        if (i == exit) { r = 0; g = 255; b = 255; }
                        if (i == goalCell) { r = 255; g = 0; b = 255; }
                        if (i == npc) { r = 255; g = 255; b = 255; }
                        for (int dy = 0; dy < sc; dy++)
                            for (int dx = 0; dx < sc; dx++)
                            {
                                int o = (z * sc + dy) * row + (x * sc + dx) * 3;
                                px[o] = b; px[o + 1] = g; px[o + 2] = r;
                            }
                    }
                if (route != null) foreach (int i in route) Paint(px, row, sc, i % W, i / W, 0, 200, 255);
                if (trail != null)
                    for (int t = 0; t < trail.Length; t++)
                    {
                        byte k = trailKind[t];
                        if (k == 0) Paint(px, row, sc, trail[t] % W, trail[t] / W, 255, 255, 255);
                        else if (k == 1) Paint(px, row, sc, trail[t] % W, trail[t] / W, 255, 140, 0);
                        else Paint(px, row, sc, trail[t] % W, trail[t] / W, 255, 0, 0);
                    }
                // marks a little bigger so they show
                foreach (var m in new[] { npc, goalCell, exit })
                {
                    if (m < 0) continue;
                    int mx = m % W, mz = m / W;
                    byte r = m == npc ? (byte)255 : m == goalCell ? (byte)255 : (byte)0, g = m == npc ? (byte)255 : m == goalCell ? (byte)0 : (byte)255, b = 255;
                    for (int dz = -2; dz <= 2; dz++) for (int dx = -2; dx <= 2; dx++)
                    {
                        int cx = mx + dx, cz = mz + dz; if (cx < 0 || cz < 0 || cx >= W || cz >= H) continue;
                        for (int yy = 0; yy < sc; yy++) for (int xx = 0; xx < sc; xx++) { int o = (cz * sc + yy) * row + (cx * sc + xx) * 3; px[o] = b; px[o + 1] = g; px[o + 2] = r; }
                    }
                }
                string dir = System.IO.Path.Combine(Core.Assets.ModuleConfigDir("Debug"), "NavDump");
                System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir, s.Name.Replace("(Clone)", "") + "_" + Mathf.RoundToInt(s.RootScene.x) + "_" + Mathf.RoundToInt(s.RootScene.z) + "_" + tag + ".bmp");
                using (var fs = new System.IO.FileStream(file, System.IO.FileMode.Create))
                using (var w = new System.IO.BinaryWriter(fs))
                {
                    w.Write((byte)'B'); w.Write((byte)'M'); w.Write(54 + px.Length); w.Write(0); w.Write(54);
                    w.Write(40); w.Write(pw); w.Write(ph); w.Write((short)1); w.Write((short)24); w.Write(0); w.Write(px.Length); w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
                    w.Write(px);
                }
                Core.Log.Main(DebugModule.Instance, "Nav: map picture " + file + " (cell " + s.Cell.ToString("0.00") + " m, grid origin " + s.GridOrigin.x.ToString("0.0") + "," + s.GridOrigin.z.ToString("0.0") + ", base y " + s.ReferenceY.ToString("0.0") + ")");
            }
            catch (Exception e) { Core.Log.Error(DebugModule.Instance, "Nav: dump failed: " + e.Message); }
        }

        /// <summary>Sets one cell (sc x sc pixels) of the BMP pixel buffer.</summary>
        private static void Paint(byte[] px, int row, int sc, int x, int z, byte r, byte g, byte b)
        {
            for (int dy = 0; dy < sc; dy++) for (int dx = 0; dx < sc; dx++) { int o = (z * sc + dy) * row + (x * sc + dx) * 3; if (o >= 0 && o + 2 < px.Length) { px[o] = b; px[o + 1] = g; px[o + 2] = r; } }
        }

    }
}
