using System;
using System.Collections.Generic;
using MapMagic.Products;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    // Numeric description of the perimeter dressing kit, captured once on the main thread. Workers never touch prefabs.
    internal sealed class TownDressingKit
    {
        internal bool TowerAvailable;
        internal Bounds TowerLocal;                                  // non-trigger collider AABB in the tower prefab frame
        internal readonly List<Bounds> TreeLocal = new List<Bounds>(); // per kit tree, same order as TownWallKit.Trees
        internal readonly List<Bounds> ShellMesh = new List<Bounds>(); // per car shell mesh, same order as TownAssets.CarShells
    }

    // Deterministic placement of towers, border trees/wrecks and the central wreck garden, derived from the plan key only.
    // Positions and boxes use the town-centred world-axis frame (the frame of TownOutline and the cached gate graphs).
    internal sealed class TownDressingPlan
    {
        internal struct Item { internal int Kind, Index; internal Vector3 Position; internal float Yaw, Scale; internal Bounds Box; } // Kind: 0 tower, 1 tree, 2 car shell
        internal Item[] Towers = new Item[0], Props = new Item[0], Garden = new Item[0];
        internal int TowerTarget;
        internal readonly List<Bounds> Blockers = new List<Bounds>();   // towers + border props: cached gate graphs and tower spacing
        internal readonly List<Bounds> GardenBoxes = new List<Bounds>(); // wreck garden: always built with the streets root
        internal static Bounds Rotated(Bounds local, float yaw, float scale, Vector3 pivot)
        {
            var rotation = Quaternion.Euler(0, yaw, 0); var result = new Bounds(); bool any = false;
            for (int c = 0; c < 8; c++)
            {
                var p = pivot + rotation * (Vector3.Scale(local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)), Vector3.one * scale));
                if (!any) { result = new Bounds(p, Vector3.zero); any = true; } else result.Encapsulate(p);
            }
            return result;
        }
        internal static TownDressingPlan Make(TownPlan plan, TownDressingKit kit, List<Bounds> shells)
        {
            var result = new TownDressingPlan();
            // Wreck garden: same seeds, positions and yaws as the original TownDebris construction.
            if (plan.LayoutVersion >= 7 && plan.LotCount >= 5 && shells.Count > 0)
            {
                var garden = new System.Random(TownPlanner.Hash(plan.Key + "/wreck-garden")); var items = new List<Item>();
                for (int i = 0; i < 2; i++)
                {
                    int index = garden.Next(shells.Count); var bounds = shells[index];
                    float scale = Mathf.Min(1, 2.4f / Mathf.Max(.1f, Mathf.Max(bounds.extents.x, bounds.extents.z)));
                    float yaw = plan.Yaw + (i == 0 ? 58 : -64);
                    var point = Quaternion.Euler(0, plan.Yaw, 0) * new Vector3(i == 0 ? -3.1f : 3.1f, 0, .4f * i);
                    var pivot = point - Quaternion.Euler(0, yaw, 0) * (new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale);
                    var box = Rotated(bounds, yaw, scale, pivot);
                    items.Add(new Item { Kind = 2, Index = index, Position = pivot, Yaw = yaw, Scale = scale, Box = box }); result.GardenBoxes.Add(box);
                }
                result.Garden = items.ToArray();
            }
            if (kit == null) return result;
            // Border: the exact random sequence of the original runtime TownBorderProps, so existing towns keep their look.
            var random = new System.Random(TownPlanner.Hash(plan.Key + "/border-props-v1"));
            var corners = new[] { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--) { int j = random.Next(i + 1), t = corners[i]; corners[i] = corners[j]; corners[j] = t; }
            var frame = TownPerimeter.Frame(plan); var outline = TownPerimeter.Outline(plan);
            int buildings = 0; foreach (var lot in plan.Lots) if (!lot.IsParking && !lot.Skipped) buildings++;
            result.TowerTarget = kit.TowerAvailable ? buildings >= 8 || frame.size.x * frame.size.z >= 10000 ? random.Next(3, 5) : random.Next(1, 4) : 0;
            var towers = new List<Item>();
            for (int t = 0; t < result.TowerTarget; t++)
            {
                int corner = corners[t]; Vector3 point;
                // Log.Always: this can run on the MapMagic worker, and Warn's rate-limit table is main-thread only.
                if (!outline.Tower(plan, corner, result.Blockers, out point)) { Log.Always("[Towns] WARNING: no clear tower position " + plan.Key + "/" + corner + "; building and gate clearance retained."); continue; }
                var inward = frame.center - point; inward.y = 0;
                float yaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg - Mathf.Atan2(2.63f, -.71f) * Mathf.Rad2Deg;
                var box = Rotated(kit.TowerLocal, yaw, 1, point);
                towers.Add(new Item { Kind = 0, Position = point, Yaw = yaw, Scale = 1, Box = box }); result.Blockers.Add(box);
            }
            result.Towers = towers.ToArray();
            var props = new List<Item>(); var nodes = plan.StreetNodes; var edges = plan.StreetEdges;
            var cluster = TownPerimeter.Buildings(plan); cluster.Expand(new Vector3(5, 0, 5));
            for (int attempt = 0; attempt < 128 && props.Count < 24; attempt++)
            {
                bool tree = random.Next(3) != 0;
                if (tree ? kit.TreeLocal.Count == 0 : shells.Count == 0) continue;
                var point = frame.center + new Vector3(((float)random.NextDouble() * 2 - 1) * (frame.extents.x - 1), 0, ((float)random.NextDouble() * 2 - 1) * (frame.extents.z - 1));
                float radius = tree ? 1.5f : 2.5f;
                if (!outline.Contains(point, radius + 1)) continue;
                if (Mathf.Abs(point.x - cluster.center.x) < cluster.extents.x && Mathf.Abs(point.z - cluster.center.z) < cluster.extents.z) continue;
                if (Mathf.Abs(point.x - frame.center.x) < 12 + radius || Mathf.Abs(point.z - frame.center.z) < 12 + radius) continue;
                if (TownPerimeter.BuildingLine(plan, point, point, radius + 2)) continue;
                bool overlap = false;
                foreach (var b in result.Blockers) if (Mathf.Abs(point.x - b.center.x) < b.extents.x + radius + 2 && Mathf.Abs(point.z - b.center.z) < b.extents.z + radius + 2) { overlap = true; break; }
                if (overlap) continue;
                bool street = false;
                for (int e = 0; e < edges.Length && !street; e += 2)
                {
                    var a = plan.World(nodes[edges[e]]) - plan.WorldCenter; var b = plan.World(nodes[edges[e + 1]]) - plan.WorldCenter;
                    if (TownOutline.Distance(point, a, b) < (radius + 3) * (radius + 3)) street = true;
                }
                if (street) continue;
                Item item;
                if (tree)
                {
                    int index = random.Next(kit.TreeLocal.Count); float yaw = random.Next(360);
                    var box = Rotated(kit.TreeLocal[index], yaw, 1, point);
                    var size = box.size; size.x = Mathf.Max(size.x, .8f); size.z = Mathf.Max(size.z, .8f); box.size = size;
                    item = new Item { Kind = 1, Index = index, Position = point, Yaw = yaw, Scale = 1, Box = box };
                }
                else
                {
                    int index = random.Next(shells.Count); var bounds = shells[index];
                    float scale = Mathf.Min(1, 2f / Mathf.Max(.1f, Mathf.Max(bounds.extents.x, bounds.extents.z)));
                    float yaw = random.Next(360);
                    var pivot = point - Quaternion.Euler(0, yaw, 0) * (new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale);
                    item = new Item { Kind = 2, Index = index, Position = pivot, Yaw = yaw, Scale = scale, Box = Rotated(bounds, yaw, scale, pivot) };
                }
                props.Add(item); result.Blockers.Add(item.Box);
            }
            result.Props = props.ToArray();
            return result;
        }
    }

    // Per-building instance overlay computed from the plan and the read-only prefab templates, normally on the MapMagic worker.
    // Applying it at runtime is a few array writes per building; the physics-free result equals the former sliced projection.
    internal sealed class TownOverlayPlan
    {
        internal sealed class LotPatch
        {
            internal int W, H; internal float Cell;
            internal int[] BaseSolid = new int[0], EdgeCells = new int[0], DressSolid = new int[0];
            internal byte[] EdgeClear = new byte[0];
            internal bool Any(bool dressing) { return BaseSolid.Length + EdgeCells.Length + (dressing ? DressSolid.Length : 0) > 0; }
        }
        internal LotPatch[] Lots;
        internal TownDressingPlan Dressing;
        internal int Blocked, Edges;
        private static bool Solid(TownBakeData map, int i) { return i >= 0 && (map.Why[i] == 2 || map.Why[i] == 3); }
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
        private static bool Inside(Bounds box, Vector3 p, float floor)
        { return p.x >= box.min.x && p.x <= box.max.x && p.z >= box.min.z && p.z <= box.max.z && floor + 1.8f >= box.min.y && floor + .1f <= box.max.y; }
        internal static TownOverlayPlan Build(TownPlan plan, BuildingSpec[] specs, TownDressingKit kit, List<Bounds> shells, float step, StopToken stop)
        {
            int count = plan.LotCount; var result = new TownOverlayPlan { Lots = new LotPatch[count], Dressing = TownDressingPlan.Make(plan, kit, shells) };
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
        // Fallback for plans that reach the main thread without a worker-computed overlay (retained saved record, module restart).
        internal static TownDressingPlan DressingFor(TownPlan plan, TownAssets assets)
        {
            if (plan.Overlay != null && plan.Overlay.Dressing != null) return plan.Overlay.Dressing;
            if (plan.DressingOnly == null) plan.DressingOnly = TownDressingPlan.Make(plan, assets.WallKit.Dressing, assets.ShellBounds);
            return plan.DressingOnly;
        }
        internal static bool SameLayout(TownPlan a, TownPlan b)
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
