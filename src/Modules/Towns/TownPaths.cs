using System;
using System.Collections.Generic;
using Den.Tools;
using Den.Tools.Matrices;
using Den.Tools.Splines;
using MapMagic.Nodes.ObjectsGenerators;
using MapMagic.Products;
using UnityEngine;

namespace WildWasteland.Modules.Towns
{
    internal sealed class TownWorldRoads
    {
        private readonly List<Vector3> _segments = new List<Vector3>();
        private readonly float _minX, _minZ, _maxX, _maxZ;
        internal TownWorldRoads(SplineSys roads, TileData data)
        {
            var area = data.area.active;
            _minX = area.worldPos.x + 12; _minZ = area.worldPos.z + 12;
            _maxX = area.worldPos.x + area.worldSize.x - 12; _maxZ = area.worldPos.z + area.worldSize.z - 12;
            if (roads != null) foreach (var line in roads.lines) foreach (var segment in line.segments)
            {
                int samples = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(segment.start.pos, segment.end.pos) / 16), 8, 512);
                var previous = segment.GetPoint(0); previous.y = 0;
                for (int i = 1; i <= samples; i++)
                {
                    var point = segment.GetPoint(i / (float)samples); point.y = 0;
                    _segments.Add(previous); _segments.Add(point); previous = point;
                }
            }
            if (Core.VistasWorldGen.Present) foreach (var line in Core.VistasWorldGen.RoadLines)
                for (int i = 1; i < line.Length; i++)
                {
                    var a = line[i - 1]; var b = line[i]; a.y = b.y = 0;
                    if (Mathf.Max(a.x, b.x) < _minX || Mathf.Min(a.x, b.x) > _maxX
                        || Mathf.Max(a.z, b.z) < _minZ || Mathf.Min(a.z, b.z) > _maxZ) continue;
                    int samples = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / 16), 1, 512);
                    for (int s = 0; s < samples; s++)
                    { _segments.Add(Vector3.Lerp(a, b, s / (float)samples)); _segments.Add(Vector3.Lerp(a, b, (s + 1) / (float)samples)); }
                }
            if (!Core.VistasWorldGen.Present) return;
            // Vistas publishes roads from workers. Stable ordering keeps site selection reproducible.
            var segments = new List<Vector3[]>();
            for (int i = 0; i < _segments.Count; i += 2) segments.Add(new[] { _segments[i], _segments[i + 1] });
            segments.Sort((a, b) => { int c = a[0].x.CompareTo(b[0].x); if (c == 0) c = a[0].z.CompareTo(b[0].z);
                if (c == 0) c = a[1].x.CompareTo(b[1].x); return c == 0 ? a[1].z.CompareTo(b[1].z) : c; });
            _segments.Clear(); foreach (var segment in segments) _segments.AddRange(segment);
        }
        internal bool Inside(Vector3 p) { return p.x >= _minX && p.x <= _maxX && p.z >= _minZ && p.z <= _maxZ; }
        internal bool Nearest(Vector3 position, float limit, out Vector3 point)
        {
            point = Vector3.zero; position.y = 0; float best = limit * limit;
            for (int i = 0; i < _segments.Count; i += 2)
            {
                var a = _segments[i]; var d = _segments[i + 1] - a;
                var p = a + d * Mathf.Clamp01(Vector3.Dot(position - a, d) / Mathf.Max(.001f, d.sqrMagnitude));
                if (!Inside(p)) continue;
                float distance = (p - position).sqrMagnitude;
                if (distance >= best) continue; best = distance; point = p;
            }
            return best < limit * limit;
        }
        internal List<Vector3> Sites(float outer, float reach)
        {
            var points = new List<Vector3>();
            int stride = Mathf.Max(2, (_segments.Count / 96 / 2) * 2);
            for (int i = 0; i < _segments.Count; i += stride)
            {
                var a = _segments[i]; var b = _segments[i + 1]; var direction = Vector3.Cross(Vector3.up, (b - a).normalized);
                for (int side = -1; side <= 1; side += 2) for (int offset = 0; offset < 3; offset++)
                {
                    float distance = outer + 24 + offset * Mathf.Max(24, (reach - outer - 48) * .4f);
                    if (distance >= reach) continue;
                    var p = (a + b) * .5f + direction * (side * distance);
                    if (Inside(p)) points.Add(p);
                }
            }
            return points;
        }
        internal List<Vector3> Connections(Vector3 position, float limit)
        {
            var points = new List<Vector3>(); var distances = new List<float>(); position.y = 0;
            for (int i = 0; i < _segments.Count; i += 2)
            {
                var a = _segments[i]; var d = _segments[i + 1] - a;
                var p = a + d * Mathf.Clamp01(Vector3.Dot(position - a, d) / Mathf.Max(.001f, d.sqrMagnitude));
                float distance = (p - position).sqrMagnitude;
                if (!Inside(p) || distance >= limit * limit) continue;
                bool nearby = false; foreach (var old in points) if ((old - p).sqrMagnitude < 64 * 64) { nearby = true; break; }
                if (nearby) continue;
                int at = distances.FindIndex(value => distance < value); if (at < 0) at = points.Count;
                if (at >= 8) continue;
                points.Insert(at, p); distances.Insert(at, distance);
                if (points.Count > 8) { points.RemoveAt(8); distances.RemoveAt(8); }
            }
            return points;
        }
    }

    // Numeric prefab maps only. Workers never query scene objects or physics.
    internal sealed class TownGround
    {
        private readonly TownPlan _plan;
        private readonly TownBakeData[] _maps;
        private readonly Quaternion[] _inverse;
        private readonly Vector4[] _bounds;
        private readonly MatrixWorld _heights;
        private readonly float _scale;
        private readonly List<Vector3> _avoid;
        internal TownGround(TownPlan plan, BuildingSpec[] specs, MatrixWorld heights, float scale, List<Vector3> avoid)
        {
            _plan = plan; _scale = scale;
            if (avoid != null) { _avoid = new List<Vector3>(avoid.Count); foreach (var p in avoid) _avoid.Add(Local(p)); }
            _maps = new TownBakeData[plan.LotCount]; _inverse = new Quaternion[plan.LotCount];
            _bounds = new Vector4[plan.LotCount];
            for (int i = 0; i < plan.LotCount; i++)
            {
                if (plan.Lots[i].IsParking || plan.Lots[i].Skipped) continue;
                foreach (var spec in specs) if (spec.Name == plan.Lots[i].Prefab) { _maps[i] = spec.Bake; break; }
                if (_maps[i] == null) throw new InvalidOperationException("Entrance path requires matching bake for " + plan.Lots[i].Prefab);
                _inverse[i] = Quaternion.Euler(0, -plan.Lots[i].Yaw, 0);
                var map = _maps[i]; var rotation = Quaternion.Euler(0, plan.Lots[i].Yaw, 0);
                var box = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);
                for (int c = 0; c < 4; c++)
                {
                    var p = plan.Lots[i].LocalPosition + rotation * (map.Min + new Vector3((c & 1) == 0 ? 0 : map.W * map.Cell, 0, (c & 2) == 0 ? 0 : map.H * map.Cell));
                    box.x = Mathf.Min(box.x, p.x - .01f); box.y = Mathf.Min(box.y, p.z - .01f);
                    box.z = Mathf.Max(box.z, p.x + .01f); box.w = Mathf.Max(box.w, p.z + .01f);
                }
                _bounds[i] = box;
            }
            if (heights != null)
            {
                _heights = new MatrixWorld(heights);
                Flatten200.StampLevel(_heights, plan.WorldCenter.y / scale, new Vector2D(plan.WorldCenter.x, plan.WorldCenter.z),
                    plan.Radius + plan.Feather, plan.Radius / (plan.Radius + plan.Feather));
            }
        }
        internal Vector3 Local(Vector3 world) { return Quaternion.Euler(0, -_plan.Yaw, 0) * (world - _plan.WorldCenter); }
        internal bool Protected(Vector3 world) { return Blocked(Local(world), -1); }
        private bool Contains(int i, Vector3 p)
        { var b = _bounds[i]; return _maps[i] != null && p.x >= b.x && p.z >= b.y && p.x <= b.z && p.z <= b.w; }
        internal bool Blocked(Vector3 local, int door)
        {
            for (int i = 0; i < _maps.Length; i++)
            {
                if (!Contains(i, local)) continue;
                var map = _maps[i]; int index = map.Index(_inverse[i] * (local - _plan.Lots[i].LocalPosition));
                if (index < 0 || !map.Protected(index)) continue;
                if (i == door && map.Why[index] == 0 && !float.IsNaN(map.Floor[index])) continue;
                return true;
            }
            if (_avoid != null)
            {
                foreach (var site in _avoid) { float x = local.x - site.x, z = local.z - site.z; if (x * x + z * z < 120 * 120) return true; }
            }
            return false;
        }
        internal float Height(Vector3 local)
        {
            if (_heights == null) return 0;
            var p = _plan.World(local);
            return _heights.GetWorldInterpolatedValue(p.x, p.z) * _scale - _plan.WorldCenter.y;
        }
        internal bool Wall(Vector3 a, Vector3 b)
        {
            for (int i = 0; i < _maps.Length; i++)
            {
                if (!Contains(i, a) || !Contains(i, b)) continue;
                var map = _maps[i]; var lot = _plan.Lots[i];
                int from = map.Index(_inverse[i] * (a - lot.LocalPosition)), to = map.Index(_inverse[i] * (b - lot.LocalPosition));
                if (from < 0 || to < 0 || from == to || float.IsNaN(map.Floor[from]) || float.IsNaN(map.Floor[to])) continue;
                if (!map.Walk(from, to)) return true;
            }
            return false;
        }
        internal bool DoorLine(Vector3 a, Vector3 b, int door)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) * 5)); var previous = a;
            for (int i = 0; i <= steps; i++)
            { var p = Vector3.Lerp(a, b, i / (float)steps); if (Blocked(p, door) || Wall(previous, p)) return false; previous = p; }
            return true;
        }
        internal string Blocker(Vector3 local, int door)
        {
            for (int i = 0; i < _maps.Length; i++)
            {
                var map = _maps[i]; if (map == null) continue; var p = _inverse[i] * (local - _plan.Lots[i].LocalPosition); int cell = map.Index(p);
                if (cell < 0 || !map.Protected(cell)) continue;
                if (i == door && map.Why[cell] == 0 && !float.IsNaN(map.Floor[cell])) continue;
                return _plan.Lots[i].Prefab + "/" + i + " cell=" + cell % map.W + "," + cell / map.W + " why=" + map.Why[cell]
                    + " sky=" + map.Sky[cell] + " local=" + p + " at=" + local;
            }
            return "native POI exclusion at " + local;
        }
        internal string DoorFailure(Vector3 a, Vector3 b, int door)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) * 5)); var previous = a;
            for (int s = 0; s <= steps; s++)
            {
                var p = Vector3.Lerp(a, b, s / (float)steps);
                if (Blocked(p, door)) return "cell " + Blocker(p, door);
                if (Wall(previous, p))
                    for (int i = 0; i < _maps.Length; i++)
                    {
                        var map = _maps[i]; if (map == null) continue; var lot = _plan.Lots[i];
                        int from = map.Index(_inverse[i] * (previous - lot.LocalPosition)), to = map.Index(_inverse[i] * (p - lot.LocalPosition));
                        if (from < 0 || to < 0 || from == to || float.IsNaN(map.Floor[from]) || float.IsNaN(map.Floor[to]) || map.Walk(from, to)) continue;
                        return "edge " + lot.Prefab + "/" + i + " " + from % map.W + "," + from / map.W + "->" + to % map.W + "," + to / map.W
                            + " bits=" + map.Edges[from] + "/" + map.Edges[to] + " at=" + p;
                    }
                previous = p;
            }
            return "clear";
        }
    }

    internal static class TownPaths
    {
        private struct Open { internal int Cell; internal float Cost; }
        internal sealed class Heap
        {
            private readonly List<Open> _items = new List<Open>();
            internal int Count { get { return _items.Count; } }
            internal void Push(int cell, float cost)
            {
                int i = _items.Count; _items.Add(new Open());
                while (i > 0) { int p = (i - 1) / 2; if (_items[p].Cost <= cost) break; _items[i] = _items[p]; i = p; }
                _items[i] = new Open { Cell = cell, Cost = cost };
            }
            internal int Pop()
            {
                int result = _items[0].Cell, last = _items.Count - 1; var tail = _items[last]; _items.RemoveAt(last);
                if (last == 0) return result;
                int i = 0;
                while (i * 2 + 1 < last)
                {
                    int c = i * 2 + 1; if (c + 1 < last && _items[c + 1].Cost < _items[c].Cost) c++;
                    if (_items[c].Cost >= tail.Cost) break; _items[i] = _items[c]; i = c;
                }
                _items[i] = tail; return result;
            }
        }
        private sealed class Grid
        {
            private readonly TownGround _ground;
            private readonly TownPlan _plan;
            private readonly TownWorldRoads _roads;
            private readonly StopToken _stop;
            private readonly float _x, _z, _cell, _radius;
            private readonly int _w, _h;
            private readonly byte[] _blocked;
            private readonly byte[] _checkedEdges, _openEdges;
            private readonly Vector3[] _rim = new Vector3[8];
            private readonly float[] _height;
            private readonly bool _outside, _gentle;
            internal Grid(TownGround ground, TownPlan plan, TownWorldRoads roads, Vector3 min, Vector3 max, float cell, float width, bool outside, StopToken stop, bool gentle = true)
            {
                _ground = ground; _plan = plan; _roads = roads; _stop = stop; _cell = cell; _radius = width * .5f + .4f; _outside = outside; _gentle = gentle;
                _x = Mathf.Floor(min.x / cell) * cell; _z = Mathf.Floor(min.z / cell) * cell;
                _w = Mathf.CeilToInt((max.x - _x) / cell) + 1; _h = Mathf.CeilToInt((max.z - _z) / cell) + 1;
                if (_w * _h > 1200000) throw new InvalidOperationException("Town path footprint exceeds bounded routing grid.");
                _blocked = new byte[_w * _h]; _height = new float[_w * _h];
                _checkedEdges = new byte[_w * _h]; _openEdges = new byte[_w * _h];
                for (int i = 0; i < _rim.Length; i++) { float a = i * Mathf.PI * .25f; _rim[i] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * _radius; }
            }
            private bool Blocked(int i)
            {
                if (_blocked[i] == 0)
                {
                    var p = Point(i); bool blocked = !_roads.Inside(_plan.World(p)) || !Room(p, _radius);
                    _blocked[i] = blocked ? (byte)2 : (byte)1;
                    if (_outside && !blocked) _height[i] = _ground.Height(p);
                }
                return _blocked[i] == 2;
            }
            private void Cancel(int i) { if ((i & 255) == 0 && _stop != null && _stop.stop) throw new OperationCanceledException(); }
            private bool Room(Vector3 p, float radius)
            {
                if (_ground.Blocked(p, -1)) return false;
                // Sample the swept width too, including diagonals; path centers alone miss thin walls.
                for (int d = 0; d < 8; d++)
                {
                    if (_ground.Blocked(p + _rim[d], -1)) return false;
                }
                return true;
            }
            private int Index(Vector3 p)
            { int x = Mathf.RoundToInt((p.x - _x) / _cell), z = Mathf.RoundToInt((p.z - _z) / _cell); return x < 0 || z < 0 || x >= _w || z >= _h ? -1 : z * _w + x; }
            private Vector3 Point(int i) { return new Vector3(_x + i % _w * _cell, 0, _z + i / _w * _cell); }
            internal bool Free(Vector3 p)
            { int i = Index(p); return i >= 0 && !Blocked(i) && ((p - Point(i)).sqrMagnitude < .000001f || Room(p, _radius)); }
            internal string ClearanceFailure(Vector3 p)
            {
                int i = Index(p); if (i < 0) return "outside routing grid";
                if (Blocked(i)) p = Point(i);
                if (!_roads.Inside(_plan.World(p))) return "outside terrain block";
                if (_ground.Blocked(p, -1)) return "center " + _ground.Blocker(p, -1);
                for (int d = 0; d < 8; d++)
                {
                    float a = d * Mathf.PI * .25f;
                    var rim = p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * _radius;
                    if (_ground.Blocked(rim, -1)) return "width radius=" + _radius + " " + _ground.Blocker(rim, -1);
                }
                return "clear";
            }
            internal bool Line(Vector3 a, Vector3 b)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .2f));
                float previous = _outside ? _ground.Height(a) : 0;
                var previousPoint = a;
                for (int t = 0; t <= n; t++)
                {
                    var p = Vector3.Lerp(a, b, t / (float)n); if (!Free(p)) return false;
                    if (_ground.Wall(previousPoint, p)) return false; previousPoint = p;
                    if (!_outside) continue;
                    float y = _ground.Height(p);
                    if (_gentle && t > 0 && Mathf.Abs(y - previous) > Mathf.Max(.12f, Vector3.Distance(a, b) / n * .25f)) return false;
                    previous = y;
                }
                return true;
            }
            internal List<Vector3> Route(Vector3 start, Vector3 end)
            {
                int from = Index(start), to = Index(end);
                if (from < 0 || to < 0 || Blocked(from) || Blocked(to)) return null;
                if (Line(start, end)) return Smooth(new List<Vector3> { start, end });
                if (from == to) return null;
                var costs = new float[_blocked.Length]; var parent = new int[costs.Length]; var closed = new bool[costs.Length];
                for (int i = 0; i < costs.Length; i++) { costs[i] = float.PositiveInfinity; parent[i] = -1; }
                costs[from] = 0; var open = new Heap(); open.Push(from, 0); int ops = 0;
                while (open.Count > 0)
                {
                    Cancel(ops++); if (ops > 24000) return null;
                    int a = open.Pop(); if (closed[a]) continue; closed[a] = true;
                    if (a == to)
                    {
                        var path = new List<Vector3>(); for (int i = to; i >= 0; i = parent[i]) path.Add(Point(i)); path.Reverse();
                        path[0] = start; path[path.Count - 1] = end;
                        return Smooth(path);
                    }
                    int x = a % _w, z = a / _w;
                    for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int cx = x + dx, cz = z + dz;
                        if (dx == 0 && dz == 0 || cx < 0 || cz < 0 || cx >= _w || cz >= _h) continue;
                        int b = cz * _w + cx;
                        if (closed[b] || Blocked(b) || dx != 0 && dz != 0 && (Blocked(z * _w + cx) || Blocked(cz * _w + x))) continue;
                        float length = _cell * (dx != 0 && dz != 0 ? 1.414214f : 1);
                        if (_outside && _gentle && Mathf.Abs(_height[a] - _height[b]) > length * .25f) continue;
                        int bit = (dz + 1) * 3 + dx + 1; if (bit > 4) bit--;
                        if ((_checkedEdges[a] & (1 << bit)) == 0)
                        {
                            bool clear = Line(Point(a), Point(b));
                            _checkedEdges[a] |= (byte)(1 << bit); _checkedEdges[b] |= (byte)(1 << (7 - bit));
                            if (clear) { _openEdges[a] |= (byte)(1 << bit); _openEdges[b] |= (byte)(1 << (7 - bit)); }
                        }
                        if ((_openEdges[a] & (1 << bit)) == 0) continue;
                        var p = Point(b);
                        float wander = 1 + .12f * Mathf.Sin(p.x * .093f + p.z * .071f + _plan.Yaw);
                        float cost = costs[a] + length * wander;
                        if (cost >= costs[b]) continue;
                        costs[b] = cost; parent[b] = a;
                        open.Push(b, cost + Vector3.Distance(p, Point(to)) * .85f);
                    }
                }
                return null;
            }
            private List<Vector3> Smooth(List<Vector3> path)
            {
                var pulled = new List<Vector3>(); int from = 0; pulled.Add(path[0]);
                while (from < path.Count - 1)
                {
                    int to = from + 1;
                    for (int i = from + 2; i < path.Count; i++)
                    { if (!Line(path[from], path[i])) break; to = i; }
                    pulled.Add(path[to]); from = to;
                }
                var curve = new List<Vector3>(); curve.Add(pulled[0]);
                for (int i = 1; i < pulled.Count - 1; i++)
                {
                    var a = Vector3.Lerp(pulled[i], pulled[i - 1], .2f); var b = Vector3.Lerp(pulled[i], pulled[i + 1], .2f);
                    if (Line(curve[curve.Count - 1], a) && Line(a, b) && Line(b, pulled[i + 1])) { curve.Add(a); curve.Add(b); }
                    else curve.Add(pulled[i]);
                }
                if (pulled.Count > 1) curve.Add(pulled[pulled.Count - 1]);
                // A little validated wander on long open legs avoids ruler-straight paths.
                var result = new List<Vector3>(); result.Add(curve[0]);
                for (int i = 1; i < curve.Count; i++)
                {
                    var a = curve[i - 1]; var b = curve[i]; var d = b - a; float length = d.magnitude;
                    var mid = (a + b) * .5f + Vector3.Cross(Vector3.up, d.normalized) * Mathf.Min(2, length * .06f) * Mathf.Sin(a.x * .13f + b.z * .17f);
                    if (length > 12 && Line(a, mid) && Line(mid, b)) result.Add(mid);
                    result.Add(b);
                }
                for (int i = 1; i < result.Count; i++) if (!Line(result[i - 1], result[i])) return null;
                for (int i = 0; i < result.Count; i++) { var p = result[i]; p.y = _ground.Height(p); result[i] = p; }
                return result;
            }
        }

        internal static bool Build(TownPlan plan, GenerationInput input, MatrixWorld heights, float scale, List<Vector3> avoid, TownWorldRoads roads, StopToken stop, out string reason)
        {
            reason = "no native road within access range"; Vector3 road;
            if (!roads.Nearest(plan.WorldCenter, input.AccessLength, out road)) return false;
            var ground = new TownGround(plan, input.Buildings, heights, scale, avoid);
            float extent = plan.Radius + 16;
            float cell = Mathf.Max(1, extent * 2 / 1000);
            var grid = new Grid(ground, plan, roads, new Vector3(-extent, 0, -extent), new Vector3(extent, 0, extent), cell, input.PathWidth, false, stop);
            var exits = new Vector3[plan.LotCount];
            var nodes = new List<Vector3>(); var edges = new List<int>();
            for (int i = 0; i < plan.LotCount; i++)
            {
                var lot = plan.Lots[i]; BuildingSpec spec = null;
                foreach (var candidate in input.Buildings) if (candidate.Name == lot.Prefab) { spec = candidate; break; }
                if (!lot.HasEntry) { lot.Entry = spec.Entry; lot.HasEntry = true; }
                var entry = TownPlanner.Entrance(lot);
                if (input.Progress != null) input.Progress.Report(.2f + .25f * i / plan.LotCount, "Connecting entrance " + (i + 1) + "/" + plan.LotCount);
                var doorway = lot.IsParking ? new List<Vector3> { entry, entry + Quaternion.Euler(0, lot.Yaw, 0) * Vector3.back * 3 }
                    : EntranceRoute(plan, spec, i, ground, grid, stop);
                if (lot.IsParking && (!grid.Free(doorway[0]) || !grid.Line(doorway[0], doorway[1]))) doorway = null;
                if (doorway == null) { reason = "no baked entrance route at " + lot.Prefab + "/" + i
                    + "; " + grid.ClearanceFailure(entry); return false; }
                lot.EntryNode = Node(nodes, entry); lot.Street = lot.EntryNode;
                AddPath(nodes, edges, doorway); exits[i] = doorway[doorway.Count - 1];
            }
            var connected = new bool[plan.LotCount]; connected[0] = true; var rejected = new bool[plan.LotCount, plan.LotCount];
            for (int count = 1; count < plan.LotCount;)
            {
                int a = -1, b = -1; float best = float.PositiveInfinity;
                for (int i = 0; i < plan.LotCount; i++) if (connected[i]) for (int j = 0; j < plan.LotCount; j++) if (!connected[j] && !rejected[i, j])
                { float distance = (exits[i] - exits[j]).sqrMagnitude; if (distance < best) { best = distance; a = i; b = j; } }
                if (a < 0) { reason = "entrance path network is separated by building blockers"; return false; }
                var path = grid.Route(exits[a], exits[b]);
                if (path == null) { rejected[a, b] = true; continue; }
                AddPath(nodes, edges, path); connected[b] = true; count++;
            }
            List<Vector3> connector = null; bool graded = false;
            if (input.Progress != null) input.Progress.Report(.5f, "Connecting town to a world road");
            var connections = roads.Connections(plan.WorldCenter, input.AccessLength);
            for (int pass = 0; pass < (input.EnsureTown ? 2 : 1) && connector == null; pass++) foreach (var connection in connections)
            {
                var target = ground.Local(connection); target.y = 0;
                int nearest = 0; float nearestDistance = float.PositiveInfinity;
                for (int i = 0; i < exits.Length; i++) { float d = (exits[i] - target).sqrMagnitude; if (d < nearestDistance) { nearestDistance = d; nearest = i; } }
                var gate = target.normalized * (plan.Radius + 8); gate.y = 0;
                var mouth = grid.Route(exits[nearest], gate); if (mouth == null) continue;
                var min = Vector3.Min(gate, target) - new Vector3(128, 0, 128); var max = Vector3.Max(gate, target) + new Vector3(128, 0, 128);
                var access = new Grid(ground, plan, roads, min, max, 3, input.AccessWidth, true, stop, pass == 0);
                // Ensure mode uses grading before an expensive search over every steep terrain cell.
                if (pass == 0 && input.EnsureTown && !access.Line(gate, target)) continue;
                var path = access.Route(gate, target); if (path == null) continue;
                mouth.AddRange(path.GetRange(1, path.Count - 1));
                if (pass != 0 && !Grade(mouth, plan.Radius)) continue;
                connector = mouth; graded = pass != 0; break;
            }
            if (connector == null) { reason = "no obstacle-free native road connection within access range"; return false; }
            if (nodes.Count > 2048 || edges.Count > 8192 || connector.Count > 2048)
            { reason = "route graph exceeds saved town limits; retrying a simpler layout"; return false; }
            plan.StreetNodes = nodes.ToArray(); plan.StreetEdges = edges.ToArray(); plan.MainNodes = 0;
            plan.AccessPath = connector.ToArray(); plan.GradeAccess = graded; plan.PathWidth = input.PathWidth; plan.AccessWidth = input.AccessWidth; plan.PathsVersion = 1;
            reason = "entrance paths and native road connection"; return true;
        }
        private static bool Grade(List<Vector3> path, float radius)
        {
            int flat = 0;
            for (int i = 0; i < path.Count; i++) if (new Vector2(path[i].x, path[i].z).sqrMagnitude <= radius * radius) flat = i;
            if (flat == path.Count - 1) return false;
            var a = path[flat]; a.y = 0; var delta = path[flat + 1] - a; delta.y = 0;
            float dot = Vector3.Dot(a, delta), square = delta.sqrMagnitude;
            if (square > .0001f)
            {
                float t = (-dot + Mathf.Sqrt(Mathf.Max(0, dot * dot + square * (radius * radius - a.sqrMagnitude)))) / square;
                path.Insert(flat + 1, a + delta * Mathf.Clamp01(t)); flat++;
            }
            float length = 0; var lengths = new float[path.Count];
            for (int i = flat + 1; i < path.Count; i++)
            { var step = path[i] - path[i - 1]; step.y = 0; length += step.magnitude; lengths[i] = length; }
            float end = path[path.Count - 1].y;
            if (length < .01f || Mathf.Abs(end) > length * .25f) return false;
            for (int i = 0; i < path.Count; i++) { var p = path[i]; p.y = Mathf.Lerp(0, end, lengths[i] / length); path[i] = p; }
            return true;
        }
        private static List<Vector3> EntranceRoute(TownPlan plan, BuildingSpec spec, int door, TownGround ground, Grid grid, StopToken stop)
        {
            var map = spec.Bake;
            var lot = plan.Lots[door]; var rotation = Quaternion.Euler(0, lot.Yaw, 0);
            var entry = TownPlanner.Entrance(lot); int start = map.Index(lot.Entry);
            if (start < 0 || map.Why[start] != 0 || float.IsNaN(map.Floor[start])) return null;
            // FindEntry already proved this outward ray in the template. Validate it against the
            // placed neighbours first; search the full baked component only if this route changed.
            var outward = rotation * spec.Outward; var previous = entry;
            for (float distance = map.Cell * .5f; distance <= Mathf.Max(map.W, map.H) * map.Cell; distance += map.Cell)
            {
                if (stop != null && stop.stop) throw new OperationCanceledException();
                var p = entry + outward * distance; int cell = map.Index(lot.Entry + spec.Outward * distance);
                if (cell >= 0 && (map.Why[cell] != 0 || float.IsNaN(map.Floor[cell]))) break;
                p.y = cell < 0 ? 0 : lot.LocalPosition.y + map.Floor[cell];
                if (!ground.DoorLine(previous, p, door)) break;
                if (grid.Free(p) && (plan.LayoutVersion < 3 || grid.Line(p, new Vector3(p.x, 0, 0))) && ground.DoorLine(entry, p, door))
                    return new List<Vector3> { entry, p };
                previous = p;
            }
            var parent = new int[map.Floor.Length]; for (int i = 0; i < parent.Length; i++) parent[i] = -2;
            var queue = new Queue<int>(); parent[start] = -1; queue.Enqueue(start); int ops = 0;
            int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                if ((ops++ & 255) == 0 && stop != null && stop.stop) throw new OperationCanceledException();
                int a = queue.Dequeue(); var p = lot.LocalPosition + rotation * map.Point(a);
                if (grid.Free(p) && (plan.LayoutVersion < 3 || grid.Line(p, new Vector3(p.x, 0, 0))))
                {
                    var route = new List<Vector3>();
                    for (int c = a; c >= 0; c = parent[c]) route.Add(lot.LocalPosition + rotation * map.Point(c));
                    route.Add(entry); route.Reverse();
                    if (!ground.DoorLine(route[0], route[1], door)) continue;
                    // Keep turns/elevation changes; reduce long collinear runs without crossing a baked edge.
                    for (int c = route.Count - 2; c > 0; c--)
                        if (Vector3.Cross(route[c] - route[c - 1], route[c + 1] - route[c]).sqrMagnitude < .00001f
                            && ground.DoorLine(route[c - 1], route[c + 1], door)) route.RemoveAt(c);
                    return route;
                }
                for (int d = 0; d < 4; d++)
                {
                    int x = a % map.W + dx[d], z = a / map.W + dz[d];
                    if (x < 0 || z < 0 || x >= map.W || z >= map.H) continue;
                    int b = z * map.W + x;
                    if (parent[b] != -2 || map.Why[b] != 0 || !map.Walk(a, b)) continue;
                    var next = lot.LocalPosition + rotation * map.Point(b);
                    if (!ground.DoorLine(p, next, door)) continue;
                    parent[b] = a; queue.Enqueue(b);
                }
            }
            return null;
        }
        private static void AddPath(List<Vector3> nodes, List<int> edges, List<Vector3> points)
        {
            int previous = -1;
            foreach (var p in points)
            {
                int index = Node(nodes, p);
                if (previous >= 0 && previous != index) { edges.Add(previous); edges.Add(index); }
                previous = index;
            }
        }
        private static int Node(List<Vector3> nodes, Vector3 p)
        {
            for (int i = 0; i < nodes.Count; i++) if ((nodes[i] - p).sqrMagnitude < .01f) return i;
            nodes.Add(p); return nodes.Count - 1;
        }
    }
}
