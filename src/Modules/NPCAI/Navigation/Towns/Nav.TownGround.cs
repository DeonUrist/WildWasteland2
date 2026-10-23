using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPCAI
{
    internal static partial class Nav
    {
        /// <summary>Instance-only ground connections: saved building floors are read, only their gaps are probed. No template writes.</summary>
        internal sealed class TownGround : IDisposable
        {
            private readonly TownNavContext _site;
            private readonly List<Structure> _buildings = new List<Structure>();
            private readonly Dictionary<long, Field> _fields = new Dictionary<long, Field>();
            private readonly List<KeyValuePair<Field, int>> _work = new List<KeyValuePair<Field, int>>();
            private Structure _map;
            private Vector3 _anchor;
            private int _cell, _near, _workIndex, _copied, _probed;
            internal bool Ready { get; private set; }
            internal float Progress { get { return Ready ? 1 : _map == null ? 0 : _map.Phase == 0 ? .6f * _cell / _map.FloorY.Length
                : _map.Phase == 1 ? .6f + .25f * _map.Next / _map.FloorY.Length : .85f + .15f * _near / _map.FloorY.Length; } }
            internal TownGround(TownNavContext site) { _site = site; }
            private void Begin()
            {
                var plan = _site.Plan; var box = new Bounds(plan.World(Vector3.zero), Vector3.zero);
                foreach (var point in plan.Outline) box.Encapsulate(plan.World(point));
                box.Expand(new Vector3(8, 0, 8));
                float cell = Mathf.Max(1, NavSettings.Cell, Mathf.Sqrt(box.size.x * box.size.z / 40000f));
                int w = Mathf.Max(1, Mathf.CeilToInt(box.size.x / cell)), h = Mathf.Max(1, Mathf.CeilToInt(box.size.z / cell));
                _anchor = _site.Anchor.position;
                _map = new Structure { Root = _site.Anchor, Name = "Town ground " + plan.Key, Cell = cell, W = w, H = h,
                    GridOrigin = _site.Anchor.TransformPoint(box.min), GridRight = _site.Anchor.right, GridForward = _site.Anchor.forward,
                    RefY = _site.Anchor.TransformPoint(plan.WorldCenter).y, InstanceMap = true,
                    FloorY = new float[w * h], Why = new byte[w * h], Open = new bool[w * h], Edges = new byte[w * h], Near = new bool[w * h], StepAtBake = NavSettings.Step };
                foreach (var building in _site.Buildings)
                { var s = building != null ? StructureFor(building.transform) : null; if (s != null && s.Baked && s.FloorY != null) _buildings.Add(s); }
                Physics.SyncTransforms();
            }
            private void Sync()
            {
                Vector3 delta = _site.Anchor.position - _anchor; if (delta == Vector3.zero || _map == null) return;
                _anchor += delta; _map.GridOrigin += delta; _map.RefY += delta.y;
                if (delta.y != 0) for (int i = 0; i < _cell; i++) _map.FloorY[i] += delta.y;
            }
            internal bool Step()
            {
                if (Ready || _site.Anchor == null) return false;
                if (_map == null) { Begin(); return true; }
                Sync(); int n = _map.FloorY.Length;
                if (_map.Phase == 0)
                {
                    if (_cell == n) { _map.Phase = 1; return true; }
                    int i = _cell++, x = i % _map.W, z = i / _map.W;
                    var point = CellCenter(_map, x, z, _map.RefY); bool copied = false;
                    foreach (var building in _buildings)
                    {
                        int bx, bz; if (!CellOf(building, point, out bx, out bz)) continue;
                        int cell = bz * building.W + bx;
                        _map.FloorY[i] = building.FloorY[cell]; _map.Why[i] = building.Why[cell]; _map.Open[i] = building.Open[cell]; copied = true; break;
                    }
                    if (copied) _copied++;
                    else
                    {
                        if (_site.GroundTerrain != null) _map.RefY = _site.GroundTerrain.SampleHeight(point) + _site.GroundTerrain.transform.position.y;
                        Collider floor; byte why; bool open;
                        _map.FloorY[i] = Floor(_map, x, z, out floor, out why, out open); _map.Why[i] = why; _map.Open[i] = open; _probed++;
                    }
                    if (!float.IsNaN(_map.FloorY[i])) _map.Walkable++;
                    return true;
                }
                _sw.Restart();
                if (_map.Phase == 1) { BakeEdges(_map, .25f); return true; }
                if (_near < n)
                {
                    int end = Mathf.Min(n, _near + 64);
                    while (_near < end)
                    {
                        int i = _near++, x = i % _map.W, z = i / _map.W; _map.Near[i] = WallNext(_map, i);
                        if (float.IsNaN(_map.FloorY[i])) continue;
                        for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                        { int cx = x + dx, cz = z + dz; if (cx < 0 || cz < 0 || cx >= _map.W || cz >= _map.H) continue; byte why = _map.Why[cz * _map.W + cx]; if (why == 2 || why == 3) _map.Near[i] = true; }
                    }
                    return true;
                }
                if (!ComponentsStep(_map, .25f)) return true;
                _map.Baked = Ready = true;
                Log.Always(NavRuntime.Owner, "Towns: exterior ground ready " + _site.Plan.Key + ": " + _map.W + "x" + _map.H + " at " + _map.Cell.ToString("0.00")
                    + " m; " + _probed + " gap cells probed, " + _copied + " building cells reused; " + _map.Walkable + " walkable, " + _map.CompSize.Length + " connected areas; building files unchanged.");
                return true;
            }
            private int Cell(Vector3 point)
            {
                int x, z; if (!CellOf(_map, point, out x, out z)) return -1;
                return NearestWalkable(_map, x, z, 4);
            }
            internal bool Reachable(Vector3 from, Vector3 to)
            {
                if (!Ready) return false; Sync(); int a = Cell(from), b = Cell(to);
                return a >= 0 && b >= 0 && _map.Comp[a] == _map.Comp[b];
            }
            internal bool Waypoint(Vector3 from, Vector3 to, out Vector3 point)
            {
                point = to; if (!Ready) return false; Sync();
                int a = Cell(from), b = Cell(to);
                if (a < 0 || b < 0 || _map.Comp[a] != _map.Comp[b]) return false;
                if (WaypointClear(from, to)) return true;
                long key = ((long)_map.Comp[b] << 32) | (uint)(b % _map.W / 2 + (b / _map.W / 2) * ((_map.W + 1) / 2));
                Field field;
                if (!_fields.TryGetValue(key, out field))
                {
                    if (_fields.Count >= 8)
                    {
                        long oldest = 0; float at = float.PositiveInfinity;
                        foreach (var old in _fields) if (old.Value.Made < at) { oldest = old.Key; at = old.Value.Made; }
                        var remove = _fields[oldest]; for (int i = _work.Count - 1; i >= 0; i--) if (ReferenceEquals(_work[i].Key, remove)) _work.RemoveAt(i);
                        Return(remove); _fields.Remove(oldest);
                    }
                    var distance = Rent(_map.FloorY.Length); for (int i = 0; i < distance.Length; i++) distance[i] = float.PositiveInfinity;
                    var heap = _heaps.Count > 0 ? _heaps.Pop() : new Heap(256); heap.Clear(); distance[b] = 0; heap.Push(b, 0);
                    field = new Field { Dist = distance, H = heap, GoalInside = true, GoalCell = b, Settled = float.NegativeInfinity }; _fields[key] = field;
                }
                field.Made = Time.time;
                if (field.H != null && field.Dist[a] > field.Settled)
                {
                    bool queued = false; foreach (var work in _work) if (ReferenceEquals(work.Key, field)) { queued = true; break; }
                    if (!queued) _work.Add(new KeyValuePair<Field, int>(field, a));
                    point = from; return true;
                }
                if (float.IsInfinity(field.Dist[a])) return false;
                int current = a; bool found = false; Vector3 first = to;
                for (int i = 0; i < 24; i++)
                {
                    int next = Downhill(_map, field, current); if (next < 0 || !GridSight(_map, a, next)) break;
                    var candidate = CellCenter(_map, next % _map.W, next / _map.W, _map.FloorY[next]);
                    if (!found) first = candidate;
                    point = candidate; current = next; found = true;
                }
                if (!found) return false;
                if (WaypointClear(from, point)) return true;
                point = first; return WaypointClear(from, point);
            }
            internal bool FieldsStep()
            {
                if (!Ready || _work.Count == 0 || _site.Anchor == null) return false;
                if (_workIndex >= _work.Count) _workIndex = 0;
                var work = _work[_workIndex];
                ExpandCore(_map, work.Key, work.Value, 64);
                if (work.Key.H == null || work.Key.Dist[work.Value] <= work.Key.Settled) _work.RemoveAt(_workIndex); else _workIndex++;
                return _work.Count > 0;
            }
            public void Dispose()
            { foreach (var field in _fields.Values) Return(field); _fields.Clear(); _work.Clear(); _buildings.Clear(); _map = null; Ready = false; }
        }
    }
}
