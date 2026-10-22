using System;
using System.Collections.Generic;
using Den.Tools;
using Den.Tools.Matrices;
using Den.Tools.Splines;
using MapMagic.Nodes.ObjectsGenerators;
using MapMagic.Products;
using UnityEngine;

namespace WildWasteland.Modules.POI
{
    internal sealed class TownWorldRoads
    {
        private readonly List<Vector3> _segments = new List<Vector3>();
        private readonly float _minX, _minZ, _maxX, _maxZ;
        /// <summary>Initializes town world roads with the supplied owning state and immutable inputs.</summary>
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
            if (WorldGeneration.Present) foreach (var line in WorldGeneration.RoadLines)
                for (int i = 1; i < line.Length; i++)
                {
                    var a = line[i - 1]; var b = line[i]; a.y = b.y = 0;
                    if (Mathf.Max(a.x, b.x) < _minX || Mathf.Min(a.x, b.x) > _maxX
                        || Mathf.Max(a.z, b.z) < _minZ || Mathf.Min(a.z, b.z) > _maxZ) continue;
                    int samples = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / 16), 1, 512);
                    for (int s = 0; s < samples; s++)
                    { _segments.Add(Vector3.Lerp(a, b, s / (float)samples)); _segments.Add(Vector3.Lerp(a, b, (s + 1) / (float)samples)); }
                }
            if (!WorldGeneration.Present) return;
            // Vistas publishes roads from workers. Stable ordering keeps site selection reproducible.
            var segments = new List<Vector3[]>();
            for (int i = 0; i < _segments.Count; i += 2) segments.Add(new[] { _segments[i], _segments[i + 1] });
            segments.Sort((a, b) => { int c = a[0].x.CompareTo(b[0].x); if (c == 0) c = a[0].z.CompareTo(b[0].z);
                if (c == 0) c = a[1].x.CompareTo(b[1].x); return c == 0 ? a[1].z.CompareTo(b[1].z) : c; });
            _segments.Clear(); foreach (var segment in segments) _segments.AddRange(segment);
        }
        /// <summary>Tests whether a point lies inside the captured terrain extent.</summary>
        internal bool Inside(Vector3 p) { return p.x >= _minX && p.x <= _maxX && p.z >= _minZ && p.z <= _maxZ; }
        /// <summary>Finds the nearest retained road sample within the requested distance.</summary>
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
        /// <summary>Copies detached metadata and layout JSON for live authoring sites.</summary>
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
        /// <summary>Initializes town ground with the supplied owning state and immutable inputs.</summary>
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
        /// <summary>Converts a stable world position to the town plan frame.</summary>
        internal Vector3 Local(Vector3 world) { return Quaternion.Euler(0, -_plan.Yaw, 0) * (world - _plan.WorldCenter); }
        /// <summary>Tests whether the supplied geometry is reserved by the town plan.</summary>
        internal bool Protected(Vector3 world) { return Blocked(Local(world), -1); }
        /// <summary>Tests the geometry or name against the requested containment rule.</summary>
        private bool Contains(int i, Vector3 p)
        { var b = _bounds[i]; return _maps[i] != null && p.x >= b.x && p.z >= b.y && p.x <= b.z && p.z <= b.w; }
        /// <summary>Tests whether a candidate path point overlaps planned solid geometry.</summary>
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
        /// <summary>Tests whether the segment crosses a solid building or perimeter wall.</summary>
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
        /// <summary>Tests a path segment while preserving the designated entrance exception.</summary>
        internal bool DoorLine(Vector3 a, Vector3 b, int door)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) * 5)); var previous = a;
            for (int i = 0; i <= steps; i++)
            { var p = Vector3.Lerp(a, b, i / (float)steps); if (Blocked(p, door) || Wall(previous, p)) return false; previous = p; }
            return true;
        }
    }
}
