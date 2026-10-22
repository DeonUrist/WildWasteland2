using WildWasteland.Core;
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
        /// <summary>Trace text of the last Next call (structure, cells, exit, pick) for the brain's per-second trace line.</summary>
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

        // A trace picture: the map, the NPC's area (from where the trace started) tinted, the route the map plans from the start cell to the
        // goal (cyan cells; to the exit when the goal is not reachable), and the NPC's real trail (white = on the map, orange = steering
        // without the map, red = backing up / resting). File <structure>_<x>_<z>_trace_<npc>.bmp
        /// <summary>Writes a trace picture: the map, the NPC's area, the planned route and the walked trail (BMP).</summary>
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
        // BepInEx/config/WildWasteland/NPC/NavDump/<structure>_<x>_<z>_<tag>.bmp, 2 px per cell, north up. Walkable: grey by height (open sky
        // greenish, under a roof bluish); a walkable cell next to a walkable one more than MaxStep higher or lower: yellow; no floor: black;
        // too tight for a body: red; inside rock: brown. On a "no route" dump: the NPC's reachable area is tinted, the NPC white, the goal
        // magenta, the chosen exit cyan.
        /// <summary>Writes the map picture without route / trail.</summary>
        private static void Dump(Structure s, string tag, int npc, int goalCell, int exit, float[] area) { Dump(s, tag, npc, goalCell, exit, area, null, null, null); }
        /// <summary>Writes the map as a BMP (2 px per cell), optionally with the NPC's area, goal, exit, route and trail.</summary>
        private static void Dump(Structure s, string tag, int npc, int goalCell, int exit, float[] area, List<int> route, List<int> trail, List<byte> trailKind)
        {
            var sink = ModApi.Get<INavigationDiagnosticSink>(); if (sink == null || s.Root == null || s.FloorY == null) return;
            int count = s.W * s.H;
            var wall = new bool[count]; var big = new bool[count];
            for (int i = 0; i < count; i++) { wall[i] = WallNext(s, i); big[i] = BigArea(s, i); }
            sink.Write(new NavigationMapSnapshot { Name = s.Name, Tag = tag, Width = s.W, Height = s.H,
                Npc = npc, Goal = goalCell, Exit = exit, ReferenceY = s.RefY, Cell = s.Cell, MaxStep = NpcModule.NavMaxStep.Value,
                RootWorld = Origin.World(s.Root.position), RootScene = s.Root.position, GridOrigin = s.GridOrigin,
                FloorY = (float[])s.FloorY.Clone(), Why = (byte[])s.Why.Clone(), Open = (bool[])s.Open.Clone(), Wall = wall, BigArea = big,
                Area = area != null ? (float[])area.Clone() : null, Route = route != null ? route.ToArray() : null,
                Trail = trail != null ? trail.ToArray() : null, TrailKind = trailKind != null ? trailKind.ToArray() : null });
        }

        /// <summary>Captures copies of every baked map on explicit Debug request.</summary>
        internal static void CaptureDebugMaps()
        {
            foreach (var s in _structures) if (s.Baked && s.FloorY != null) Dump(s, "baked", -1, -1, -1, null);
        }

        /// <summary>ShowNavigation overlay: the waypoint each NPC follows and where it came from.</summary>
        internal static void CollectDebug(List<DiagnosticMarker> markers)
        {
            if (!On || !WildWasteland.Core.NavigationDiagnosticState.Overlay) return;
            var cyan = new Color(0.3f, 0.9f, 1f);
            var p = Player();
            foreach (var s in _structures)
            {
                if (s.Root == null) continue;
                if (p != null && s.Box.SqrDistance(p.position) > 150f * 150f) continue;
                DiagnosticMarkers.Label(markers, new Vector3(s.Box.center.x, s.RefY + 3f, s.Box.center.z), s.Name + (s.Baked ? (s.FloorY != null ? " baked, " + s.Walkable + "/" + (s.W * s.H) + " cells" : " (bake failed)") : _baking == s ? " baking " + (100 * s.Next / Math.Max(1, s.W * s.H)) + " %" : " not baked"), cyan);
            }
            float now = Time.time;
            var dead = _dead; dead.Clear();
            foreach (var kv in _debug)
            {
                if (kv.Key == null || now - kv.Value.At > 1.5f) { dead.Add(kv.Key); continue; }
                DiagnosticMarkers.Mark(markers, kv.Value.Next + Vector3.up * 0.3f, cyan, 8f);
                DiagnosticMarkers.Label(markers, kv.Value.Next + Vector3.up * 0.7f, "nav " + kv.Value.Where, cyan);
            }
            foreach (var k in dead) _debug.Remove(k);
        }

        /// <summary>Status line for the Debug page and the log.</summary>
        internal static string Status()
        {
            int b = 0; foreach (var s in _structures) if (s.Baked) b++;
            return _structures.Count + " structures, " + b + " baked";
        }
    }
}
