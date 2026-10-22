using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    // (WW) Bakes one POI instance into PoiNavData (developer tool, sliced by a time budget; the result is shared by every instance of the
    // type). Every column of the POI's footprint is cast top-down until the ground, so EVERY standable surface is found (floors, roofs,
    // container tops, walkways), not only the lowest one as the grid maps do. Then: walks between neighbouring samples, connected
    // surfaces, ledge climbs/drops between surfaces, native ladders (tag "Ladder" triggers), and a coarse node graph (~2 m).
    internal sealed class PoiNavBake
    {
        internal const float MaxStep = .45f, ClimbMin = .45f, ClimbMax = 2.3f, DropMax = 4.5f, Radius = .25f, Height = 1.7f;
        // (heavy by design: a developer bake, only its result matters) the sample spacing and the node spacing (~2 m) in cells
        private readonly float Cell;
        private readonly int NodeSpan;
        // the grid-map bake mask (Nav.BakeMask): no Ignore Raycast, cars, loose items, actors...
        internal static readonly int Mask = ~((1 << 1) | (1 << 2) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 7) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 12) | (1 << 13) | (1 << 15) | (1 << 17) | (1 << 19) | (1 << 22));

        struct Sample { public Vector3 Local, World; public bool Terrain; public int Comp; public int Cell; }

        internal readonly Transform Root;
        internal readonly string Poi;
        internal string Status = "", Error;
        internal bool Done;
        internal PoiNavData Result;
        private readonly PoiNavData _keep;          // the previous data: manual and switched-off links survive
        private int _phase, _i;
        private int _w, _h;
        private Vector3 _min;                       // local grid origin (x, top y, z)
        private float _bottomY;
        private readonly List<Sample> _s = new List<Sample>();
        private List<int>[] _cells;
        private readonly List<int> _edges = new List<int>();     // sample pairs
        private List<int>[] _adj;
        private int _comps;
        private readonly List<PoiNavLink> _links = new List<PoiNavLink>();
        private readonly Dictionary<long, PoiNavLink> _bestClimb = new Dictionary<long, PoiNavLink>();
        private readonly Dictionary<long, float> _bestClimbDist = new Dictionary<long, float>();
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private string _sig = "";

        internal PoiNavBake(Transform root, PoiNavData previous)
        {
            Root = root; Poi = PoiNavStore.TypeOf(root.name); _keep = previous;
            Cell = Mathf.Clamp(PoiNav.BakeCell != null ? PoiNav.BakeCell.Value : .33f, .2f, .5f);
            NodeSpan = Mathf.Max(2, Mathf.RoundToInt(2f / Cell));
        }

        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 }, Dz = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>Works until the budget is spent; true while unfinished.</summary>
        internal bool Step(float budgetMs)
        {
            if (Done) return false;
            if (Root == null) { Fail("the POI is gone"); return false; }
            var sw = Stopwatch.StartNew();
            try
            {
                while (sw.Elapsed.TotalMilliseconds < budgetMs && !Done)
                {
                    switch (_phase)
                    {
                        case 0: Setup(); break;
                        case 1: Column(); break;
                        case 2: Edges(); break;
                        case 3: Components(); break;
                        case 4: Climbs(); break;
                        case 5: Ladders(); break;
                        case 6: Graph(); break;
                    }
                }
            }
            catch (Exception e) { Fail(e.Message); Log.Error("[NPC] POI navigation bake " + Poi, e); }
            return !Done;
        }

        void Fail(string why) { Error = why; Status = "failed: " + why; Done = true; }

        // ---------- 0: footprint ----------
        void Setup()
        {
            var cols = Root.GetComponentsInChildren<Collider>();
            bool any = false; Bounds lb = new Bounds();
            int count = 0;
            foreach (var c in cols)
            {
                if (c == null || !c.enabled || c.isTrigger || ((1 << c.gameObject.layer) & Mask) == 0) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
                var b = c.bounds; count++;
                for (int k = 0; k < 8; k++)
                {
                    var corner = new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z);
                    var l = Root.InverseTransformPoint(corner);
                    if (!any) { lb = new Bounds(l, Vector3.zero); any = true; } else lb.Encapsulate(l);
                }
            }
            if (!any) { Fail("no solid colliders"); return; }
            lb.Expand(new Vector3(4f, 0f, 4f));                         // 2 m of ground all around
            _min = new Vector3(lb.min.x, lb.max.y + 1.5f, lb.min.z);
            _bottomY = lb.min.y - 2f;
            _w = Mathf.Clamp(Mathf.CeilToInt(lb.size.x / Cell), 1, 1200); _h = Mathf.Clamp(Mathf.CeilToInt(lb.size.z / Cell), 1, 1200);
            _cells = new List<int>[_w * _h];
            _sig = count + ":" + lb.size.x.ToString("0.0") + "x" + lb.size.y.ToString("0.0") + "x" + lb.size.z.ToString("0.0");
            _phase = 1; _i = 0;
        }

        Vector3 Local(int x, int z, float y) { return new Vector3(_min.x + (x + .5f) * Cell, y, _min.z + (z + .5f) * Cell); }

        // ---------- 1: every standable surface in each column, top to bottom ----------
        void Column()
        {
            if (_i >= _w * _h) { _phase = 2; _i = 0; return; }
            int x = _i % _w, z = _i / _w; int cell = _i++;
            Vector3 from = Root.TransformPoint(Local(x, z, _min.y));
            float bottom = Root.TransformPoint(Local(x, z, _bottomY)).y;
            for (int guard = 0; guard < 48 && from.y > bottom; guard++)
            {
                RaycastHit hit;
                if (!Physics.Raycast(from, Vector3.down, out hit, from.y - bottom, Mask, QueryTriggerInteraction.Ignore)) break;
                bool terrain = hit.collider.gameObject.layer == 14;
                bool dynamic = hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic;
                if (!dynamic && hit.normal.y >= .7f && Standable(hit.point))
                {
                    if (_cells[cell] == null) _cells[cell] = new List<int>(2);
                    _cells[cell].Add(_s.Count);
                    _s.Add(new Sample { Local = Root.InverseTransformPoint(hit.point), World = hit.point, Terrain = terrain, Comp = -1, Cell = cell });
                }
                if (terrain) break;                                       // nothing walkable under the ground
                from = hit.point + Vector3.down * .06f;                   // through this surface (casts from inside a collider miss it)
            }
            Status = "surfaces " + (100 * _i / (_w * _h)) + "%";
        }

        static bool Standable(Vector3 p)
        {
            return !Physics.CheckCapsule(p + Vector3.up * (Radius + .08f), p + Vector3.up * (Height - Radius), Radius, Mask, QueryTriggerInteraction.Ignore);
        }

        // ---------- 2: walks between neighbouring samples ----------
        void Edges()
        {
            if (_i >= _s.Count) { _phase = 3; _i = 0; _adj = new List<int>[_s.Count]; for (int k = 0; k + 1 < _edges.Count; k += 2) { Adj(_edges[k], _edges[k + 1]); Adj(_edges[k + 1], _edges[k]); } return; }
            int a = _i++; var sa = _s[a]; int x = sa.Cell % _w, z = sa.Cell / _w;
            for (int d = 0; d < 4; d++)                                   // +x, +z, +x+z, +x-z: each pair once
            {
                int nx = x + (d == 1 ? 0 : 1), nz = z + (d == 0 ? 0 : d == 3 ? -1 : 1);
                if (nx >= _w || nz >= _h || nz < 0) continue;
                var list = _cells[nz * _w + nx]; if (list == null) continue;
                foreach (int b in list)
                {
                    var sb = _s[b];
                    if (Mathf.Abs(sb.World.y - sa.World.y) > MaxStep) continue;
                    if (Physics.Linecast(sa.World + Vector3.up * .35f, sb.World + Vector3.up * .35f, Mask, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Linecast(sa.World + Vector3.up * 1.3f, sb.World + Vector3.up * 1.3f, Mask, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Linecast(sa.World + Vector3.up * .85f, sb.World + Vector3.up * .85f, Mask, QueryTriggerInteraction.Ignore)) continue;
                    _edges.Add(a); _edges.Add(b);
                }
            }
            Status = "walks " + (100 * _i / Mathf.Max(1, _s.Count)) + "%";
        }
        void Adj(int a, int b) { if (_adj[a] == null) _adj[a] = new List<int>(4); _adj[a].Add(b); }

        // ---------- 3: connected surfaces (tiny islands dropped) ----------
        void Components()
        {
            var q = new Queue<int>(); var sizes = new List<int>();
            for (int i = 0; i < _s.Count; i++)
            {
                if (_s[i].Comp >= 0) continue;
                int c = sizes.Count, n = 0; Set(i, c); q.Enqueue(i);
                while (q.Count > 0)
                {
                    int a = q.Dequeue(); n++;
                    if (_adj[a] != null) foreach (int b in _adj[a]) if (_s[b].Comp < 0) { Set(b, c); q.Enqueue(b); }
                }
                sizes.Add(n);
            }
            // renumber: islands under 4 samples (half a square metre) are not surfaces anyone stands on
            var map = new int[sizes.Count]; _comps = 0;
            for (int c = 0; c < sizes.Count; c++) map[c] = sizes[c] >= 4 ? _comps++ : -1;
            for (int i = 0; i < _s.Count; i++) { var s = _s[i]; s.Comp = map[s.Comp]; _s[i] = s; }
            _phase = 4; _i = 0;
        }
        void Set(int i, int c) { var s = _s[i]; s.Comp = c; _s[i] = s; }

        // ---------- 4: ledges between surfaces: climb up (with the drop back), and longer drops down ----------
        void Climbs()
        {
            if (_i >= _s.Count) { foreach (var l in _bestClimb.Values) _links.Add(l); _phase = 5; _i = 0; return; }
            int a = _i++; var sa = _s[a]; if (sa.Comp < 0) return;
            int x = sa.Cell % _w, z = sa.Cell / _w;
            int reach = Mathf.Max(2, Mathf.CeilToInt(1.6f / Cell));
            for (int d = 0; d < 8; d++)
            {
                Vector3 dirW = Root.TransformDirection(new Vector3(Dx[d], 0f, Dz[d])); dirW.y = 0f; if (dirW.sqrMagnitude < 1e-4f) continue; dirW.Normalize();
                for (int k = 1; k <= reach; k++)
                {
                    int nx = x + Dx[d] * k, nz = z + Dz[d] * k;
                    if (nx < 0 || nz < 0 || nx >= _w || nz >= _h) break;
                    var list = _cells[nz * _w + nx]; if (list == null) continue;
                    foreach (int b in list)
                    {
                        var sb = _s[b]; if (sb.Comp < 0 || sb.Comp == sa.Comp) continue;
                        float dy = sb.World.y - sa.World.y;
                        if (dy >= ClimbMin && dy <= ClimbMax) TryClimb(sa, sb, dirW, dy);
                        else if (dy <= -ClimbMax && dy >= -DropMax) TryDrop(sa, sb, dy);
                    }
                }
            }
            Status = "ledges " + (100 * _i / Mathf.Max(1, _s.Count)) + "%";
        }

        bool Face(Vector3 from, Vector3 dirW, float dist)
        {
            // a face in front at knee or hip height (rounded car sides and crates count)
            for (int h = 0; h < 2; h++)
            {
                RaycastHit hit;
                if (Physics.Raycast(from + Vector3.up * (h == 0 ? .25f : .7f), dirW, out hit, dist, Mask, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.normal.y) < .7f) return true;
            }
            return false;
        }

        void TryClimb(Sample sa, Sample sb, Vector3 dirW, float dy)
        {
            float flat = new Vector2(sb.World.x - sa.World.x, sb.World.z - sa.World.z).magnitude;
            if (!Face(sa.World, dirW, flat + .3f)) return;
            // room to rise in front of the face, and to go over the edge onto the top
            if (Physics.Linecast(sa.World + Vector3.up * .3f, sa.World + Vector3.up * (dy + 1.0f), Mask, QueryTriggerInteraction.Ignore)) return;
            Vector3 over = sa.World + Vector3.up * (dy + .5f);
            if (Physics.Linecast(over, sb.World + Vector3.up * .5f, Mask, QueryTriggerInteraction.Ignore)) return;
            long key = BucketKey(sa, sb, 0);
            float best;
            if (_bestClimbDist.TryGetValue(key, out best) && best <= flat) return;
            _bestClimbDist[key] = flat;
            float yaw = Quaternion.LookRotation(Root.InverseTransformDirection(dirW)).eulerAngles.y;
            _bestClimb[key] = new PoiNavLink { Kind = PoiLinkKind.Climb, From = sa.Local, To = sb.Local, LineBottom = sa.Local, LineTop = sb.Local, Yaw = yaw, FromSurface = sa.Comp, ToSurface = sb.Comp };
            long back = BucketKey(sb, sa, 1);
            _bestClimbDist[back] = flat;
            _bestClimb[back] = new PoiNavLink { Kind = PoiLinkKind.Drop, From = sb.Local, To = sa.Local, LineBottom = sb.Local, LineTop = sa.Local, Yaw = Mathf.Repeat(yaw + 180f, 360f), FromSurface = sb.Comp, ToSurface = sa.Comp };
        }

        // too high to climb back: a drop only (off the edge, straight down onto the lower surface)
        void TryDrop(Sample upper, Sample lower, float dy)
        {
            Vector3 edge = new Vector3(lower.World.x, upper.World.y + .5f, lower.World.z);
            if (Physics.Linecast(upper.World + Vector3.up * .5f, edge, Mask, QueryTriggerInteraction.Ignore)) return;
            if (Physics.Linecast(edge, lower.World + Vector3.up * .3f, Mask, QueryTriggerInteraction.Ignore)) return;
            long key = BucketKey(upper, lower, 1);
            float flat = new Vector2(lower.World.x - upper.World.x, lower.World.z - upper.World.z).magnitude, best;
            if (_bestClimbDist.TryGetValue(key, out best) && best <= flat) return;
            _bestClimbDist[key] = flat;
            Vector3 dir = Root.InverseTransformDirection(lower.World - upper.World); dir.y = 0f;
            float yaw = dir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(dir.normalized).eulerAngles.y : 0f;
            _bestClimb[key] = new PoiNavLink { Kind = PoiLinkKind.Drop, From = upper.Local, To = lower.Local, LineBottom = upper.Local, LineTop = lower.Local, Yaw = yaw, FromSurface = upper.Comp, ToSurface = lower.Comp };
        }

        // one link per kind per pair of surfaces per 2 m of ledge (at the start sample)
        long BucketKey(Sample from, Sample to, int kind)
        {
            Vector3 l = from.Local;
            return (((long)kind * 8192 + from.Comp) * 8192 + to.Comp) * 1048576L + (long)(Mathf.FloorToInt(l.x / 2f) + 512) * 1024 + (Mathf.FloorToInt(l.z / 2f) + 512);
        }

        // ---------- 5: native ladders: every surface the rung line passes links to the next ones up ----------
        void Ladders()
        {
            foreach (var c in Root.GetComponentsInChildren<Collider>())
            {
                if (c == null || !c.isTrigger || !c.CompareTag("Ladder")) continue;
                LadderLinks(c);
            }
            _phase = 6; _i = 0;
        }

        void LadderLinks(Collider ladder)
        {
            var box = ladder as BoxCollider; var t = ladder.transform;
            Vector3 center, axisUp, through; float length;
            if (box != null && Mathf.Abs(Vector3.Dot(t.up, Vector3.up)) > .7f)
            {
                var s = t.lossyScale; float sx = Mathf.Abs(s.x) * box.size.x, sz = Mathf.Abs(s.z) * box.size.z;
                center = t.TransformPoint(box.center); length = Mathf.Abs(s.y) * box.size.y;
                axisUp = t.up.y >= 0f ? t.up : -t.up; through = sx >= sz ? t.forward : t.right;
            }
            else { var b = ladder.bounds; center = b.center; length = b.size.y; axisUp = Vector3.up; through = b.size.x >= b.size.z ? Vector3.forward : Vector3.right; }
            through.y = 0f; if (through.sqrMagnitude < 1e-4f) return; through.Normalize();
            float bottomY = center.y - length * .5f * axisUp.y, topY = center.y + length * .5f * axisUp.y;
            // ONE climbing side for the whole ladder (from its geometry, not from where the floors happen to be)
            Vector3 side = ClimbSide(ladder, center, axisUp, through, bottomY, topY);
            // a side set in the editor wins (and the exits are picked for it) - except on a leaning ladder, whose foot (out from the
            // wall, under the climber) and top (against it) say the side beyond doubt, and on the hardcoded tall ones
            Vector3 leanH = axisUp; leanH.y = 0f;
            bool fixedSide = leanH.sqrMagnitude > .05f * .05f || Hardcoded(ladder) != Vector3.zero;
            if (_keep != null && !fixedSide)
            {
                Vector3 axLocal = Root.InverseTransformPoint(OnAxis(center, axisUp, bottomY));
                foreach (var old in _keep.Links)
                {
                    if (!old.Manual || old.Kind != PoiLinkKind.Ladder) continue;
                    float dx = old.LineBottom.x - axLocal.x, dz = old.LineBottom.z - axLocal.z; if (dx * dx + dz * dz > .3f * .3f) continue;
                    Vector3 o = -(Root.rotation * (Quaternion.Euler(0f, old.Yaw, 0f) * Vector3.forward)); o.y = 0f;
                    if (o.sqrMagnitude > 1e-4f) { side = Vector3.Dot(o, through) >= 0f ? through : -through; Log.Info("[NPC] POI navigation bake " + Poi + ": ladder side kept from the editor / the player"); }
                    break;
                }
            }
            // per surface: its sample nearest the rung line (within 1.8 m of it, between the ladder's foot and top), preferring the
            // climbing side over the space behind the ladder
            // per surface two samples: its FOOT (where a climber stands, the climbing side) and its EXIT (where one steps off at that
            // level: forward over the ladder, onto the far side, when there is floor there)
            var best = new Dictionary<int, int>(); var bestD = new Dictionary<int, float>();
            var exit = new Dictionary<int, int>(); var exitD = new Dictionary<int, float>();
            for (int i = 0; i < _s.Count; i++)
            {
                var s = _s[i]; if (s.Comp < 0 || s.World.y < bottomY - 1.5f || s.World.y > topY + .6f) continue;
                Vector3 ax = OnAxis(center, axisUp, s.World.y);
                float dx = s.World.x - ax.x, dz = s.World.z - ax.z, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > 1.8f) continue;
                float behind = Vector3.Dot(new Vector3(dx, 0f, dz), side);
                float score = Mathf.Abs(d - .6f) + (behind < -.15f ? 2f : 0f);
                float es = Mathf.Abs(d - .75f) + (behind > -.3f ? 1.5f : 0f), eo;
                if (!exitD.TryGetValue(s.Comp, out eo) || es < eo) { exitD[s.Comp] = es; exit[s.Comp] = i; }
                float old; if (bestD.TryGetValue(s.Comp, out old) && old <= score) continue;
                bestD[s.Comp] = score; best[s.Comp] = i;
            }
            var stops = new List<int>(best.Values);
            stops.Sort((p, q) => _s[p].World.y.CompareTo(_s[q].World.y));
            int added = 0;
            for (int i = 0; i < stops.Count; i++)
            {
                var lo = _s[stops[i]];
                // the next level up (and any other surface within half a metre of it)
                float next = float.MaxValue;
                for (int j = i + 1; j < stops.Count; j++) { float y = _s[stops[j]].World.y; if (y >= lo.World.y + 1f) { next = y; break; } }
                if (next == float.MaxValue) break;
                for (int j = i + 1; j < stops.Count; j++)
                {
                    var hi = _s[stops[j]];
                    int ex; var land = exit.TryGetValue(hi.Comp, out ex) ? _s[ex] : hi;
                    if (hi.World.y < lo.World.y + 1f || hi.World.y > next + .5f) continue;
                    // a ladder hanging above this floor (its bottom out of reach of the feet): the rung line starts at its real
                    // bottom, and NPCs jump up to grab it
                    float lineFoot = bottomY > lo.World.y + .7f && bottomY < hi.World.y - 1f ? bottomY : lo.World.y;
                    Vector3 bottom = OnAxis(center, axisUp, lineFoot), top = OnAxis(center, axisUp, hi.World.y);
                    _links.Add(new PoiNavLink
                    {
                        Kind = PoiLinkKind.Ladder, From = lo.Local, To = land.Local,
                        LineBottom = Root.InverseTransformPoint(bottom), LineTop = Root.InverseTransformPoint(top),
                        Yaw = Quaternion.LookRotation(Root.InverseTransformDirection(-side)).eulerAngles.y,
                        FromSurface = lo.Comp, ToSurface = hi.Comp
                    });
                    if (Hardcoded(ladder) != Vector3.zero) _hardcodedYaw.Add(_links[_links.Count - 1]);
                    added++;
                }
            }
            if (added == 0) Log.Warn("[NPC] POI navigation bake " + Poi + ": ladder " + ladder.transform.parent?.name + " found " + stops.Count + " surfaces along it, linked none");
        }
        // the side a person climbs this ladder from (horizontal unit vector from the rung line towards the climber):
        // 1. a leaning ladder is climbed on the side away from what it leans on; 2. the side opposite the floor at its top;
        // 3. the side its trigger sticks out to from its solid body; 4. away from the structure it is mounted on;
        // 5. the side with clearly more room for a body; 6. away from the POI centre. Every decision is logged.
        Vector3 ClimbSide(Collider ladder, Vector3 center, Vector3 axisUp, Vector3 through, float bottomY, float topY)
        {
            string name = ladder.transform.parent != null ? ladder.transform.parent.name + "/" + ladder.name : ladder.name;
            Vector3 hard = Hardcoded(ladder);
            if (hard != Vector3.zero) return Said(name, "hardcoded", Vector3.Dot(Root.TransformDirection(hard), through) >= 0f ? through : -through);
            // every signal is measured and logged (+ = the `through` side), then the first clear one decides
            Vector3 lean = axisUp; lean.y = 0f;
            float leanS = lean.sqrMagnitude > .05f * .05f ? -Vector3.Dot(lean, through) : 0f;
            // the floor at the top: you step FORWARD off the ladder onto it, so the climber is on the other side
            int fwd = 0, back = 0;
            for (int i = 0; i < _s.Count; i++)
            {
                var q = _s[i]; if (q.Comp < 0 || q.World.y < topY - 2.6f || q.World.y > topY + .3f || q.World.y < bottomY + 1.5f) continue;
                Vector3 ax = OnAxis(center, axisUp, q.World.y); float dx = q.World.x - ax.x, dz = q.World.z - ax.z;
                if (dx * dx + dz * dz > 1.6f * 1.6f) continue;
                float along = dx * through.x + dz * through.z;
                if (along > .3f) fwd++; else if (along < -.3f) back++;
            }
            // the trigger sticking out from the ladder's solid body
            Bounds trig = ladder.bounds; Vector3 solid = Vector3.zero; int solids = 0;
            var scope = ladder.transform.parent != null ? ladder.transform.parent : ladder.transform;
            foreach (var c in scope.GetComponentsInChildren<Collider>())
            {
                if (c == null || c.isTrigger || c == ladder) continue;
                var cb = c.bounds; if (cb.size.x > 3f && cb.size.z > 3f) continue;
                var grown = trig; grown.Expand(.6f); if (!grown.Intersects(cb)) continue;
                solid += cb.center; solids++;
            }
            float stick = 0f;
            if (solids > 0) { solid /= solids; stick = (trig.center.x - solid.x) * through.x + (trig.center.z - solid.z) * through.z; }
            // room for a climbing body along the height, each side on its own
            int roomP = 0, roomN = 0, probes = 0;
            for (float y = bottomY + .3f; y < topY - 1.2f; y += .5f, probes++)
                for (int k = 0; k < 2; k++)
                {
                    Vector3 p = OnAxis(center, axisUp, y) + through * (k == 0 ? .5f : -.5f);
                    if (!Physics.CheckCapsule(p + Vector3.up * .3f, p + Vector3.up * 1.4f, .25f, Mask, QueryTriggerInteraction.Ignore)) { if (k == 0) roomP++; else roomN++; }
                }
            // the structure it is mounted on
            float host = 0f; string hostName = "-";
            var ht = ladder.transform.parent != null ? ladder.transform.parent.parent : null;
            if (ht != null && ht.GetComponent<Collider>() != null) { Vector3 away = center - ht.GetComponent<Collider>().bounds.center; away.y = 0f; host = Vector3.Dot(away, through); hostName = ht.name; }
            Vector3 off = center - Root.position; off.y = 0f; float poi = Vector3.Dot(off, through);
            string signals = "lean " + leanS.ToString("0.00") + ", top floor +" + fwd + "/-" + back + ", trigger " + stick.ToString("0.00") + " (" + solids + " bodies), room +" + roomP + "/-" + roomN + " of " + probes + ", host " + hostName + " " + host.ToString("0.0") + ", POI " + poi.ToString("0.0");
            if (leanS != 0f) return Said(name, "leans | " + signals, leanS > 0f ? through : -through);
            if (fwd + back >= 3 && (fwd >= 3 * Mathf.Max(1, back) || back >= 3 * Mathf.Max(1, fwd))) return Said(name, "top floor | " + signals, fwd > back ? -through : through);
            if (Mathf.Abs(stick) > .05f) return Said(name, "trigger | " + signals, stick > 0f ? through : -through);
            if (Mathf.Abs(roomP - roomN) >= 2) return Said(name, "room | " + signals, roomP > roomN ? through : -through);
            if (Mathf.Abs(host) > .1f) return Said(name, "away from the host | " + signals, host > 0f ? through : -through);
            return Said(name, "away from the POI centre | " + signals, poi >= 0f ? through : -through);
        }
        // the few tall ladders (towers, cranes) whose side no geometry rule reads reliably: POI type + "parent/trigger" -> the climber's
        // side as a POI-local direction (checked in game)
        static readonly Dictionary<string, Vector3> HardSides = new Dictionary<string, Vector3>
        {
            { "Camp_13|ladder_collider/ladder_trigger", new Vector3(0f, 0f, 1f) },     // the radio tower (22 m, middle floor at 10.8 m)
        };
        Vector3 Hardcoded(Collider ladder)
        {
            string name = ladder.transform.parent != null ? ladder.transform.parent.name + "/" + ladder.name : ladder.name;
            Vector3 v; return HardSides.TryGetValue(Poi + "|" + name, out v) ? v : Vector3.zero;
        }
        private readonly HashSet<PoiNavLink> _hardcodedYaw = new HashSet<PoiNavLink>();

        Vector3 Said(string ladder, string why, Vector3 side)
        {
            Vector3 l = Root.InverseTransformDirection(side);
            Log.Info("[NPC] POI navigation bake " + Poi + ": ladder " + ladder + " climbed from local (" + l.x.ToString("0.0") + ", " + l.z.ToString("0.0") + ") side: " + why);
            return side;
        }

        static Vector3 OnAxis(Vector3 center, Vector3 up, float y) { var p = center + up * ((y - center.y) / Mathf.Max(.2f, up.y)); p.y = y; return p; }

        // ---------- 6: the node graph (one node per surface per 2 m square), straight walks between neighbouring nodes ----------
        // streamline: one climb / drop per pair of surfaces per 4 m of edge (the cleanest), none that duplicate another's start and
        // landing, and none onto (or off) a scrap of surface smaller than a body needs to stand and turn on (a wall top, a junk pile)
        int _thinned;
        void Thin()
        {
            var size = new Dictionary<int, int>();
            foreach (var q in _s) if (q.Comp >= 0) { int c; size.TryGetValue(q.Comp, out c); size[q.Comp] = c + 1; }
            var laddered = new HashSet<int>();
            foreach (var l in _links) if (l.Kind == PoiLinkKind.Ladder) { laddered.Add(l.FromSurface); laddered.Add(l.ToSurface); }
            int minSamples = Mathf.CeilToInt(1.1f / (Cell * Cell));
            System.Func<int, bool> tiny = c => { int n; return !laddered.Contains(c) && (!size.TryGetValue(c, out n) || n < minSamples); };
            var cand = new List<PoiNavLink>(); var kept = new List<PoiNavLink>();
            foreach (var l in _links)
            {
                if (l.Kind == PoiLinkKind.Ladder) { kept.Add(l); continue; }
                if (l.Kind == PoiLinkKind.Climb && tiny(l.ToSurface) || l.Kind == PoiLinkKind.Drop && tiny(l.FromSurface)) continue;
                cand.Add(l);
            }
            cand.Sort((x, y) => Flat(x).CompareTo(Flat(y)));               // the most direct first
            foreach (var l in cand)
            {
                bool dup = false;
                foreach (var k in kept)
                {
                    if (k.Kind != l.Kind) continue;
                    float df = (k.From - l.From).sqrMagnitude, dt = (k.To - l.To).sqrMagnitude;
                    if (k.FromSurface == l.FromSurface && k.ToSurface == l.ToSurface && df < 4f * 4f) { dup = true; break; }
                    if (df < 1.5f * 1.5f && dt < 1.5f * 1.5f) { dup = true; break; }
                }
                if (!dup) kept.Add(l);
            }
            _thinned = _links.Count - kept.Count;
            _links.Clear(); _links.AddRange(kept);
        }
        static float Flat(PoiNavLink l) { return new Vector2(l.To.x - l.From.x, l.To.z - l.From.z).magnitude; }

        void Graph()
        {
            Thin();
            var data = new PoiNavData { Poi = Poi, Signature = _sig };
            var nodeOf = new Dictionary<long, int>();                    // (comp, super x, super z) -> node
            var members = new Dictionary<long, List<int>>();
            for (int i = 0; i < _s.Count; i++)
            {
                var s = _s[i]; if (s.Comp < 0) continue;
                int x = s.Cell % _w, z = s.Cell / _w;
                long key = Key(s.Comp, x / (int)NodeSpan, z / (int)NodeSpan);
                List<int> m; if (!members.TryGetValue(key, out m)) members[key] = m = new List<int>();
                m.Add(i);
            }
            var sampleOfNode = new List<int>();
            foreach (var kv in members)
            {
                Vector3 mean = Vector3.zero; foreach (int i in kv.Value) mean += _s[i].Local; mean /= kv.Value.Count;
                int best = kv.Value[0]; float bd = float.MaxValue;
                foreach (int i in kv.Value) { float d = (_s[i].Local - mean).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
                nodeOf[kv.Key] = data.Nodes.Count; sampleOfNode.Add(best);
                bool terrain = false; foreach (int i in kv.Value) if (_s[i].Terrain) { terrain = true; break; }
                data.Nodes.Add(new PoiNavNode { Pos = _s[best].Local, Surface = _s[best].Comp, Terrain = terrain && _s[best].Terrain });
            }
            foreach (var kv in nodeOf)
            {
                long key = kv.Key; int comp = (int)(key >> 40), sx = (int)((key >> 20) & 0xFFFFF) - 512, sz = (int)(key & 0xFFFFF) - 512;
                for (int dz = 0; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dz == 0 && dx <= 0) continue;                // each neighbour pair once
                        int other; if (!nodeOf.TryGetValue(Key(comp, sx + dx, sz + dz), out other)) continue;
                        if (WalkClear(sampleOfNode[kv.Value], sampleOfNode[other])) { data.Edges.Add(kv.Value); data.Edges.Add(other); }
                    }
            }
            // every baked link end is a node of its own, joined only by checked straight walks to the nodes around it (an NPC never
            // heads for a ladder foot or a ledge through a bar or a wall just because a node happened to be near it)
            var sampleAt = new Dictionary<Vector3, int>();
            for (int i = 0; i < _s.Count; i++) if (_s[i].Comp >= 0 && !sampleAt.ContainsKey(_s[i].Local)) sampleAt[_s[i].Local] = i;
            var nodeOfSample = new Dictionary<int, int>();
            for (int n = 0; n < sampleOfNode.Count; n++) nodeOfSample[sampleOfNode[n]] = n;
            int endNodes = 0, isolated = 0;
            foreach (var l in _links)
            {
                for (int e = 0; e < 2; e++)
                {
                    int si; if (!sampleAt.TryGetValue(e == 0 ? l.From : l.To, out si)) continue;
                    if (nodeOfSample.ContainsKey(si)) continue;
                    var sm = _s[si]; int node = data.Nodes.Count;
                    data.Nodes.Add(new PoiNavNode { Pos = sm.Local, Surface = sm.Comp, Terrain = sm.Terrain });
                    sampleOfNode.Add(si); nodeOfSample[si] = node; endNodes++;
                    int joined = 0;
                    for (int n = 0; n < node; n++)
                    {
                        if (data.Nodes[n].Surface != sm.Comp) continue;
                        Vector3 dv = data.Nodes[n].Pos - sm.Local;
                        if (dv.x * dv.x + dv.z * dv.z > 9f || Mathf.Abs(dv.y) > 3f) continue;
                        if (WalkClear(sampleOfNode[n], si)) { data.Edges.Add(n); data.Edges.Add(node); joined++; }
                    }
                    if (joined == 0) isolated++;
                }
            }
            // links: baked ones plus the editor's (manual links are kept; a baked link switched off stays off)
            // an editor ladder on the rung line of a baked one is a SIDE override (a flip in the editor, or learned from the player): the
            // fresh baked links of that ladder take its facing and keep being overrides; the old copies (old positions) are dropped
            var consumed = new HashSet<PoiNavLink>();
            if (_keep != null)
                foreach (var l in _links)
                {
                    if (l.Kind != PoiLinkKind.Ladder) continue;
                    foreach (var old in _keep.Links)
                    {
                        if (!old.Manual || old.Kind != PoiLinkKind.Ladder) continue;
                        float dx = old.LineBottom.x - l.LineBottom.x, dz = old.LineBottom.z - l.LineBottom.z;
                        if (dx * dx + dz * dz > .3f * .3f) continue;
                        consumed.Add(old);
                        Vector3 run = l.LineTop - l.LineBottom; bool leaning = new Vector2(run.x, run.z).magnitude > .05f * Mathf.Max(.5f, Mathf.Abs(run.y));
                        if (!leaning && !_hardcodedYaw.Contains(l) && Mathf.Abs(Mathf.DeltaAngle(old.Yaw, l.Yaw)) > 90f) l.Yaw = old.Yaw;
                        l.Manual = true;
                    }
                }
            foreach (var l in _links)
            {
                bool covered = false;   // an editor link already does this one
                if (l.Kind == PoiLinkKind.Ladder) { data.Links.Add(l); continue; }
                if (_keep != null) foreach (var old in _keep.Links) if (old.Manual && old.Kind == l.Kind && (old.From - l.From).sqrMagnitude < 2.25f && (old.To - l.To).sqrMagnitude < 2.25f) { covered = true; break; }
                if (covered) continue;
                if (_keep != null) foreach (var old in _keep.Links) if (!old.Manual && old.Off && old.Kind == l.Kind && (old.From - l.From).sqrMagnitude < 1f && (old.To - l.To).sqrMagnitude < 1f) { l.Off = true; break; }
                data.Links.Add(l);
            }
            if (_keep != null) foreach (var old in _keep.Links) if (old.Manual && !consumed.Contains(old)) data.Links.Add(old.Copy());
            data.Recompute();
            Result = data; Done = true;
            int ladders = 0, climbs = 0; foreach (var l in data.Links) { if (l.Kind == PoiLinkKind.Ladder) ladders++; else if (l.Kind == PoiLinkKind.Climb) climbs++; }
            Status = "done: " + data.Nodes.Count + " nodes (" + endNodes + " link ends, " + isolated + " unreachable) on " + data.Surfaces + " surfaces, " + (data.Edges.Count / 2) + " walks, " + ladders + " ladders, " + climbs + " climbs (" + _thinned + " redundant links left out)";
        }
        static long Key(int comp, int sx, int sz) { return ((long)comp << 40) | ((long)(sx + 512) << 20) | (long)(uint)(sz + 512); }

        // a straight walk between two samples of one surface: every half cell along the line stands on that surface within a step
        bool WalkClear(int a, int b)
        {
            var sa = _s[a]; var sb = _s[b];
            Vector3 la = sa.Local, lb = sb.Local; float y = la.y;
            int steps = Mathf.Max(1, Mathf.CeilToInt(new Vector2(lb.x - la.x, lb.z - la.z).magnitude / (Cell * .5f)));
            for (int k = 1; k < steps; k++)
            {
                Vector3 p = Vector3.Lerp(la, lb, (float)k / steps);
                int x = Mathf.FloorToInt((p.x - _min.x) / Cell), z = Mathf.FloorToInt((p.z - _min.z) / Cell);
                if (x < 0 || z < 0 || x >= _w || z >= _h) return false;
                var list = _cells[z * _w + x]; if (list == null) return false;
                bool found = false;
                foreach (int i in list) if (_s[i].Comp == sa.Comp && Mathf.Abs(_s[i].Local.y - y) <= MaxStep) { y = _s[i].Local.y; found = true; break; }
                if (!found) return false;
            }
            // and nothing in the way at shin, hip or chest height (bars, railings, the slab of a ladder)
            for (int h = 0; h < 3; h++)
            {
                Vector3 up = Vector3.up * (h == 0 ? .35f : h == 1 ? .85f : 1.3f);
                if (Physics.Linecast(sa.World + up, sb.World + up, Mask, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }
    }
}
