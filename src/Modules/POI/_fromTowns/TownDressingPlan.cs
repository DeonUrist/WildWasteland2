using System;
using System.Collections.Generic;
using MapMagic.Products;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    // Wave A (A6): split out of Towns/TownOverlayPlan.cs (POI dressing data; the nav overlay part went to NPCAI/Navigation).
    // Numeric description of the perimeter dressing kit, captured once on the main thread. Workers never touch prefabs.
    internal sealed class TownDressingKit
    {
        internal bool TowerAvailable;
        internal Bounds TowerLocal;                                  // non-trigger collider AABB in the tower prefab frame
        internal readonly List<Bounds> TreeLocal = new List<Bounds>(); // per kit tree, same order as TownWallKit.Trees
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
}
