using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Towns
{
    // World-cardinal numeric footprint; immutable building transforms remain the source of truth.
    internal static class TownPerimeter
    {
        internal const float GateHalf = 7.5f, Clearance = 10f;
        internal static TownOutline Outline(TownPlan plan)
        { if (plan.Outline == null) plan.Outline = new TownOutline(plan); return plan.Outline; }
        internal static bool Allegiance(TownPlan plan)
        { return plan.Owned && (plan.Faction == TownFaction.Coyotes || plan.Faction == TownFaction.Scrapyard); }
        internal static Bounds Buildings(TownPlan plan)
        {
            var bounds = new Bounds(); bool any = false;
            foreach (var lot in plan.Lots) if (!lot.IsParking)
                for (int c = 0; c < 4; c++)
                {
                    var p = Quaternion.Euler(0, plan.Yaw, 0) * (lot.LocalPosition + Quaternion.Euler(0, lot.Yaw, 0)
                        * (lot.BoundsCenter + new Vector3((c & 1) == 0 ? -lot.HalfX : lot.HalfX, 0, (c & 2) == 0 ? -lot.HalfZ : lot.HalfZ)));
                    p.y = 0;
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
                }
            return bounds;
        }
        internal static Bounds Frame(TownPlan plan)
        {
            var b = Buildings(plan); b.Expand(new Vector3(Clearance * 2, 0, Clearance * 2));
            var size = b.size; size.x = Mathf.Max(48, size.x); size.z = Mathf.Max(48, size.z); b.size = size; return b;
        }
        internal static Vector3 Normal(int side)
        { return side == 0 ? Vector3.right : side == 1 ? Vector3.forward : side == 2 ? Vector3.left : Vector3.back; }
        internal static Vector3 Gate(Bounds frame, int side)
        {
            var n = Normal(side);
            return frame.center + new Vector3(n.x * frame.extents.x, 0, n.z * frame.extents.z);
        }
        internal static Vector3 Corner(Bounds b, int corner, float offset)
        { return b.center + new Vector3(((corner == 0 || corner == 3) ? 1 : -1) * (b.extents.x + offset), 0,
            (corner < 2 ? 1 : -1) * (b.extents.z + offset)); }
        internal static bool BuildingLine(TownPlan plan, Vector3 a, Vector3 b, float margin)
        {
            var inverse = Quaternion.Euler(0, -plan.Yaw, 0);
            a = inverse * a; b = inverse * b;
            foreach (var lot in plan.Lots) if (!lot.IsParking && !lot.Skipped)
            {
                var rot = Quaternion.Euler(0, -lot.Yaw, 0);
                var from = rot * (a - lot.LocalPosition) - lot.BoundsCenter;
                var to = rot * (b - lot.LocalPosition) - lot.BoundsCenter;
                float lo = 0, hi = 1;
                if (Clip(from.x, to.x - from.x, lot.HalfX + margin, ref lo, ref hi)
                    && Clip(from.z, to.z - from.z, lot.HalfZ + margin, ref lo, ref hi)) return true;
            }
            return false;
        }
        internal static bool Clip(float p, float d, float extent, ref float lo, ref float hi)
        {
            if (Mathf.Abs(d) < .0001f) return Mathf.Abs(p) <= extent;
            float a = (-extent - p) / d, b = (extent - p) / d;
            lo = Mathf.Max(lo, Mathf.Min(a, b)); hi = Mathf.Min(hi, Mathf.Max(a, b)); return lo <= hi;
        }
        internal static void WalkNodes(TownPlan plan, List<Vector3> nodes, List<int> outside)
        {
            var frame = Frame(plan); var inv = Quaternion.Euler(0, -plan.Yaw, 0);
            for (int side = 0; side < 4; side++)
                foreach (float offset in new[] { -5f, 16f })
                { outside.Add(nodes.Count); nodes.Add(inv * (Gate(frame, side) + Normal(side) * offset)); }
            var outline = Outline(plan);
            for (int c = 0; c < outline.Vertices.Length; c++) foreach (float offset in new[] { -3.2f, 10f })
            { outside.Add(nodes.Count); nodes.Add(inv * outline.Offset(c, offset)); }
        }
    }

    // Cached cardinal gates and inner/outer contour vertices, not a town-wide bake.
    internal sealed class TownVehicleGates
    {
        private readonly Bounds _frame;
        private readonly TownOutline _outline;
        private readonly List<Bounds> _props;
        private readonly Bounds[] _buildings;
        private readonly Quaternion[] _inverse;
        private readonly Vector3[] _nodes;
        private readonly float[,] _cost;
        private readonly int[,] _next;
        private bool _ready;
        internal bool Ready { get { return _ready; } }
        internal float Progress { get { return Mathf.Clamp01(_row / (2f * _nodes.Length)); } }
        private readonly float _margin;
        private readonly bool _walking;
        private readonly bool[] _from, _to;
        private int _row;
        private long _fromKey = long.MinValue, _toKey = long.MinValue;
        private float _fromAt, _toAt;
        private static long Bucket(Vector3 p) { return ((long)Mathf.FloorToInt(p.x / 2f) << 32) ^ ((long)Mathf.FloorToInt(p.z / 2f) & 0xFFFFFFFFL); }
        internal TownVehicleGates(TownPlan plan, bool walking = false, List<Bounds> props = null)
        {
            _frame = TownPerimeter.Frame(plan); _walking = walking; _margin = walking ? .5f : 3f;
            _outline = TownPerimeter.Outline(plan); _props = props ?? new List<Bounds>();
            int n = 8 + _outline.Vertices.Length * 2; _nodes = new Vector3[n]; _cost = new float[n, n]; _next = new int[n, n]; _from = new bool[n]; _to = new bool[n];
            _buildings = new Bounds[plan.LotCount]; _inverse = new Quaternion[plan.LotCount];
            var rotation = Quaternion.Euler(0, plan.Yaw, 0);
            for (int i = 0; i < plan.LotCount; i++)
            {
                var lot = plan.Lots[i]; if (lot.IsParking || lot.Skipped) continue;
                _inverse[i] = Quaternion.Euler(0, -plan.Yaw - lot.Yaw, 0);
                _buildings[i] = new Bounds(rotation * (lot.LocalPosition + Quaternion.Euler(0, lot.Yaw, 0) * lot.BoundsCenter), new Vector3(lot.HalfX * 2, 1, lot.HalfZ * 2));
            }
            for (int i = 0; i < 4; i++)
            { _nodes[i * 2] = TownPerimeter.Gate(_frame, i) - TownPerimeter.Normal(i) * 7;
                _nodes[i * 2 + 1] = TownPerimeter.Gate(_frame, i) + TownPerimeter.Normal(i) * 16; }
            for (int i = 0; i < _outline.Vertices.Length; i++)
            { _nodes[8 + i * 2] = _outline.Offset(i, -3.2f); _nodes[9 + i * 2] = _outline.Offset(i, 10); }
            if (!walking)
            { _nodes = new Vector3[4]; for (int i = 0; i < 4; i++) _nodes[i] = TownPerimeter.Corner(_frame, i, 20f); }
        }
        private bool Clear(Vector3 a, Vector3 b)
        {
            if (!_walking && CrossesFrame(a, b)) return false;
            if (_outline.Crosses(a, b, _margin) || Props(a, b)) return false;
            if (!_walking) for (int i = 0; i < _buildings.Length; i++)
            {
                var box = _buildings[i]; if (box.size.y == 0) continue;
                var from = _inverse[i] * (a - box.center); var to = _inverse[i] * (b - box.center); float lo = 0, hi = 1;
                if (TownPerimeter.Clip(from.x, to.x - from.x, box.extents.x + _margin, ref lo, ref hi)
                    && TownPerimeter.Clip(from.z, to.z - from.z, box.extents.z + _margin, ref lo, ref hi)) return false;
            }
            return true;
        }
        private bool CrossesFrame(Vector3 a, Vector3 b)
        {
            float lo = 0, hi = 1;
            return TownPerimeter.Clip(a.x - _frame.center.x, b.x - a.x, _frame.extents.x + 6f, ref lo, ref hi)
                && TownPerimeter.Clip(a.z - _frame.center.z, b.z - a.z, _frame.extents.z + 6f, ref lo, ref hi);
        }
        private bool Props(Vector3 a, Vector3 b)
        {
            foreach (var box in _props)
            {
                float lo = 0, hi = 1;
                if (TownPerimeter.Clip(a.x - box.center.x, b.x - a.x, box.extents.x + _margin, ref lo, ref hi)
                    && TownPerimeter.Clip(a.z - box.center.z, b.z - a.z, box.extents.z + _margin, ref lo, ref hi)) return true;
            }
            return false;
        }
        internal bool Step()
        {
            if (_ready) return false;
            if (_row < _nodes.Length)
            {
                for (int j = 0; j < _nodes.Length; j++)
                { bool clear = _row == j || Clear(_nodes[_row], _nodes[j]); _cost[_row, j] = clear ? Vector3.Distance(_nodes[_row], _nodes[j]) : float.PositiveInfinity;
                    _next[_row, j] = clear ? j : -1; }
            }
            else
            {
                int k = _row - _nodes.Length;
                for (int i = 0; i < _nodes.Length; i++) for (int j = 0; j < _nodes.Length; j++)
                    if (_cost[i, k] + _cost[k, j] < _cost[i, j])
                    { _cost[i, j] = _cost[i, k] + _cost[k, j]; _next[i, j] = _next[i, k]; }
            }
            _ready = ++_row >= _nodes.Length * 2; return true;
        }
        internal bool Waypoint(Vector3 a, Vector3 b, out Vector3 point, out bool hold)
        {
            point = b; hold = false;
            if (Clear(a, b)) return false;
            if (!_ready) { point = a; hold = true; return true; }
            float best = float.PositiveInfinity; int start = -1, end = -1;
            // Node visibility is memoised per 2 m cell for half a second: every pilot decision asked for all 2n segments.
            long ka = Bucket(a), kb = Bucket(b); float now = Time.unscaledTime;
            if (ka != _fromKey || now - _fromAt > .5f) { for (int i = 0; i < _nodes.Length; i++) _from[i] = Clear(a, _nodes[i]); _fromKey = ka; _fromAt = now; }
            if (kb != _toKey || now - _toAt > .5f) { for (int i = 0; i < _nodes.Length; i++) _to[i] = Clear(_nodes[i], b); _toKey = kb; _toAt = now; }
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (!_from[i]) continue;
                for (int j = 0; j < _nodes.Length; j++)
                {
                    float value = Vector3.Distance(a, _nodes[i]) + _cost[i, j] + Vector3.Distance(_nodes[j], b);
                    if (value >= best || !_to[j]) continue;
                    start = i; end = j; best = value;
                }
            }
            if (start >= 0)
            { if ((a - _nodes[start]).sqrMagnitude < 9 && start != end) start = _next[start, end]; point = _nodes[start]; return true; }
            // Dense building rows may have no vehicle clearance. Wait outside a gate rather than ram.
            for (int i = _walking ? 1 : 0; i < (_walking ? 8 : _nodes.Length); i += _walking ? 2 : 1)
                if (_from[i] && (a - _nodes[i]).sqrMagnitude < best)
                { best = (a - _nodes[i]).sqrMagnitude; point = _nodes[i]; }
            if (float.IsPositiveInfinity(best)) point = a;
            hold = true; return true;
        }
    }
}
