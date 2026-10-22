using System.Collections.Generic;
using MapMagic.Products;
using UnityEngine;

namespace WildWasteland.Modules.Towns
{
    // A small invisible visibility graph. No path paint, road mesh or grid/A* searches.
    internal static class TownWalkGraph
    {
        internal static void Build(TownPlan plan, BuildingSpec[] specs, StopToken stop)
        {
            var ground = new TownGround(plan, specs, null, 0, null);
            var nodes = new List<Vector3>(); var edges = new List<int>(); var outside = new List<int>();
            for (int i = 0; i < plan.LotCount; i++)
            {
                if (stop != null && stop.stop) return;
                var lot = plan.Lots[i]; var entry = TownPlanner.Entrance(lot);
                lot.EntryNode = lot.Street = nodes.Count; nodes.Add(entry);
                if (lot.Skipped) continue;
                var spec = lot.IsParking ? null : Find(specs, lot.Prefab); if (!lot.IsParking && spec == null) continue;
                var direction = Quaternion.Euler(0, lot.Yaw, 0) * (lot.IsParking ? Vector3.back : spec.Outward);
                bool found = false;
                for (int n = 1; n <= 40; n++)
                {
                    var p = entry + direction * (n * .75f); p.y = entry.y;
                    if (!ground.DoorLine(entry, p, i)) break;
                    if (ground.Protected(plan.World(p))) continue;
                    edges.Add(lot.EntryNode); edges.Add(nodes.Count); outside.Add(nodes.Count); nodes.Add(p); found = true; break;
                }
                if (!found && plan.LayoutVersion >= 7)
                { lot.Skipped = true; ground = new TownGround(plan, specs, null, 0, null); }
            }
            // A narrow shared passage runs through the compound and out past both ends.
            // Additional garden-side points let walkers go around the central wreck pocket.
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            foreach (var lot in plan.Lots) if (!lot.Skipped)
            { float x = TownPlanner.Entrance(lot).x; left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
            if (!float.IsPositiveInfinity(left))
                for (float x = left - 6; x <= right + 12; x += 6)
                {
                    float z = plan.LayoutVersion >= 7 && plan.LotCount >= 5 && Mathf.Abs(x) < 12 ? -5 : 0;
                    var p = new Vector3(x, 0, z);
                    if (!ground.Protected(plan.World(p))) { outside.Add(nodes.Count); nodes.Add(p); }
                }
            if (plan.LayoutVersion >= 7 && plan.LotCount >= 5)
                foreach (var p in new[] { new Vector3(-8, 0, -5), new Vector3(0, 0, -5), new Vector3(8, 0, -5), new Vector3(8, 0, 5), new Vector3(0, 0, 5), new Vector3(-8, 0, 5) })
                    if (!ground.Protected(plan.World(p))) { outside.Add(nodes.Count); nodes.Add(p); }
            int perimeter = nodes.Count;
            TownPerimeter.WalkNodes(plan, nodes, outside);
            for (int side = 0; side < 4; side++)
            {
                Add(ground, plan, nodes, edges, perimeter + side * 2, perimeter + side * 2 + 1);
            }
            int bends = TownPerimeter.Outline(plan).Vertices.Length;
            for (int corner = 0; corner < bends; corner++) for (int layer = 0; layer < 2; layer++)
                Add(ground, plan, nodes, edges, perimeter + 8 + corner * 2 + layer, perimeter + 8 + ((corner + 1) % bends) * 2 + layer);
            foreach (int a in outside)
            {
                var choices = new List<int>(outside); choices.Remove(a); choices.Sort((x, y) => (nodes[x] - nodes[a]).sqrMagnitude.CompareTo((nodes[y] - nodes[a]).sqrMagnitude));
                for (int n = 0; n < Mathf.Min(4, choices.Count); n++)
                {
                    int b = choices[n]; if (a > b || !Clear(ground, plan, nodes[a], nodes[b])) continue;
                    edges.Add(a); edges.Add(b);
                }
            }
            plan.StreetNodes = nodes.ToArray(); plan.StreetEdges = edges.ToArray(); plan.MainNodes = 0;
            plan.PathsVersion = 7; plan.PathWidth = 1.2f; plan.AccessPath = new Vector3[0]; plan.GradeAccess = false;
        }
        private static void Add(TownGround ground, TownPlan plan, List<Vector3> nodes, List<int> edges, int a, int b)
        { if (Clear(ground, plan, nodes[a], nodes[b])) { edges.Add(a); edges.Add(b); } }
        private static BuildingSpec Find(BuildingSpec[] specs, string name)
        { foreach (var spec in specs) if (spec.Name == name) return spec; return null; }
        private static bool Clear(TownGround ground, TownPlan plan, Vector3 a, Vector3 b)
        {
            var rotation = Quaternion.Euler(0, plan.Yaw, 0);
            if (TownPerimeter.Outline(plan).Crosses(rotation * a, rotation * b, .5f)) return false;
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .75f)); var previous = a;
            for (int n = 0; n <= samples; n++)
            {
                var p = Vector3.Lerp(a, b, n / (float)samples);
                if (plan.LayoutVersion >= 7 && plan.LotCount >= 5 && Mathf.Abs(p.x) < 6 && Mathf.Abs(p.z) < 3) return false;
                if (ground.Blocked(p, -1) || ground.Blocked(p + Vector3.right * .35f, -1) || ground.Blocked(p - Vector3.right * .35f, -1)
                    || ground.Blocked(p + Vector3.forward * .35f, -1) || ground.Blocked(p - Vector3.forward * .35f, -1) || ground.Wall(previous, p)) return false;
                previous = p;
            }
            return true;
        }
    }
}
