using System;
using System.Collections.Generic;
using MapMagic.Products;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    // Per-building instance overlay computed from the plan and the read-only prefab templates, normally on the MapMagic worker.
    // Applying it at runtime is a few array writes per building; the physics-free result equals the former sliced projection.
    internal sealed class TownOverlayPlan
    {
        internal sealed class LotPatch
        {
            internal int W, H; internal float Cell;
            internal int[] BaseSolid = new int[0], EdgeCells = new int[0], DressSolid = new int[0];
            internal byte[] EdgeClear = new byte[0];
            /// <summary>True when the patch changes anything (dressing cells counted when asked).</summary>
            internal bool Any(bool dressing) { return BaseSolid.Length + EdgeCells.Length + (dressing ? DressSolid.Length : 0) > 0; }
        }
        internal LotPatch[] Lots;
        internal TownDressingPlan Dressing;   // refs-ok: REFACTOR-REQUEST B08-6
        internal int Blocked, Edges;
        /// <summary>True when cell i of a template is wall or rock.</summary>
        private static bool Solid(TownBakeData map, int i) { return i >= 0 && (map.Why[i] == 2 || map.Why[i] == 3); }
        /// <summary>True when two neighbouring template cells are joined within the step (or by a hop edge).</summary>
        private static bool Step(TownBakeData map, int a, int b, float step)
        {
            if (float.IsNaN(map.Floor[a]) || float.IsNaN(map.Floor[b])) return false;
            int dx = b % map.W - a % map.W, dz = b / map.W - a / map.W;
            if (Mathf.Abs(dx) > 1 || Mathf.Abs(dz) > 1) return false;
            if (dx == 0 && dz == 0) return true;
            if (dz < 0 || dz == 0 && dx < 0) { int t = a; a = b; b = t; dx = -dx; dz = -dz; }
            int k = dz == 0 ? 0 : dx == 0 ? 1 : dx > 0 ? 2 : 3;
            return Mathf.Abs(map.Floor[a] - map.Floor[b]) <= step && (map.Edges[a] & (1 << k)) != 0 || k < 2 && (map.Edges[a] & (1 << (k + 4))) != 0;
        }
        /// <summary>True when a cell point at floor height is covered by a body-height part of the box.</summary>
        private static bool Inside(Bounds box, Vector3 p, float floor)
        { return p.x >= box.min.x && p.x <= box.max.x && p.z >= box.min.z && p.z <= box.max.z && floor + 1.8f >= box.min.y && floor + .1f <= box.max.y; }
        /// <summary>Worker-thread overlay of a planned town: per lot the cells and edges its neighbours, shells and dressing block (physics-free, from the read-only templates).</summary>
        internal static TownOverlayPlan Build(TownPlan plan, BuildingSpec[] specs, TownDressingKit kit, List<Bounds> shells, float step, StopToken stop)   // refs-ok: REFACTOR-REQUEST B08-6
        {
            int count = plan.LotCount; var result = new TownOverlayPlan { Lots = new LotPatch[count], Dressing = TownDressingPlan.Make(plan, kit, shells) };   // refs-ok: REFACTOR-REQUEST B08-6
            var maps = new TownBakeData[count]; var toRoot = new Quaternion[count]; var fromRoot = new Quaternion[count]; var boxes = new Vector4[count];
            var town = Quaternion.Euler(0, plan.Yaw, 0);
            for (int i = 0; i < count; i++)
            {
                var lot = plan.Lots[i]; if (lot.IsParking || lot.Skipped) continue;
                foreach (var spec in specs) if (spec.Name == lot.Prefab) { maps[i] = spec.Bake; break; }
                if (maps[i] == null) continue;
                fromRoot[i] = Quaternion.Euler(0, lot.Yaw, 0); toRoot[i] = Quaternion.Euler(0, -lot.Yaw, 0);
                var map = maps[i]; var box = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);
                for (int c = 0; c < 4; c++)
                {
                    var p = town * (lot.LocalPosition + fromRoot[i] * (map.Min + new Vector3((c & 1) == 0 ? 0 : map.W * map.Cell, 0, (c & 2) == 0 ? 0 : map.H * map.Cell)));
                    box.x = Mathf.Min(box.x, p.x - map.Cell); box.y = Mathf.Min(box.y, p.z - map.Cell); box.z = Mathf.Max(box.z, p.x + map.Cell); box.w = Mathf.Max(box.w, p.z + map.Cell);
                }
                boxes[i] = box;
            }
            var garden = result.Dressing.GardenBoxes; var dress = result.Dressing.Blockers;
            var gardenBoxes = new List<Bounds>(); foreach (var b in garden) { var e = b; e.Expand(.3f); gardenBoxes.Add(e); }
            var dressBoxes = new List<Bounds>(); foreach (var b in dress) { var e = b; e.Expand(.3f); dressBoxes.Add(e); }
            var solidBase = new List<int>(); var solidDress = new List<int>(); var edgeCells = new List<int>(); var edgeClear = new List<byte>();
            for (int t = 0; t < count; t++)
            {
                var map = maps[t]; if (map == null) continue;
                if (stop != null && stop.stop) return null;
                var neighbours = new List<int>();
                for (int s = 0; s < count; s++)
                    if (s != t && maps[s] != null && boxes[s].x <= boxes[t].z && boxes[s].z >= boxes[t].x && boxes[s].y <= boxes[t].w && boxes[s].w >= boxes[t].y) neighbours.Add(s);
                bool gardenNear = false, dressNear = false;
                foreach (var b in gardenBoxes) if (b.min.x <= boxes[t].z && b.max.x >= boxes[t].x && b.min.z <= boxes[t].w && b.max.z >= boxes[t].y) { gardenNear = true; break; }
                foreach (var b in dressBoxes) if (b.min.x <= boxes[t].z && b.max.x >= boxes[t].x && b.min.z <= boxes[t].w && b.max.z >= boxes[t].y) { dressNear = true; break; }
                var patch = new LotPatch { W = map.W, H = map.H, Cell = map.Cell }; result.Lots[t] = patch;
                if (neighbours.Count == 0 && !gardenNear && !dressNear) continue;
                int n = map.W * map.H; var solid = new bool[n]; var work = (byte[])map.Edges.Clone();
                solidBase.Clear(); solidDress.Clear(); edgeCells.Clear(); edgeClear.Clear();
                var lot = plan.Lots[t];
                for (int i = 0; i < n; i++)
                {
                    var root = new Vector3(map.Min.x + (i % map.W + .5f) * map.Cell, 0, map.Min.z + (i / map.W + .5f) * map.Cell);
                    var local = lot.LocalPosition + fromRoot[t] * root; var o = town * local; float floor = map.Floor[i];
                    if (gardenNear && !float.IsNaN(floor)) foreach (var b in gardenBoxes) if (Inside(b, o, floor)) { solid[i] = true; solidBase.Add(i); break; }
                    if (dressNear && !solid[i] && !float.IsNaN(floor)) foreach (var b in dressBoxes) if (Inside(b, o, floor)) { solidDress.Add(i); break; }
                    foreach (int s in neighbours)
                    {
                        var source = maps[s]; var sourceLot = plan.Lots[s]; var near = boxes[s];
                        if (o.x < near.x - source.Cell || o.z < near.y - source.Cell || o.x > near.z + source.Cell || o.z > near.w + source.Cell) continue; // cell far from this neighbour's grid
                        int a = source.Index(toRoot[s] * (local - sourceLot.LocalPosition));
                        if (Solid(source, a)) { if (!solid[i]) { solid[i] = true; solidBase.Add(i); } break; }
                        if (float.IsNaN(floor)) continue;
                        for (int k = 0; k < 4; k++)
                        {
                            int dx = k == 0 || k == 2 ? 1 : k == 3 ? -1 : 0, dz = k == 0 ? 0 : 1;
                            int x = i % map.W + dx, z = i / map.W + dz;
                            if (x < 0 || z < 0 || x >= map.W || z >= map.H) continue;
                            int bits = (1 << k) | (k < 2 ? 1 << (k + 4) : 0);
                            if ((work[i] & bits) == 0) continue;
                            var endRoot = new Vector3(map.Min.x + (x + .5f) * map.Cell, 0, map.Min.z + (z + .5f) * map.Cell);
                            var endLocal = lot.LocalPosition + fromRoot[t] * endRoot;
                            int previous = a, samples = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(root, endRoot) / (source.Cell * .25f)));
                            for (int q = 1; q <= samples; q++)
                            {
                                int b = source.Index(toRoot[s] * (Vector3.Lerp(local, endLocal, q / (float)samples) - sourceLot.LocalPosition));
                                bool blocked = Solid(source, b) || previous >= 0 && b >= 0 && previous != b && !float.IsNaN(source.Floor[previous]) && !float.IsNaN(source.Floor[b]) && !Step(source, previous, b, step);
                                if (blocked) { work[i] &= (byte)~bits; break; }
                                previous = b;
                            }
                        }
                    }
                }
                for (int i = 0; i < n; i++) if (!solid[i] && work[i] != map.Edges[i]) { edgeCells.Add(i); edgeClear.Add((byte)(map.Edges[i] & ~work[i])); }
                patch.BaseSolid = solidBase.ToArray(); patch.DressSolid = solidDress.ToArray(); patch.EdgeCells = edgeCells.ToArray(); patch.EdgeClear = edgeClear.ToArray();
                result.Blocked += patch.BaseSolid.Length; result.Edges += patch.EdgeCells.Length;
            }
            return result;
        }
        /// <summary>Fallback for plans that reach the main thread without a worker-computed overlay (retained saved record, module restart).</summary>
        internal static TownDressingPlan DressingFor(TownPlan plan, TownAssets assets)   // refs-ok: REFACTOR-REQUEST B08-6
        {
            if (plan.Overlay != null && plan.Overlay.Dressing != null) return plan.Overlay.Dressing;
            if (plan.DressingOnly == null) plan.DressingOnly = TownDressingPlan.Make(plan, assets.WallKit.Dressing, assets.ShellBounds);   // refs-ok: REFACTOR-REQUEST B08-6
            return plan.DressingOnly;
        }
        /// <summary>True when two plans have the same lots, positions, yaws, skips and street count (the overlay can be reused).</summary>
        internal static bool SameLayout(TownPlan a, TownPlan b)   // refs-ok: REFACTOR-REQUEST B08-6
        {
            if (a == null || b == null || a.LotCount != b.LotCount || a.Yaw != b.Yaw || a.LayoutVersion != b.LayoutVersion) return false;
            for (int i = 0; i < a.LotCount; i++)
            {
                var x = a.Lots[i]; var y = b.Lots[i];
                if (x.Prefab != y.Prefab || x.LocalPosition != y.LocalPosition || x.Yaw != y.Yaw || x.Skipped != y.Skipped) return false;
            }
            return a.StreetEdges.Length == b.StreetEdges.Length;
        }
    }
}
