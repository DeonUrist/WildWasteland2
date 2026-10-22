using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.POI
{
    /// <summary>
    /// Cached cardinal gates and inner/outer contour vertices of a walled town (Floyd-Warshall over a few nodes), not a town-wide bake.
    /// Wave A (A6): split out of Towns/TownPerimeter.cs. One class, two instances per walled town: TownWalls.Walkers (NPC walkers,
    /// B08) and TownWalls.Vehicles (Patrols driving, B11 needs its own copy reading the outline through ITownsApi).
    /// </summary>
    internal sealed class TownVehicleGates : Core.ITownBoundary
    {
        private readonly Bounds _frame;
        private readonly TownOutline _outline;   // refs-ok: REFACTOR-REQUEST B08-6
        private readonly List<Bounds> _props;
        private readonly Bounds[] _buildings;
        private readonly Quaternion[] _inverse;
        private readonly Vector3[] _nodes;
        private readonly float[,] _cost;
        private readonly int[,] _next;
        private bool _ready;
        /// <summary>All-pairs routes between the gate/contour nodes are built.</summary>
        internal bool Ready { get { return _ready; } }
        /// <summary>Preparation progress 0..1.</summary>
        internal float Progress { get { return Mathf.Clamp01(_row / (2f * _nodes.Length)); } }
        private readonly float _margin;
        private readonly bool _walking;
        private readonly bool[] _from, _to;
        private int _row;
        private long _fromKey = long.MinValue, _toKey = long.MinValue;
        private float _fromAt, _toAt;
        /// <summary>2 m cell key of a point (visibility memo).</summary>
        private static long Bucket(Vector3 p) { return ((long)Mathf.FloorToInt(p.x / 2f) << 32) ^ ((long)Mathf.FloorToInt(p.z / 2f) & 0xFFFFFFFFL); }
        /// <summary>Gate and contour nodes of a walled town (walkers: inside/outside gate points and outline offsets; vehicles: the four frame corners).</summary>
        internal TownVehicleGates(TownPlan plan, bool walking = false, List<Bounds> props = null)   // refs-ok: REFACTOR-REQUEST B08-6
        {
            _frame = TownPerimeter.Frame(plan); _walking = walking; _margin = walking ? .5f : 3f;   // refs-ok: REFACTOR-REQUEST B08-6
            _outline = TownPerimeter.Outline(plan); _props = props ?? new List<Bounds>();   // refs-ok: REFACTOR-REQUEST B08-6
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
            { _nodes[i * 2] = TownPerimeter.Gate(_frame, i) - TownPerimeter.Normal(i) * 7;   // refs-ok: REFACTOR-REQUEST B08-6
                _nodes[i * 2 + 1] = TownPerimeter.Gate(_frame, i) + TownPerimeter.Normal(i) * 16; }   // refs-ok: REFACTOR-REQUEST B08-6
            for (int i = 0; i < _outline.Vertices.Length; i++)
            { _nodes[8 + i * 2] = _outline.Offset(i, -3.2f); _nodes[9 + i * 2] = _outline.Offset(i, 10); }
            if (!walking)
            { _nodes = new Vector3[4]; for (int i = 0; i < 4; i++) _nodes[i] = TownPerimeter.Corner(_frame, i, 20f); }   // refs-ok: REFACTOR-REQUEST B08-6
        }
        /// <summary>True when a straight segment crosses no wall (with margin), prop and, for vehicles, no building or frame.</summary>
        private bool Clear(Vector3 a, Vector3 b)
        {
            if (!_walking && CrossesFrame(a, b)) return false;
            if (_outline.Crosses(a, b, _margin) || Props(a, b)) return false;
            if (!_walking) for (int i = 0; i < _buildings.Length; i++)
            {
                var box = _buildings[i]; if (box.size.y == 0) continue;
                var from = _inverse[i] * (a - box.center); var to = _inverse[i] * (b - box.center); float lo = 0, hi = 1;
                if (TownPerimeter.Clip(from.x, to.x - from.x, box.extents.x + _margin, ref lo, ref hi)   // refs-ok: REFACTOR-REQUEST B08-6
                    && TownPerimeter.Clip(from.z, to.z - from.z, box.extents.z + _margin, ref lo, ref hi)) return false;   // refs-ok: REFACTOR-REQUEST B08-6
            }
            return true;
        }
        /// <summary>True when a segment enters the wall frame (+6 m).</summary>
        private bool CrossesFrame(Vector3 a, Vector3 b)
        {
            float lo = 0, hi = 1;
            return TownPerimeter.Clip(a.x - _frame.center.x, b.x - a.x, _frame.extents.x + 6f, ref lo, ref hi)   // refs-ok: REFACTOR-REQUEST B08-6
                && TownPerimeter.Clip(a.z - _frame.center.z, b.z - a.z, _frame.extents.z + 6f, ref lo, ref hi);   // refs-ok: REFACTOR-REQUEST B08-6
        }
        /// <summary>True when a segment crosses a dressing prop box.</summary>
        private bool Props(Vector3 a, Vector3 b)
        {
            foreach (var box in _props)
            {
                float lo = 0, hi = 1;
                if (TownPerimeter.Clip(a.x - box.center.x, b.x - a.x, box.extents.x + _margin, ref lo, ref hi)   // refs-ok: REFACTOR-REQUEST B08-6
                    && TownPerimeter.Clip(a.z - box.center.z, b.z - a.z, box.extents.z + _margin, ref lo, ref hi)) return true;   // refs-ok: REFACTOR-REQUEST B08-6
            }
            return false;
        }
        /// <summary>One row of visibility, then one Floyd-Warshall pass per call; false when ready.</summary>
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
        /// <summary>Next node from a toward b when the straight way is blocked; hold = wait (not ready / no clear node).</summary>
        public bool Waypoint(Vector3 a, Vector3 b, out Vector3 point, out bool hold)
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
