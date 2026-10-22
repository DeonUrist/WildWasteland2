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
    }
}
