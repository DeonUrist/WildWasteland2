using UnityEngine;
using System.Collections.Generic;

namespace WildWasteland.Modules.Towns
{
    // Numeric entrance selection outside the town; the street graph is only used inside its contour.
    internal sealed class TownApproach
    {
        private readonly TownOutline _outline;
        private readonly Vector3[] _gates = new Vector3[4];
        private readonly Vector3[] _corners = new Vector3[4];
        private readonly float[,] _cost = new float[4, 4];
        private readonly int[,] _direction = new int[4, 4];
        private readonly bool[] _from = new bool[4], _to = new bool[4];
        private sealed class Detour { internal GameObject Owner; internal Vector3 Goal; internal readonly Vector3[] Points = new Vector3[4]; internal int Count, Index; }
        private readonly Dictionary<int, Detour> _detours = new Dictionary<int, Detour>();
        private readonly List<int> _dead = new List<int>();
        internal TownApproach(TownPlan plan)
        {
            _outline = TownPerimeter.Outline(plan); var frame = TownPerimeter.Frame(plan);
            for (int side = 0; side < 4; side++) _gates[side] = TownPerimeter.Gate(frame, side);
            for (int i = 0; i < 4; i++) _corners[i] = TownPerimeter.Corner(frame, i, 12f);
            float perimeter = 0f; for (int i = 0; i < 4; i++) perimeter += Vector3.Distance(_corners[i], _corners[(i + 1) % 4]);
            for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++)
            {
                float forward = 0f; for (int n = a; n != b; n = (n + 1) % 4) forward += Vector3.Distance(_corners[n], _corners[(n + 1) % 4]);
                _cost[a, b] = Mathf.Min(forward, perimeter - forward); _direction[a, b] = forward <= perimeter - forward ? 1 : -1;
            }
        }
        internal bool Inside(Vector3 p) { return _outline.Contains(p, 0f); }
        internal bool Around(GameObject owner, Vector3 position, Vector3 goal, out Vector3 point)
        {
            position.y = goal.y = 0f; point = goal; int id = owner == null ? 0 : owner.GetInstanceID();
            if (!_outline.Crosses(position, goal, .6f)) { _detours.Remove(id); return false; }
            Detour route;
            if (_detours.TryGetValue(id, out route) && route.Owner == owner && (route.Goal - goal).sqrMagnitude < 4f)
            {
                while (route.Index < route.Count && (position - route.Points[route.Index]).sqrMagnitude < 4f) route.Index++;
                if (route.Index < route.Count && !_outline.Crosses(position, route.Points[route.Index], .6f)) { point = route.Points[route.Index]; return true; }
            }
            for (int i = 0; i < 4; i++) { _from[i] = !_outline.Crosses(position, _corners[i], .6f); _to[i] = !_outline.Crosses(_corners[i], goal, .6f); }
            float best = float.PositiveInfinity; int start = -1, end = -1;
            for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++)
            {
                if (!_from[a] || !_to[b]) continue;
                float cost = Vector3.Distance(position, _corners[a]) + _cost[a, b] + Vector3.Distance(_corners[b], goal);
                if (cost < best) { best = cost; start = a; end = b; }
            }
            if (start < 0) { _detours.Remove(id); return false; }
            if (_detours.Count >= 64)
            {
                _dead.Clear(); foreach (var kv in _detours) if (kv.Value.Owner == null) _dead.Add(kv.Key);
                foreach (int key in _dead) _detours.Remove(key);
            }
            if (_detours.Count >= 1024) _detours.Clear();
            route = new Detour { Owner = owner, Goal = goal };
            for (int n = start; ; n = (n + _direction[start, end] + 4) % 4) { route.Points[route.Count++] = _corners[n]; if (n == end) break; }
            if (owner != null) _detours[id] = route;
            point = route.Points[0]; return true;
        }
        internal Vector3 Entrance(Vector3 position)
        {
            position.y = 0f; int best = -1; float cost = float.PositiveInfinity;
            // Prefer a gate on a facing side: its approach stays outside the wall's bounding frame.
            for (int pass = 0; pass < 2 && best < 0; pass++) for (int side = 0; side < 4; side++)
            {
                var normal = TownPerimeter.Normal(side);
                if (pass == 0 && Vector3.Dot(position - _gates[side], normal) < -1f) continue;
                float distance = (position - _gates[side] - normal * 16f).sqrMagnitude;
                if (distance < cost) { cost = distance; best = side; }
            }
            var n = TownPerimeter.Normal(best); var delta = position - _gates[best];
            float outward = Vector3.Dot(delta, n);
            var lateral = delta - n * outward;
            // Line up outside first, then cross the opening to the existing inner gate node.
            return _gates[best] + n * (outward <= 18f && lateral.sqrMagnitude < 36f ? -5f : 16f);
        }
    }
}
