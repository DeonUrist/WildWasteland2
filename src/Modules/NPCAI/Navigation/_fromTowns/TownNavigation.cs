using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    internal sealed class TownNavigation
    {
        // Same static-world layers as the existing baker; triggers, actors, cars and loose items are excluded.
        internal const int Mask = ~((1 << 1) | (1 << 2) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 7) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 12) | (1 << 13) | (1 << 15) | (1 << 17) | (1 << 19) | (1 << 22));
        private readonly TownSite _site;
        private readonly TownOutline _outline;
        private readonly List<Vector3> _cells = new List<Vector3>(256);
        private readonly Vector3[] _nodes; // map-anchor-relative positions, so origin jumps need no cached correction
        private readonly float[,] _distance;
        private readonly int[,] _hop;
        private struct Arc { internal int To; internal float Cost; }
        private readonly List<Arc>[] _links;
        private int _lot, _floyd = -1, _cellIndex = -1;
        private int _edgeCheck;
        private float _retry;
        private float _portalCost;
        private Vector3 _probeAnchor, _portal;
        private bool _streetsChecked, _synced;
        private struct Memo { internal bool Result; internal Vector3 Waypoint, Anchor; internal float At; }
        private readonly Dictionary<long, Memo> _memo = new Dictionary<long, Memo>();
        internal int Casts, Accepted;   // street edges rechecked with physics / accepted from the plan's numeric validation
        internal bool Ready { get; private set; }
        internal bool Failed { get { return false; } }
        internal string FailureReason { get; private set; }
        internal float Progress { get { return Ready ? 1 : !_streetsChecked ? _edgeCheck / (float)Mathf.Max(1, _site.Plan.StreetEdges.Length) * .3f
            : _floyd < 0 ? .3f + .3f * _lot / _site.Plan.LotCount : .6f + .4f * _floyd / _nodes.Length; } }
        internal TownNavigation(TownSite site)
        {
            _site = site; _outline = TownPerimeter.Outline(site.Plan); int n = site.Plan.StreetNodes.Length + site.Plan.LotCount;
            _nodes = new Vector3[n]; _distance = new float[n, n]; _hop = new int[n, n];
            _links = new List<Arc>[n]; for (int i = 0; i < n; i++) _links[i] = new List<Arc>();
            for (int a = 0; a < n; a++) for (int b = 0; b < n; b++)
            { _distance[a, b] = a == b ? 0 : float.PositiveInfinity; _hop[a, b] = -1; }
            for (int i = 0; i < site.Plan.StreetNodes.Length; i++) _nodes[i] = site.Plan.World(site.Plan.StreetNodes[i]);
        }
        private Vector3 Scene(int i) { return _site.Anchor.TransformPoint(_nodes[i]); }
        internal bool Step()
        {
            if (Ready || Services.Npc == null || Time.unscaledTime < _retry) return false;
            int streets = _site.Plan.StreetNodes.Length;
            if (!_streetsChecked)
            {
                var edges = _site.Plan.StreetEdges;
                if (_edgeCheck < edges.Length)
                {
                    int a = edges[_edgeCheck++], b = edges[_edgeCheck++];
                    if (!_synced) { Physics.SyncTransforms(); _synced = true; }
                    // The plan validated every street link against the prefab templates; physics only rechecks links that
                    // run near the perimeter walls or the dressing props, which the templates do not contain.
                    if (NeedsCast(a, b)) { Casts++; if (!Clear(Scene(a), Scene(b))) return true; } // Keep the other usable links instead of restarting the town.
                    else Accepted++;
                    Link(a, b);
                    return true;
                }
                _streetsChecked = true;
            }
            if (_lot < _site.Plan.LotCount)
            {
                if (_site.Plan.Lots[_lot].IsParking)
                { int entry = _site.Plan.Lots[_lot].EntryNode; _nodes[streets + _lot] = _nodes[entry]; Link(entry, streets + _lot); _lot++; return true; }
                var building = _site.Buildings[_lot]; if (building == null || _site.Plan.Lots[_lot].Skipped) { _lot++; _cellIndex = -1; return true; }
                if (_cellIndex < 0)
                {
                    bool known, baked;
                    int count = TownOverlap.PortalCandidates(building.transform, Scene(_site.Plan.Lots[_lot].EntryNode), _cells, out known, out baked);
                    if (!known) TownBakeBridge.Discover(building);
                    if (!baked) { _retry = Time.unscaledTime + .5f; return false; }
                    if (count == 0) Log.Warn("[Towns] no walkable cells beside measured entrance for " + building.name);
                    _probeAnchor = _site.Anchor.position; _portalCost = float.PositiveInfinity; _cellIndex = 0;
                }
                var lot = _site.Plan.Lots[_lot];
                int street = lot.HasEntry ? lot.EntryNode : lot.Street;
                Vector3 road = Scene(street);
                // At most one entrance candidate/capsule cast per scheduler step.
                if (_cellIndex < _cells.Count)
                {
                    var point = _cells[_cellIndex++] + _site.Anchor.position - _probeAnchor;
                    float cost = (point - road).sqrMagnitude;
                    if (cost < _portalCost && Clear(point, road))
                    { _portal = _site.Anchor.InverseTransformPoint(point); _portalCost = cost; _cellIndex = _cells.Count; } // Candidates are ordered nearest the entrance.
                    return true;
                }
                if (float.IsPositiveInfinity(_portalCost))
                {
                    _lot++; _cellIndex = -1; return true; // Its native building map still owns its exit.
                }
                int portal = streets + _lot;
                _nodes[portal] = _portal; Link(street, portal); _lot++; _cellIndex = -1;
                return true;
            }
            if (_floyd < 0) _floyd = 0;
            if (_floyd < _nodes.Length)
            {
                BuildRoutes(_floyd++);
                return true;
            }
            Ready = true;
            Log.Always("[Towns] connected " + _site.Plan.Key + ": " + _site.Plan.LotCount + " separate baked buildings, " + _nodes.Length + " validated street/entrance nodes.");
            return false;
        }
        private bool NeedsCast(int a, int b)
        {
            if (_site.GroundTerrain != null) return true;
            var center = _site.Plan.WorldCenter; var pa = _nodes[a] - center; var pb = _nodes[b] - center;
            if (_site.Walls != null && _outline.Crosses(pa, pb, 2.5f)) return true;
            foreach (var box in _site.DebrisBlockers)
            {
                float lo = 0, hi = 1;
                if (TownPerimeter.Clip(_nodes[a].x - box.center.x, _nodes[b].x - _nodes[a].x, box.extents.x + 3, ref lo, ref hi)
                    && TownPerimeter.Clip(_nodes[a].z - box.center.z, _nodes[b].z - _nodes[a].z, box.extents.z + 3, ref lo, ref hi)) return true;
            }
            return false;
        }
        private void Link(int a, int b)
        {
            float cost = Vector3.Distance(_nodes[a], _nodes[b]);
            _links[a].Add(new Arc { To = b, Cost = cost }); _links[b].Add(new Arc { To = a, Cost = cost });
        }
        private void BuildRoutes(int source)
        {
            var closed = new bool[_nodes.Length]; var open = new TownPaths.Heap(); open.Push(source, 0);
            while (open.Count > 0)
            {
                int a = open.Pop(); if (closed[a]) continue; closed[a] = true;
                foreach (var edge in _links[a])
                {
                    float distance = _distance[source, a] + edge.Cost;
                    if (distance >= _distance[source, edge.To]) continue;
                    _distance[source, edge.To] = distance; _hop[source, edge.To] = a == source ? edge.To : _hop[source, a];
                    open.Push(edge.To, distance);
                }
            }
        }
        internal bool Waypoint(Vector3 position, Vector3 goal, out Vector3 waypoint)
        {
            waypoint = goal;
            Vector3 center = _site.Anchor.TransformPoint(_site.Plan.WorldCenter);
            float radius = _site.Plan.Radius + _site.Plan.Feather + 30;
            if (Horizontal(position, center) > radius * radius) return false;
            // One answer per 1.5 m cell pair for 0.4 s: NPC thinks repeat the same question several times a second.
            long key = ((long)Mathf.FloorToInt(position.x / 1.5f) & 0xFFFFF) << 44 | ((long)Mathf.FloorToInt(position.z / 1.5f) & 0xFFFFF) << 24
                | ((long)Mathf.FloorToInt(goal.x / 1.5f) & 0xFFF) << 12 | ((long)Mathf.FloorToInt(goal.z / 1.5f) & 0xFFF);
            Memo memo; float now = Time.unscaledTime; var anchor = _site.Anchor.position;
            if (_memo.TryGetValue(key, out memo) && now - memo.At < .4f && memo.Anchor == anchor) { waypoint = memo.Waypoint; return memo.Result; }
            bool result = Compute(position, goal, out waypoint);
            if (_memo.Count > 1024) _memo.Clear();
            _memo[key] = new Memo { Result = result, Waypoint = waypoint, Anchor = anchor, At = now };
            return result;
        }
        private bool Compute(Vector3 position, Vector3 goal, out Vector3 waypoint)
        {
            waypoint = goal;
            int startLot = Lot(TownBakeBridge.Root(position)), endLot = Lot(TownBakeBridge.Root(goal));
            if (startLot >= 0 && startLot == endLot) return false; // existing local map handles the whole leg
            if (startLot < 0 && endLot < 0 && Clear(position, goal)) return false;
            int streets = _site.Plan.StreetNodes.Length;
            int end = endLot >= 0 ? streets + endLot : Nearest(goal);
            if (endLot >= 0 && Horizontal(position, Scene(end)) < 2.25f) return false; // now on destination map
            if (startLot >= 0)
            {
                var start = _site.Plan.Lots[startLot];
                int portal = streets + startLot, street = start.HasEntry ? start.EntryNode : start.Street;
                Vector3 a = Scene(portal), direction = Scene(street) - a;
                // Until reaching the entrance, let that building's native map route out. After passing
                // it, keep advancing along the entrance link instead of returning to its nearest node.
                if (Vector3.Dot(position - a, direction.normalized) < -.25f)
                { waypoint = a; return true; }
            }
            if (Horizontal(position, Scene(end)) < 2.25f) return false;
            // Project onto a graph EDGE, not the nearest vertex. Nearest-vertex steering reverses
            // direction as soon as an actor moves away from the first waypoint.
            int next = -1; float bestOffset = float.PositiveInfinity, bestRoute = float.PositiveInfinity;
            int edgeCount = _site.Plan.StreetEdges.Length / 2 + _site.Plan.LotCount;
            for (int i = 0; i < edgeCount; i++)
            {
                int a, b;
                if (i < _site.Plan.StreetEdges.Length / 2) { a = _site.Plan.StreetEdges[i * 2]; b = _site.Plan.StreetEdges[i * 2 + 1]; }
                else { int lot = i - _site.Plan.StreetEdges.Length / 2; a = streets + lot; b = _site.Plan.Lots[lot].HasEntry ? _site.Plan.Lots[lot].EntryNode : _site.Plan.Lots[lot].Street; }
                var pa = Scene(a); var pb = Scene(b); var delta = pb - pa; delta.y = 0;
                var flat = position - pa; flat.y = 0;
                float t = Mathf.Clamp01(Vector3.Dot(flat, delta) / Mathf.Max(.001f, delta.sqrMagnitude));
                Vector3 projection = pa + delta * t;
                float offset = Horizontal(position, projection);
                if (_site.Walls != null && _outline.Crosses(_site.Anchor.InverseTransformPoint(position) - _site.Plan.WorldCenter,
                    _site.Anchor.InverseTransformPoint(projection) - _site.Plan.WorldCenter, .4f)) continue;
                float ca = t * delta.magnitude + _distance[a, end], cb = (1 - t) * delta.magnitude + _distance[b, end];
                float route = Mathf.Min(ca, cb);
                if (float.IsPositiveInfinity(route) || offset > bestOffset + .1f || (Mathf.Abs(offset - bestOffset) <= .1f && route >= bestRoute)) continue;
                bestOffset = offset; bestRoute = route; next = ca <= cb ? a : b;
            }
            if (next >= 0 && Horizontal(position, Scene(next)) < 2.25f) next = _hop[next, end];
            if (next < 0 || !Clear(position, Scene(next))) return false;
            waypoint = Scene(next);
            return Horizontal(waypoint, goal) > .25f;
        }
        private int Lot(Transform root)
        { if (root == null) return -1; for (int i = 0; i < _site.Buildings.Length; i++) if (_site.Buildings[i] != null && _site.Buildings[i].transform == root) return i; return -1; }
        private int Nearest(Vector3 point)
        {
            int best = 0; float cost = float.PositiveInfinity;
            for (int i = 0; i < _site.Plan.StreetNodes.Length; i++)
            { float d = Horizontal(point, Scene(i)); if (d < cost) { cost = d; best = i; } }
            return best;
        }
        private static float Horizontal(Vector3 a, Vector3 b) { float x = a.x - b.x, z = a.z - b.z; return x * x + z * z; }
        internal static bool Clear(Vector3 a, Vector3 b)
        {
            var delta = b - a; float length = delta.magnitude;
            if (Mathf.Abs(delta.y) > Mathf.Max(.5f, length * .2f)) return false;
            var bottom = a + Vector3.up * .5f; var top = a + Vector3.up * 1.4f;
            if (Physics.CheckCapsule(bottom, top, .25f, Mask, QueryTriggerInteraction.Ignore)) return false;
            if (Physics.CheckCapsule(b + Vector3.up * .5f, b + Vector3.up * 1.4f, .25f, Mask, QueryTriggerInteraction.Ignore)) return false;
            RaycastHit hit;
            return length < .01f || !Physics.CapsuleCast(bottom, top, .25f, delta / length, out hit, length, Mask, QueryTriggerInteraction.Ignore);
        }
    }
}
