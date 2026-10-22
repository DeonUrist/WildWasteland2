using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Nav part owned by Debug: NavTrace / NavDump BMP pictures, trace cell texts, the waypoint overlay (DrawDebug) and the status line.</summary>
    internal static partial class Nav
    {
        internal static string TraceInfo()
        {
            var s = _tS;
            if (s == null) return "no baked map here";
            var sb = new System.Text.StringBuilder();
            sb.Append(s.Name.Replace("(Clone)", ""));
            if (_tFrom < 0) return sb.Append(" no cell").ToString();
            int comp = s.Comp != null ? s.Comp[_tFrom] : -1;
            sb.Append(" cell ").Append(_tFrom % s.W).Append(',').Append(_tFrom / s.W).Append(" dy ").Append(_tDy.ToString("0.0"));
            if (comp >= 0) sb.Append(" area#").Append(comp).Append(" (").Append(s.CompSize[comp]).Append(CompReachesRing(s, comp) ? ", reaches ring)" : ", NO ring)");
            sb.Append(_tGoal >= 0 ? " goal cell " + (_tGoal % s.W) + "," + (_tGoal / s.W) + (s.Comp != null && s.Comp[_tGoal] == comp ? " same area" : " area#" + (s.Comp != null ? s.Comp[_tGoal] : -1)) : " goal outside");
            if (_tExit >= 0) sb.Append(" EXIT ").Append(_tExit % s.W).Append(',').Append(_tExit / s.W).Append(_tRelaxed ? " relaxed" : "");
            if (!float.IsNaN(_tD0)) sb.Append(" path ").Append(_tD0.ToString("0.0")).Append(" m");
            if (_tPick >= 0) sb.Append(" wp ").Append(_tPick % s.W).Append(',').Append(_tPick / s.W);
            return sb.ToString();
        }

        // the cell of a world point on the map the NPC was traced on ("x,z" or "-")
        internal static string CellText(Vector3 p)
        {
            var s = _tS; int x, z;
            if (s == null || s.Root == null || !CellOf(s, p, out x, out z)) return "-";
            int i = z * s.W + x;
            return x + "," + z + (float.IsNaN(s.FloorY[i]) ? (s.Why != null ? " (" + WhyName(s.Why[i]) + ")" : " (no floor)") : "");
        }
        private static string WhyName(byte w) { return w == 2 ? "too tight" : w == 3 ? "in rock" : w == 4 ? "why4" : "no floor"; }

        // A trace picture: the map, the NPC's area (from where the trace started) tinted, the route the map plans from the start cell to the
        // goal (cyan cells; to the exit when the goal is not reachable), and the NPC's real trail (white = on the map, orange = steering
        // without the map, red = backing up / resting). File <structure>_<x>_<z>_trace_<npc>.bmp
        internal static void TraceDump(string who, List<Vector3> trail, List<byte> kinds, Vector3 goal)
        {
            if (trail == null || trail.Count < 2) return;
            Structure s = null; int from = -1;
            foreach (var p in trail)
            {
                int c; s = BakedAt(p, out c);
                if (s != null) { from = c; break; }
            }
            if (s == null) { NpcModule.Log.LogInfo("Nav: trace of " + who + ": the trail is on no baked map"); return; }
            int gx, gz; bool inside = CellOf(s, goal, out gx, out gz);
            int gc = inside ? NearestWalkable(s, gx, gz, 4) : -1;
            if (inside && gc < 0) inside = false;
            Field f = Build(s, goal, inside, gc);
            Field mine = Build(s, goal, true, from);
            var route = new List<int>();
            int cur = from;
            if (!float.IsInfinity(f.Dist[from]))
            {
                route.Add(cur);
                for (int k = 0; k < 4000; k++) { int nb = Downhill(s, f, cur); if (nb < 0) break; cur = nb; route.Add(cur); }
            }
            var tr = new List<int>(); var tk = new List<byte>();
            for (int i = 0; i < trail.Count; i++) { int x, z; if (CellOf(s, trail[i], out x, out z)) { tr.Add(z * s.W + x); tk.Add(kinds[i]); } }
            Dump(s, "trace_" + who.Replace("(Clone)", ""), from, inside ? gc : -1, route.Count > 0 ? route[route.Count - 1] : -1, mine.Dist, route, tr, tk);
            NpcModule.Log.LogInfo("Nav: trace of " + who + " on " + s.Name + ": start cell " + (from % s.W) + "," + (from / s.W) + ", goal " + (inside ? "cell " + (gc % s.W) + "," + (gc / s.W) : "outside")
                + (route.Count > 0 ? ", map route " + route.Count + " cells" : ", NO map route from the start cell") + ", trail " + trail.Count + " points");
            Return(f); Return(mine);
        }

        // ---------- [Debug] NavDump: the map as a picture ----------
        // (also: orange = a wall or spike between this cell and a walkable neighbour; purple = a small island, never used for routes)
        // BepInEx/config/Apocaraider/NavDump/<structure>_<x>_<z>_<tag>.bmp, 2 px per cell, north up. Walkable: grey by height (open sky
        // greenish, under a roof bluish); a walkable cell next to a walkable one more than MaxStep higher or lower: yellow; no floor: black;
        // too tight for a body: red; inside rock: brown. On a "no route" dump: the NPC's reachable area is tinted, the NPC white, the goal
        // magenta, the chosen exit cyan.
        private static void Dump(Structure s, string tag, int npc, int goalCell, int exit, float[] area) { Dump(s, tag, npc, goalCell, exit, area, null, null, null); }
        private static void Dump(Structure s, string tag, int npc, int goalCell, int exit, float[] area, List<int> route, List<int> trail, List<byte> trailKind)
        {
            try
            {
                int W = s.W, H = s.H, sc = 2, pw = W * sc, ph = H * sc, row = (pw * 3 + 3) & ~3;
                var px = new byte[row * ph];
                float step = Mathf.Max(0.1f, NpcModule.NavMaxStep.Value);
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
                            float t = Mathf.Clamp01((s.FloorY[i] - s.RefY + 3f) / 6f);
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
                            if (WallNext(s, i)) { r = 255; g = 130; b = 0; }
                            if (!BigArea(s, i)) { r = 150; g = 60; b = 170; }
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
                    for (int t = 0; t < trail.Count; t++)
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
                string dir = System.IO.Path.Combine(NpcModule.DataDir ?? Core.Assets.ModuleConfigDir("NPC"), "NavDump");
                System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir, s.Name.Replace("(Clone)", "") + "_" + Mathf.RoundToInt(s.Root.position.x) + "_" + Mathf.RoundToInt(s.Root.position.z) + "_" + tag + ".bmp");
                using (var fs = new System.IO.FileStream(file, System.IO.FileMode.Create))
                using (var w = new System.IO.BinaryWriter(fs))
                {
                    w.Write((byte)'B'); w.Write((byte)'M'); w.Write(54 + px.Length); w.Write(0); w.Write(54);
                    w.Write(40); w.Write(pw); w.Write(ph); w.Write((short)1); w.Write((short)24); w.Write(0); w.Write(px.Length); w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
                    w.Write(px);
                }
                NpcModule.Log.LogInfo("Nav: map picture " + file + " (cell " + s.Cell.ToString("0.00") + " m, grid origin " + s.GridOrigin.x.ToString("0.0") + "," + s.GridOrigin.z.ToString("0.0") + ", base y " + s.RefY.ToString("0.0") + ")");
            }
            catch (Exception e) { NpcModule.Log.LogError("Nav: dump failed: " + e.Message); }
        }

        private static void Paint(byte[] px, int row, int sc, int x, int z, byte r, byte g, byte b)
        {
            for (int dy = 0; dy < sc; dy++) for (int dx = 0; dx < sc; dx++) { int o = (z * sc + dy) * row + (x * sc + dx) * 3; if (o >= 0 && o + 2 < px.Length) { px[o] = b; px[o + 1] = g; px[o + 2] = r; } }
        }

        internal static void DrawDebug()
        {
            if (!On || !NpcModule.ShowNav.Value) return;
            var cyan = new Color(0.3f, 0.9f, 1f);
            var p = Player();
            foreach (var s in _structures)
            {
                if (s.Root == null) continue;
                if (p != null && s.Box.SqrDistance(p.position) > 150f * 150f) continue;
                DebugOverlay.Label(new Vector3(s.Box.center.x, s.RefY + 3f, s.Box.center.z), s.Name + (s.Baked ? (s.FloorY != null ? " baked, " + s.Walkable + "/" + (s.W * s.H) + " cells" : " (bake failed)") : _baking == s ? " baking " + (100 * s.Next / Math.Max(1, s.W * s.H)) + " %" : " not baked"), cyan);
            }
            float now = Time.time;
            var dead = _dead; dead.Clear();
            foreach (var kv in _debug)
            {
                if (kv.Key == null || now - kv.Value.At > 1.5f) { dead.Add(kv.Key); continue; }
                DebugOverlay.Mark(kv.Value.Next + Vector3.up * 0.3f, cyan, 8f);
                DebugOverlay.Label(kv.Value.Next + Vector3.up * 0.7f, "nav " + kv.Value.Where, cyan);
            }
            foreach (var k in dead) _debug.Remove(k);
        }

        internal static string Status()
        {
            int b = 0; foreach (var s in _structures) if (s.Baked) b++;
            return _structures.Count + " structures, " + b + " baked";
        }
    }
}
