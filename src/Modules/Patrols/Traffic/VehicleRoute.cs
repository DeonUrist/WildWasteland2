using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // Worker-only search over immutable final terrain. Unknown ground cannot certify a vehicle route.
    internal static class VehicleRoute
    {
        private const float Cell = 40f;
        private struct Open { internal int Id; internal float Cost, Score; }
        private sealed class Heap
        {
            private readonly List<Open> _items = new List<Open>();
            internal int Count { get { return _items.Count; } }
            internal void Push(Open value)
            {
                int i = _items.Count; _items.Add(value);
                while (i > 0) { int parent = (i - 1) / 2; if (_items[parent].Score <= value.Score) break; _items[i] = _items[parent]; i = parent; }
                _items[i] = value;
            }
            internal Open Pop()
            {
                var result = _items[0]; var last = _items[_items.Count - 1]; _items.RemoveAt(_items.Count - 1);
                if (_items.Count == 0) return result;
                int i = 0;
                while (i * 2 + 1 < _items.Count)
                { int child = i * 2 + 1; if (child + 1 < _items.Count && _items[child + 1].Score < _items[child].Score) child++;
                    if (last.Score <= _items[child].Score) break; _items[i] = _items[child]; i = child; }
                _items[i] = last; return result;
            }
        }
        internal static Vector3[] Build(Vector3 a, Vector3 b, Vector3[][] lines, ElevationGrid[] grids, Bounds[] blocks, bool convoy, out bool[] roads, out float total)
        {
            roads = null; total = float.PositiveInfinity;
            if (grids.Length == 0) return null;
            if (!ElevationGrid.Sample(grids, a, out a.y, out _) || !ElevationGrid.Sample(grids, b, out b.y, out _)) return null;
            float limit = convoy ? .22f : .30f;
            float x0 = Mathf.Floor((Mathf.Min(a.x, b.x) - 1400f) / Cell) * Cell;
            float z0 = Mathf.Floor((Mathf.Min(a.z, b.z) - 1400f) / Cell) * Cell;
            int width = Mathf.CeilToInt((Mathf.Max(a.x, b.x) + 1400f - x0) / Cell) + 1;
            int depth = Mathf.CeilToInt((Mathf.Max(a.z, b.z) + 1400f - z0) / Cell) + 1;
            if ((long)width * depth > 120000) return null;
            int count = width * depth;
            var heights = new float[count]; var valid = new bool[count]; var road = new bool[count];
            var positions = new Vector3[count];
            var cost = new float[count]; var parent = new int[count];
            for (int i = 0; i < count; i++)
            {
                var p = Point(i, width, x0, z0); positions[i] = p; float slope;
                valid[i] = ElevationGrid.Sample(grids, p, out heights[i], out slope) && slope <= limit && !Inside(blocks, p);
                cost[i] = float.PositiveInfinity; parent[i] = -1;
            }
            // Rasterise actual centreline segments; an unordered hint cloud must never create invented roads.
            foreach (var line in lines) for (int segment = 1; segment < line.Length; segment++)
            {
                var left = line[segment-1]; var right = line[segment];
                if (Mathf.Max(left.x,right.x) < x0 || Mathf.Min(left.x,right.x) > x0+(width-1)*Cell || Mathf.Max(left.z,right.z) < z0 || Mathf.Min(left.z,right.z) > z0+(depth-1)*Cell) continue;
                float distance = TrafficLocations.FlatDistance(line[segment - 1], line[segment]);
                int samples = Mathf.Min(4096, Mathf.CeilToInt(distance / 10f));
                for (int s = 0; s <= samples; s++)
                {
                    var p = Vector3.Lerp(line[segment - 1], line[segment], samples == 0 ? 0 : (float)s / samples);
                    int x = Mathf.RoundToInt((p.x - x0) / Cell), z = Mathf.RoundToInt((p.z - z0) / Cell);
                    if (x < 0 || z < 0 || x >= width || z >= depth) continue;
                    int id = z * width + x; float gap = TrafficLocations.FlatDistance(p, Point(id,width,x0,z0));
                    if (gap >= cost[id]) continue;
                    // Snap to the real road, which can be much narrower than a search cell.
                    cost[id] = gap; positions[id] = p; road[id] = true;
                    valid[id] = ElevationGrid.Sample(grids,p,out heights[id],out float slope) && slope <= limit && !Inside(blocks,p);
                }
            }
            for (int i = 0; i < count; i++) cost[i] = float.PositiveInfinity;
            int start = Mathf.RoundToInt((a.z - z0) / Cell) * width + Mathf.RoundToInt((a.x - x0) / Cell);
            int end = Mathf.RoundToInt((b.z - z0) / Cell) * width + Mathf.RoundToInt((b.x - x0) / Cell);
            // Parking can lie less than one coarse cell from a town wall. Keep the exact exterior endpoints.
            positions[start] = a; heights[start] = a.y; valid[start] = !Inside(blocks, a);
            positions[end] = b; heights[end] = b.y; valid[end] = !Inside(blocks, b);
            var first = positions[start]; first.y = heights[start];
            var last = positions[end]; last.y = heights[end];
            if (!valid[start] || !valid[end] || !Safe(a, first, grids, blocks, limit, out _) || !Safe(last, b, grids, blocks, limit, out _)) return null;
            var open = new Heap(); cost[start] = 0; open.Push(new Open { Id = start, Score = 0, Cost = 0 });
            int visits = 0;
            while (open.Count > 0 && visits++ < 240000)
            {
                var current = open.Pop(); int id = current.Id;
                if (current.Cost != cost[id]) continue;
                if (id == end) break;
                var from = positions[id]; from.y = heights[id];
                int x = id % width, z = id / width;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (dx == 0 && dz == 0 || nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
                    int next = nz * width + nx; if (!valid[next]) continue;
                    var to = positions[next]; to.y = heights[next];
                    if (!Safe(from, to, grids, blocks, limit, out float slope)) continue;
                    float length = Mathf.Max(.01f,TrafficLocations.FlatDistance(from,to));
                    float value = cost[id] + length * (1f + 40f * slope * slope) * (road[id] && road[next] ? .45f : 1f);
                    if (value >= cost[next]) continue;
                    cost[next] = value; parent[next] = id;
                    open.Push(new Open { Id = next, Cost = value, Score = value + TrafficLocations.FlatDistance(to, b) * .45f });
                }
            }
            if (start != end && parent[end] < 0) return null;
            var path = new List<int>(); for (int id = end; id >= 0; id = parent[id]) { path.Add(id); if (id == start) break; }
            path.Reverse();
            var points = new List<Vector3> { a }; var kinds = new List<bool> { false };
            for (int i = 0; i < path.Count; i++)
            {
                // Retain turns and road transitions. Collinear runs preserve every certified edge.
                if (i > 0 && i + 1 < path.Count && path[i] - path[i - 1] == path[i + 1] - path[i] && !road[path[i]] && !road[path[i - 1]] && !road[path[i + 1]]) continue;
                var p = positions[path[i]]; p.y = heights[path[i]];
                points.Add(p); kinds.Add(i > 0 && road[path[i - 1]] && road[path[i]]);
            }
            points.Add(b); kinds.Add(false); roads = kinds.ToArray(); total = cost[end]; return points.ToArray();
        }
        private static Vector3 Point(int id, int width, float x, float z) { return new Vector3(x + id % width * Cell, 0, z + id / width * Cell); }
        internal static Vector3[] Revalidate(Vector3[] route, Vector3[][] lines, ElevationGrid[] grids, Bounds[] blocks, bool convoy, out bool[] roads)
        {
            roads = null; var points = new List<Vector3>(); var kinds = new List<bool>();
            float limit = convoy ? .22f : .30f;
            for (int i = 0; i < route.Length; i++)
            {
                var p = route[i]; if (!ElevationGrid.Sample(grids, p, out p.y, out _)) return null;
                if (i > 0 && !Safe(points[points.Count - 1], p, grids, blocks, limit, out _))
                {
                    var detour = Build(points[points.Count - 1], p, lines, grids, blocks, convoy, out var detourRoads, out _);
                    if (detour == null || points.Count + detour.Length > 20000) return null;
                    for (int k = 1; k < detour.Length; k++) { points.Add(detour[k]); kinds.Add(detourRoads[k]); }
                }
                else { points.Add(p); kinds.Add(false); }
            }
            roads = kinds.ToArray(); return points.ToArray();
        }
        private static bool Inside(Bounds[] blocks, Vector3 p)
        { p.y = 0; foreach (var box in blocks) if (box.Contains(p)) return true; return false; }
        private static bool Safe(Vector3 a, Vector3 b, ElevationGrid[] grids, Bounds[] blocks, float limit, out float roughness)
        {
            roughness = 0; var delta = b - a; delta.y = 0; float length = delta.magnitude;
            foreach (var box in blocks)
            {
                var flat = a; flat.y = 0;
                if (box.Contains(flat) || length > .001f && box.IntersectRay(new Ray(flat, delta / length), out float hit) && hit < length) return false;
            }
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 10f));
            float previous = 0; var side = length > .001f ? Vector3.Cross(Vector3.up, delta / length) * 3f : Vector3.right * 3f;
            for (int i = 0; i <= steps; i++)
            {
                var p = Vector3.Lerp(a, b, (float)i / steps);
                if (!ElevationGrid.Sample(grids, p, out float height, out float slope) || slope > limit) return false;
                if (i > 0 && Mathf.Abs(height - previous) > Mathf.Max(.1f, length / steps) * limit) return false;
                if (!ElevationGrid.Sample(grids, p - side, out float left, out _) || !ElevationGrid.Sample(grids, p + side, out float right, out _)
                    || Mathf.Abs(left - right) > 6f * limit) return false;
                roughness = Mathf.Max(roughness, slope); previous = height;
            }
            return true;
        }
    }
}
