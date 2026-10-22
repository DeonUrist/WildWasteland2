using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// (WW) Runtime POI navigation: human NPCs inside a POI whose type has pre-recorded data (PoiNavData) route over its node graph with
    /// A* (a few hundred nodes, no physics) and take its ladders, ledge climbs and drops as scripted traversals with the ladder / climb
    /// animations (IAnimationApi.BeginNpcTraversal). Outside such POIs, or for creatures, Nav's grid maps and feelers carry on.
    /// </summary>
    internal static partial class PoiNav
    {
        /// <summary>[NPCAI / Pathfinding] PoiNavigation (bound by <see cref="NavSettings.Bind"/>; null = unbound = off).</summary>
        internal static ConfigEntry<bool> Enabled;
        /// <summary>[NPCAI / POI pathfinding] NpcLadderSpeed, m/s (bound by <see cref="NavSettings.Bind"/>).</summary>
        internal static ConfigEntry<float> LadderSpeed;

        // ------------------------------------------------------------------ graph per data
        /// <summary>The node/edge/link graph of one POI data set (built a step at a time).</summary>
        internal sealed class Graph
        {
            /// <summary>One graph arc: target node, cost, link code (-1 = walk).</summary>
            internal struct Arc { public int To; public float Cost; public int Link; }   // Link -1 = a walk, else link*2 (+1 = taken backwards: down a ladder)
            internal readonly PoiNavData Data;
            internal readonly List<Arc>[] Out;
            internal readonly int[] LinkFrom, LinkTo;
            internal readonly Dictionary<long, List<int>> Buckets = new Dictionary<long, List<int>>();
            private int _node, _edge, _link;
            internal bool Ready;
            /// <summary>An empty graph for one data set; built a node / edge / link at a time by Step.</summary>
            internal Graph(PoiNavData d)
            {
                Data = d; int n = d.Nodes.Count;
                Out = new List<Arc>[n]; LinkFrom = new int[d.Links.Count]; LinkTo = new int[d.Links.Count];
            }
            /// <summary>One build step: bucket one node, add one edge pair, or resolve one link's end nodes; Ready after the last.</summary>
            internal void Step()
            {
                var d = Data;
                if (_node < d.Nodes.Count)
                {
                    int i = _node++;
                    var p = d.Nodes[i].Pos; long key = Bucket(Mathf.FloorToInt(p.x / 2f), Mathf.FloorToInt(p.z / 2f));
                    List<int> nodes; if (!Buckets.TryGetValue(key, out nodes)) Buckets[key] = nodes = new List<int>();
                    nodes.Add(i); Out[i] = new List<Arc>(6); return;
                }
                if (_edge + 1 < d.Edges.Count)
                {
                    int i = _edge; _edge += 2;
                    int a = d.Edges[i], b = d.Edges[i + 1]; float c = Vector3.Distance(d.Nodes[a].Pos, d.Nodes[b].Pos);
                    Out[a].Add(new Arc { To = b, Cost = c, Link = -1 }); Out[b].Add(new Arc { To = a, Cost = c, Link = -1 }); return;
                }
                if (_link < d.Links.Count)
                {
                    int i = _link++;
                    var l = d.Links[i];
                    int a = Nearest(l.From, l.FromSurface), b = Nearest(l.To, l.ToSurface);
                    LinkFrom[i] = a; LinkTo[i] = b;
                    if (l.Off || a < 0 || b < 0 || a == b) return;
                    float extra = l.Kind == PoiLinkKind.Ladder ? Mathf.Abs(l.LineTop.y - l.LineBottom.y) * 2f + 3f : l.Kind == PoiLinkKind.Climb ? 8f : 4f;   // walking round is preferred over hopping onto things
                    Out[a].Add(new Arc { To = b, Cost = Vector3.Distance(d.Nodes[a].Pos, l.From) + extra + Vector3.Distance(l.To, d.Nodes[b].Pos), Link = i * 2 });
                    // ladders work both ways (climbs come with their own drop link)
                    if (l.Kind == PoiLinkKind.Ladder)
                        Out[b].Add(new Arc { To = a, Cost = Vector3.Distance(d.Nodes[b].Pos, l.To) + extra + Vector3.Distance(l.From, d.Nodes[a].Pos), Link = i * 2 + 1 });
                    return;
                }
                Ready = true;
            }
            /// <summary>Node nearest to a link end (within 2.5 m, height weighted, on the given surface when >= 0), or -1.</summary>
            private int Nearest(Vector3 p, int surface)
            {
                int best = -1; float distance = 6.25f;
                for (int x = Mathf.FloorToInt((p.x - 2.5f) / 2f); x <= Mathf.FloorToInt((p.x + 2.5f) / 2f); x++)
                for (int z = Mathf.FloorToInt((p.z - 2.5f) / 2f); z <= Mathf.FloorToInt((p.z + 2.5f) / 2f); z++)
                {
                    List<int> nodes; if (!Buckets.TryGetValue(Bucket(x, z), out nodes)) continue;
                    foreach (int i in nodes)
                    {
                        if (surface >= 0 && Data.Nodes[i].Surface != surface) continue;
                        Vector3 v = Data.Nodes[i].Pos - p; v.y *= 2f; float s = v.sqrMagnitude;
                        if (s < distance || s == distance && best >= 0 && i < best) { distance = s; best = i; }
                    }
                }
                return best;
            }
        }
        private static readonly Dictionary<PoiNavData, Graph> _graphs = new Dictionary<PoiNavData, Graph>();
        /// <summary>The (shared) graph of a data set, created on first use (cache cleared beyond 128).</summary>
        internal static Graph GraphOf(PoiNavData d)
        {
            Graph g; if (!_graphs.TryGetValue(d, out g)) { if (_graphs.Count > 128) _graphs.Clear(); _graphs[d] = g = new Graph(d); }
            return g;
        }

        // ------------------------------------------------------------------ instances (one per live POI root with data)
        /// <summary>One live POI instance with data: world node positions and bounds.</summary>
        internal sealed class Inst
        {
            internal Transform Root; internal string Poi; internal PoiNavData Data; internal Graph G;
            internal Vector3[] World; internal Bounds Box; internal Vector3 RootPos; internal int Id;
            internal int BuildIndex; internal bool Ready, Queued;
            /// <summary>Scene position of a POI-local point.</summary>
            internal Vector3 W(Vector3 local) { return Root.TransformPoint(local); }
        }
        private static readonly Dictionary<int, Inst> _inst = new Dictionary<int, Inst>();
        private static readonly List<Transform> _roots = new List<Transform>();
        private static readonly List<int> _dead = new List<int>();
        private static readonly Queue<Inst> _builds = new Queue<Inst>();
        private static int _buildFrame = -1, _buildNodes, _buildRays;
        private static float _nextScan;
        internal static Dictionary<int, Inst>.ValueCollection Instances { get { return _inst.Values; } }

        /// <summary>At most once a second: drops dead instances/routes and creates or rebuilds the instance of every structure root whose POI type has data.</summary>
        internal static void Rescan(bool force = false)
        {
            float now = Time.unscaledTime;
            if (!force && now < _nextScan) return;
            _nextScan = now + 1f;
            Nav.Roots(_roots);
            NavigationAuthoringApi.AppendSetupRoots(_roots);
            _dead.Clear();
            foreach (var kv in _inst) if (kv.Value.Root == null || !kv.Value.Root.gameObject.activeInHierarchy) _dead.Add(kv.Key);
            foreach (int k in _dead) _inst.Remove(k);
            _dead.Clear(); foreach (var kv in _routes) if (kv.Value.Owner == null) _dead.Add(kv.Key);
            foreach (int k in _dead) _routes.Remove(k);
            foreach (var r in _roots)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                int id = r.GetInstanceID(); string poi = PoiNavStore.TypeOf(r.name);
                var data = PoiNavStore.GetAsync(poi);
                Inst inst;
                if (_inst.TryGetValue(id, out inst))
                {
                    if (data == null) { _inst.Remove(id); continue; }
                    if (inst.Data != data || (inst.RootPos - r.position).sqrMagnitude > .01f || !inst.Ready && !inst.Queued) Build(inst, data);
                    continue;
                }
                if (data == null) continue;
                inst = new Inst { Root = r, Poi = poi, Id = id };
                Build(inst, data);
                _inst[id] = inst;
            }
        }

        /// <summary>(Re)binds an instance to its data: world bounds now, node positions queued for the sliced build.</summary>
        internal static void Build(Inst inst, PoiNavData data)
        {
            inst.Ready = false; inst.BuildIndex = 0;
            inst.Data = data; inst.G = GraphOf(data); inst.RootPos = inst.Root.position;
            inst.World = new Vector3[data.Nodes.Count];
            var b = data.Box; bool first = true; Bounds wb = new Bounds();
            for (int k = 0; k < 8; k++)
            {
                var c = inst.Root.TransformPoint(new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z));
                if (first) { wb = new Bounds(c, Vector3.zero); first = false; } else wb.Encapsulate(c);
            }
            inst.Box = wb;
            if (!inst.Queued) { inst.Queued = true; _builds.Enqueue(inst); }
        }

        // One node per scheduler step; the ray allowance is shared by every POI in the frame.
        /// <summary>Old NPC-entry sliced job body: forwards to <see cref="BuildSlice"/> unless the NPCAI entry runs "NPCAI.PoiNav.Build".</summary>
        [System.Obsolete("refactor: NPCAIModule runs navigation (NavRuntime); no-op while it does")]
        internal static bool BuildStep() { return !NavRuntime.Active && BuildSlice(); }

        /// <summary>Job "NPCAI.PoiNav.Build" (sliced, 500 us): one graph/node step of the queued POI instances; true = more work.</summary>
        internal static bool BuildSlice()
        {
            if (_builds.Count == 0 || Enabled == null || !Enabled.Value) return false;
            if (_buildFrame != Time.frameCount) { _buildFrame = Time.frameCount; _buildNodes = _buildRays = 0; }
            if (_buildNodes >= 256 || _buildRays >= 64) return false;
            var inst = _builds.Peek(); Inst live;
            if (inst.Root == null || !inst.Root.gameObject.activeInHierarchy || !_inst.TryGetValue(inst.Id, out live) || live != inst)
            { _builds.Dequeue(); inst.Queued = false; return _builds.Count > 0; }
            if (!inst.G.Ready) { inst.G.Step(); _buildNodes++; return true; }
            if (inst.BuildIndex < inst.World.Length)
            {
                int k = inst.BuildIndex++; _buildNodes++;
                inst.World[k] = inst.Root.TransformPoint(inst.Data.Nodes[k].Pos);
                if (inst.Data.Nodes[k].Terrain)
                {
                    RaycastHit h; _buildRays++;
                    if (Physics.Raycast(inst.World[k] + Vector3.up * 3f, Vector3.down, out h, 7f, 1 << 14, QueryTriggerInteraction.Ignore)) inst.World[k].y = h.point.y;
                }
            }
            if (inst.BuildIndex >= inst.World.Length) { inst.Ready = true; inst.Queued = false; _builds.Dequeue(); }
            return _builds.Count > 0;
        }

        /// <summary>(WW) The body's real direction on a POI route, every physics step: straight at the route's current point (the turn
        /// the body shows is only visual - no arcs, no overshoot past a node or a link start). False = not on a route now.</summary>
        internal static bool SteerDir(GameObject owner, Vector3 pos, out Vector3 dir)
        {
            dir = Vector3.zero;
            if (owner == null || Enabled == null || !Enabled.Value || PoiNavTraverse.Busy(owner)) return false;
            Route r; if (!_routes.TryGetValue(owner.GetInstanceID(), out r) || !r.HasNext || !r.Inst.Ready || r.Inst.Root == null || !r.Inst.Root.gameObject.activeInHierarchy) return false;
            Vector3 d = r.Next - pos; d.y = 0f;
            if (d.sqrMagnitude > .0025f) dir = d.normalized;
            return true;
        }

        // (WW) Walking a POI route needs no body physics: the route is known walkable, so the body is carried along it kinematically
        // (straight at the route point, feet put on the floor under it each frame) - it can't catch on a wall corner, a prop or a car edge.
        // Physics comes back the moment the NPC is off the route (or nobody drives it for a moment).
        sealed class Walker
        {
            public GameObject Owner; public Rigidbody Rb; public bool Kinematic, Gravity; public RigidbodyInterpolation Interp; public float Last, FeetOff; public bool Reignore;
            public List<Collider> Mine = new List<Collider>(), Player = new List<Collider>();
        }
        private static readonly Dictionary<int, Walker> _walkers = new Dictionary<int, Walker>();
        private static readonly List<int> _walkDone = new List<int>();
        private static readonly RaycastHit[] _walkHits = new RaycastHit[64];

        /// <summary>Moves the NPC one frame along its POI route at `speed` (m/s). False = not on a route: drive it as usual.</summary>
        internal static bool Walk(GameObject owner, float speed, Vector3 dir)
        {
            if (owner == null) return false;
            if (PoiNavTraverse.Busy(owner)) return true;                 // a ladder / climb has the body
            int id = owner.GetInstanceID();
            Vector3 pos = owner.transform.position;
            Route route; if (Enabled == null || !Enabled.Value || !_routes.TryGetValue(id, out route) || !route.HasNext || !route.Inst.Ready || route.Inst.Root == null || !route.Inst.Root.gameObject.activeInHierarchy) { Release(id); return false; }
            Walker w;
            if (!_walkers.TryGetValue(id, out w))
            {
                var rb = owner.GetComponent<Rigidbody>(); if (rb == null) return false;
                Route r; _routes.TryGetValue(id, out r);
                w = new Walker { Owner = owner, Rb = rb, Kinematic = rb.isKinematic, Gravity = rb.useGravity, Interp = rb.interpolation, FeetOff = r != null && !float.IsNaN(r.FeetOff) ? r.FeetOff : PoiNavTraverse.FeetOffset(owner, rb) };
                foreach (var c in owner.GetComponentsInChildren<Collider>()) if (c != null && !c.isTrigger) w.Mine.Add(c);
                var pl = GameRefs.Player;
                if (pl != null) foreach (var pc in pl.GetComponentsInChildren<Collider>()) if (pc != null && !pc.isTrigger) { w.Player.Add(pc); foreach (var c in w.Mine) Physics.IgnoreCollision(c, pc, true); }
                rb.velocity = Vector3.zero; rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
                _walkers[id] = w;
            }
            w.Last = Time.time;
            if (w.Reignore) { w.Reignore = false; foreach (var pc in w.Player) if (pc != null) foreach (var c in w.Mine) if (c != null) Physics.IgnoreCollision(c, pc, true); }   // the traversal gave them back
            if (dir == Vector3.zero || speed <= 0f) return true;
            float dt = Time.deltaTime;
            Vector3 to = route.Next - pos; to.y = 0f;
            Vector3 p = pos + dir * Mathf.Min(speed * dt, to.magnitude);
            // the floor under the new spot: the highest surface from half a step above the feet down to 1.5 m below them
            float feet = pos.y - w.FeetOff, best = float.MinValue;
            int count = Physics.RaycastNonAlloc(new Vector3(p.x, feet + .55f, p.z), Vector3.down, _walkHits, 2.05f, Layers.NavSolid, QueryTriggerInteraction.Ignore);
            if (count == _walkHits.Length) { Release(id); return false; }   // truncated, unordered hits: let normal movement handle it
            for (int k = 0; k < count; k++) { var h = _walkHits[k]; if (h.collider == null || h.collider.attachedRigidbody == w.Rb || h.normal.y < .5f) continue; if (h.point.y > best) best = h.point.y; }
            if (best == float.MinValue) { Release(id); return false; }   // no floor: let it fall
            p.y = Mathf.MoveTowards(pos.y, best + w.FeetOff, Mathf.Max(1.5f, speed * 1.5f) * dt * 4f);
            w.Rb.MovePosition(p);
            return true;
        }
        /// <summary>Gives a walker's body its physics and player collisions back.</summary>
        static void Release(int id)
        {
            Walker w; if (!_walkers.TryGetValue(id, out w)) return;
            _walkers.Remove(id);
            try
            {
                foreach (var pc in w.Player) if (pc != null) foreach (var c in w.Mine) if (c != null) Physics.IgnoreCollision(c, pc, false);
                if (w.Rb != null) { w.Rb.isKinematic = w.Kinematic; w.Rb.useGravity = w.Gravity; w.Rb.interpolation = w.Interp; }
            }
            catch (Exception) { }
        }
        /// <summary>Old NPC-entry job body: forwards to <see cref="UpdateWalkers"/> unless the NPCAI entry runs "NPCAI.PoiNav.Walkers".</summary>
        [System.Obsolete("refactor: NPCAIModule runs navigation (NavRuntime); no-op while it does")]
        internal static void WalkerTick() { if (!NavRuntime.Active) UpdateWalkers(); }

        /// <summary>Job "NPCAI.PoiNav.Walkers" (every frame): bodies nobody walked for 0.25 s (the brain stopped driving it: an attack, a stand) get their physics back.</summary>
        internal static void UpdateWalkers()
        {
            if (_walkers.Count == 0) return;
            _walkDone.Clear();
            float now = Time.time;
            foreach (var kv in _walkers)
            {
                if (kv.Value.Rb != null && PoiNavTraverse.Busy(kv.Value.Owner)) { kv.Value.Last = now; kv.Value.Reignore = true; continue; }   // mid-ladder: the traversal owns the body
                if (kv.Value.Rb == null || now - kv.Value.Last > .25f) _walkDone.Add(kv.Key);
            }
            foreach (int id in _walkDone) Release(id);
        }

        /// <summary>True while this NPC walks a POI route (the brain's old map gates, hops and "full feelers" stay out of its way).</summary>
        internal static bool Guiding(GameObject owner)
        {
            if (owner == null) return false;
            if (PoiNavTraverse.Busy(owner)) return true;
            Route r; return Enabled != null && Enabled.Value && _routes.TryGetValue(owner.GetInstanceID(), out r) && r.HasNext && r.Inst.Ready && r.Inst.Root != null && r.Inst.Root.gameObject.activeInHierarchy;
        }
        /// <summary>Stops following the current POI route point (the route stays for the next think).</summary>
        internal static void StopGuiding(GameObject owner)
        {
            Route r; if (owner == null) return; int id = owner.GetInstanceID();
            if (_routes.TryGetValue(id, out r)) r.HasNext = false;
            if (!PoiNavTraverse.Busy(owner)) Release(id);
        }
        /// <summary>Forgets an NPC's route so the next think plans again from where it stands.</summary>
        internal static void DropRoute(GameObject owner)
        {
            Route r; if (owner == null || !_routes.TryGetValue(owner.GetInstanceID(), out r)) return;
            r.Steps.Clear(); r.Index = 0; r.HasNext = false; r.Pending = true; r.DirectSurface = -1;
        }
        /// <summary>True when a point lies in the bounds of a POI instance with navigation data.</summary>
        internal static bool Inside(Vector3 p) { return Enabled != null && Enabled.Value && _inst.Count > 0 && At(p, false) != null; }

        /// <summary>The (ready) instance whose bounds contain p, nearest centre first, or null.</summary>
        static Inst At(Vector3 p, bool readyOnly = true)
        {
            Inst best = null; float bd = float.MaxValue;
            foreach (var i in _inst.Values)
            {
                if (i.Root == null || readyOnly && !i.Ready || !i.Box.Contains(p)) continue;
                float d = (i.Box.center - p).sqrMagnitude; if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>the node under these feet: within 4 m flat, from 1.2 m below to 0.6 m above them; level and in plain sight first</summary>
        static long Bucket(int x, int z) { return ((long)x << 32) | (long)(uint)z; }
        private static readonly List<int> _nearNodes = new List<int>();
        /// <summary>World terrain projection changes Y only. Tilted roots retain the exact full-search fallback.</summary>
        static bool Planar(Inst inst) { Vector3 up = inst.Root.up; return Mathf.Abs(up.x) < .000001f && Mathf.Abs(up.z) < .000001f && up.y > 0f; }
        /// <summary>Fills the near-node buffer with the nodes of the 2 m buckets within radius (every node for tilted roots), in node order.</summary>
        static void Nearby(Inst inst, Vector3 p, float radius)
        {
            _nearNodes.Clear();
            if (!Planar(inst)) { for (int i = 0; i < inst.World.Length; i++) _nearNodes.Add(i); return; }
            Vector3 local = inst.Root.InverseTransformPoint(p), scale = inst.Root.lossyScale;
            float rx = radius / Mathf.Max(.0001f, Mathf.Abs(scale.x)), rz = radius / Mathf.Max(.0001f, Mathf.Abs(scale.z));
            int x0 = Mathf.FloorToInt((local.x - rx) / 2f), x1 = Mathf.FloorToInt((local.x + rx) / 2f);
            int z0 = Mathf.FloorToInt((local.z - rz) / 2f), z1 = Mathf.FloorToInt((local.z + rz) / 2f);
            if (((long)x1 - x0 + 1) * ((long)z1 - z0 + 1) > inst.G.Buckets.Count)
            {
                foreach (var kv in inst.G.Buckets)
                {
                    int x = (int)(kv.Key >> 32), z = unchecked((int)kv.Key);
                    if (x >= x0 && x <= x1 && z >= z0 && z <= z1) _nearNodes.AddRange(kv.Value);
                }
            }
            else for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
            { List<int> nodes; if (inst.G.Buckets.TryGetValue(Bucket(x, z), out nodes)) _nearNodes.AddRange(nodes); }
            _nearNodes.Sort();   // preserve node order, including the existing line-of-sight preference
        }
        /// <summary>The node the NPC stands on: within 4 m flat, from 1.2 m below to 0.6 m above its feet, level and in plain sight first.</summary>
        static int NearestStart(Inst inst, Vector3 feet)
        {
            int best = -1; float bd = float.MaxValue;
            var w = inst.World;
            Nearby(inst, feet, 4f);
            for (int pass = 0; pass < 2 && best < 0; pass++)
                foreach (int i in _nearNodes)
                {
                    float dy = w[i].y - feet.y; if (dy > .6f || dy < -1.2f) continue;
                    float fx = w[i].x - feet.x, fz = w[i].z - feet.z, flat = fx * fx + fz * fz; if (flat > 16f) continue;
                    float score = Mathf.Sqrt(flat) + Mathf.Abs(dy) * 3f; if (score >= bd) continue;
                    if (pass == 0 && !Clear(feet + Vector3.up * .3f, w[i] + Vector3.up * .3f)) continue;
                    bd = score; best = i;
                }
            return best;
        }

        /// <summary>Node nearest to a scene point within a distance (ground nodes only: flat distance), or -1.</summary>
        static int NearestWorld(Inst inst, Vector3 p, float within, bool groundOnly)
        {
            int best = -1; float bd = within * within;
            var w = inst.World;
            if (groundOnly && Planar(inst))
            {
                Vector3 local = inst.Root.InverseTransformPoint(p), scale = inst.Root.lossyScale;
                foreach (var kv in inst.G.Buckets)
                {
                    float x = (int)(kv.Key >> 32) * 2f, z = unchecked((int)kv.Key) * 2f;
                    float dx = (local.x - Mathf.Clamp(local.x, x, x + 2f)) * scale.x, dz = (local.z - Mathf.Clamp(local.z, z, z + 2f)) * scale.z;
                    if (dx * dx + dz * dz > bd) continue;
                    foreach (int i in kv.Value)
                    {
                        if (!inst.Data.Nodes[i].Terrain) continue;
                        Vector3 v = w[i] - p; v.y = 0f; float s = v.sqrMagnitude;
                        if (s < bd || (s == bd && best >= 0 && i < best)) { bd = s; best = i; }
                    }
                }
                return best;
            }
            Nearby(inst, p, within);
            foreach (int i in _nearNodes)
            {
                if (groundOnly && !inst.Data.Nodes[i].Terrain) continue;
                Vector3 v = w[i] - p; v.y *= groundOnly ? 0f : 2.5f;
                float s = v.sqrMagnitude; if (s < bd) { bd = s; best = i; }
            }
            return best;
        }

        // ------------------------------------------------------------------ A*
        private static float[] _g = new float[0]; private static int[] _came = new int[0], _cameLink = new int[0], _seen = new int[0];
        private static int _stamp;
        private static readonly List<KeyValuePair<float, int>> _open = new List<KeyValuePair<float, int>>();

        /// <summary>Steps from start to goal: >= 0 node, &lt; 0 link -(i+1) (followed by its landing node). False when unreachable.</summary>
        static bool Path(Inst inst, int start, int goal, List<int> into)
        {
            into.Clear();
            int n = inst.World.Length;
            if (_g.Length < n) { _g = new float[n]; _came = new int[n]; _cameLink = new int[n]; _seen = new int[n]; }
            _stamp++; _open.Clear();
            _g[start] = 0f; _came[start] = -1; _cameLink[start] = -1; _seen[start] = _stamp;
            Push(Vector3.Distance(inst.World[start], inst.World[goal]), start);
            int guard = 0;
            while (_open.Count > 0 && guard++ < 20000)
            {
                int cur = Pop();
                if (cur == goal) break;
                foreach (var arc in inst.G.Out[cur])
                {
                    float g = _g[cur] + arc.Cost;
                    if (_seen[arc.To] == _stamp && g >= _g[arc.To]) continue;
                    _seen[arc.To] = _stamp; _g[arc.To] = g; _came[arc.To] = cur; _cameLink[arc.To] = arc.Link;
                    Push(g + Vector3.Distance(inst.World[arc.To], inst.World[goal]), arc.To);
                }
            }
            if (_seen[goal] != _stamp)
            {
                // unreachable: as close as the known ways get (the nearest reached node to the goal, if it brings the NPC closer)
                int near = -1; float nd = Vector3.Distance(inst.World[start], inst.World[goal]) - 1f;
                for (int i = 0; i < n; i++) if (_seen[i] == _stamp) { float d = Vector3.Distance(inst.World[i], inst.World[goal]); if (d < nd) { nd = d; near = i; } }
                if (near < 0) return false;
                goal = near;
            }
            for (int c = goal; c >= 0; c = _came[c])
            {
                into.Add(c);
                if (_cameLink[c] >= 0) into.Add(-(_cameLink[c] + 1));
                if (into.Count > 4096) return false;
            }
            into.Reverse();
            return true;
        }
        /// <summary>A* open list: push (binary heap on a list).</summary>
        static void Push(float f, int i)
        {
            // small binary heap on a list
            _open.Add(new KeyValuePair<float, int>(f, i));
            int c = _open.Count - 1;
            while (c > 0) { int p = (c - 1) / 2; if (_open[p].Key <= _open[c].Key) break; var t = _open[p]; _open[p] = _open[c]; _open[c] = t; c = p; }
        }
        /// <summary>A* open list: pop the smallest key.</summary>
        static int Pop()
        {
            int r = _open[0].Value; int last = _open.Count - 1; _open[0] = _open[last]; _open.RemoveAt(last);
            int c = 0;
            while (true)
            {
                int l = c * 2 + 1, rr = l + 1, m = c;
                if (l < _open.Count && _open[l].Key < _open[m].Key) m = l;
                if (rr < _open.Count && _open[rr].Key < _open[m].Key) m = rr;
                if (m == c) break; var t = _open[m]; _open[m] = _open[c]; _open[c] = t; c = m;
            }
            return r;
        }

        // ------------------------------------------------------------------ per NPC routes
        sealed class Route
        {
            public GameObject Owner;
            public Inst Inst; public readonly List<int> Steps = new List<int>(); public int Index;
            public Vector3 Goal; public float Planned = -9f; public bool GoalInside, Pending; public int GoalNode = -1; public Graph Graph;
            public Vector3 Next; public bool HasNext; public readonly List<float> Suffix = new List<float>();
            public int DirectSurface = -1;
            public float Guided = -9f, LastCall = -9f, WatchSince; public Vector3 WatchPos;   // the stuck watchdog
            public float FeetOff = float.NaN;                                                     // pivot height above the feet
        }
        private static readonly Dictionary<int, Route> _routes = new Dictionary<int, Route>();
        private static int _planFrame = -1, _plansNow;

        /// <summary>True for human NPCs without a creature sight cone (only they use POI navigation).</summary>
        static bool Human(GameObject owner)
        {
            var a = Senses.AgentFor(owner);
            return a != null && a.Human && a.Cone == 0;
        }

        /// <summary>Nav.Next's first step. True = handled here (result in `result`); false = carry on with the grid maps.</summary>
        internal static bool TryNext(GameObject owner, Vector3 pos, Vector3 goal, out Vector3 next, out float pathLeft, out bool result)
        {
            next = goal; pathLeft = 0f; result = false;
            if (owner == null || Enabled == null || !Enabled.Value) return false;
            int id = owner.GetInstanceID();
            Route r; _routes.TryGetValue(id, out r); if (r != null) r.HasNext = false;
            if (PoiNavTraverse.Busy(owner)) { next = pos; result = true; return true; }
            if (!Human(owner)) return false;
            Rescan();
            if (_inst.Count == 0) return false;
            var inst = At(pos) ?? At(goal);
            if (inst == null) { DropRoute(owner); return false; }
            float now = Time.time;
            if (r == null) { r = new Route { Owner = owner }; _routes[id] = r; }
            bool usable = r.Inst == inst && r.Graph == inst.G && r.Index < r.Steps.Count;
            bool replan = !usable;
            int end = r.GoalNode; bool goalInside = r.GoalInside;
            if (now - r.Planned >= .5f)
            {
                goalInside = inst.Box.Contains(goal);
                end = goalInside ? NearestWorld(inst, goal, 4f, false) : -1;
                if (end < 0) end = NearestWorld(inst, goal, 1000f, true);
                // Preserve melee/ranged destinations while the player is on a ladder.
                int lad = PlayerLadderLink(inst, goal);
                if (lad >= 0 && inst.G.LinkFrom[lad] >= 0 && inst.G.LinkTo[lad] >= 0)
                {
                    int lf = inst.G.LinkFrom[lad], lt = inst.G.LinkTo[lad];
                    if (Brain.IsRanged(owner)) end = Mathf.Abs(pos.y - inst.World[lf].y) <= Mathf.Abs(pos.y - inst.World[lt].y) ? lf : lt;
                    else end = pos.y < goal.y ? lt : lf;
                    goalInside = true;
                }
                replan |= end != r.GoalNode;
                if (usable)
                {
                    int s = r.Steps[r.Index]; Vector3 tp = s >= 0 ? inst.World[s] : StartOf(inst, s);
                    if ((tp - pos).sqrMagnitude > 36f) { replan = true; usable = false; }
                }
            }
            if (_planFrame != Time.frameCount) { _planFrame = Time.frameCount; _plansNow = 0; }
            if (r.DirectSurface >= 0 && r.Inst == inst && r.Graph == inst.G && end == r.GoalNode && Services.PlayerLadder == null)
            {
                var feet = pos - Vector3.up * r.FeetOff; int near = NearestWorld(inst, feet, 4f, false);
                if (near >= 0 && inst.Data.Nodes[near].Surface == r.DirectSurface && Mathf.Abs(inst.World[near].y - feet.y) < .8f && Clear(pos, goal)) return false;
                r.DirectSurface = -1;
            }
            if (replan && (now - r.Planned < .5f || _plansNow >= 2))
            {
                if (!usable)
                {
                    if (!r.Pending && r.Inst == inst && r.Graph == inst.G && r.Steps.Count == 0 && now - r.Planned < .5f) return false;
                    r.Inst = inst; r.Graph = inst.G; r.Steps.Clear(); r.Index = 0; r.Pending = true;
                    r.Next = next = pos; r.HasNext = true; r.Guided = now; pathLeft = Vector3.Distance(pos, goal); result = true; return true;
                }
                replan = false;   // follow the old route until a search slot is available
            }
            if (replan)
            {
                if (float.IsNaN(r.FeetOff)) { var rb = owner.GetComponent<Rigidbody>(); r.FeetOff = rb != null ? PoiNavTraverse.FeetOffset(owner, rb) : 0f; }
                r.Inst = inst; r.Graph = inst.G; r.Goal = goal; r.GoalNode = end; r.GoalInside = goalInside; r.Planned = now; r.Index = 0; r.Pending = false; r.DirectSurface = -1;
                // (WW) the node it STANDS on: its own level (never a roof or ledge above its head, which made it snap up onto a car
                // and take that car's drop back down, over and over), preferring one in plain sight
                int start = NearestStart(inst, pos - Vector3.up * r.FeetOff);
                if (start < 0) start = NearestWorld(inst, pos, 4f, false);
                if (start < 0) { r.Steps.Clear(); return false; }
                if (end < 0) { r.Steps.Clear(); return false; }
                _plansNow++;
                if (!Path(inst, start, end, r.Steps)) { r.Steps.Clear(); return false; }
                // no traversal on the way and the goal in plain sight: the feelers do this best
                bool links = false; foreach (int s in r.Steps) if (s < 0) { links = true; break; }
                if (!links && inst.Data.Nodes[start].Surface == inst.Data.Nodes[end].Surface && Clear(pos, goal)) { r.DirectSurface = inst.Data.Nodes[start].Surface; r.Steps.Clear(); return false; }
                r.Suffix.Clear(); for (int k = 0; k < r.Steps.Count; k++) r.Suffix.Add(0f);
                for (int k = r.Steps.Count - 2; k >= 0; k--)
                {
                    int a = r.Steps[k], b = r.Steps[k + 1];
                    r.Suffix[k] = r.Suffix[k + 1] + Vector3.Distance(a >= 0 ? inst.World[a] : EndOf(inst, a), b >= 0 ? inst.World[b] : EndOf(inst, b));
                }
                for (int k = 1; k < r.Steps.Count && k <= 5; k++)
                {
                    int s2 = r.Steps[k]; if (s2 < 0 || inst.Data.Nodes[s2].Surface != inst.Data.Nodes[start].Surface || !Clear(pos, inst.World[s2])) break;
                    r.Index = k;
                }
            }
            if (r.Steps.Count == 0) return false;
            // advance along the steps; a link starts its traversal once the NPC stands at its start
            while (r.Index < r.Steps.Count)
            {
                int s = r.Steps[r.Index];
                if (s >= 0)
                {
                    Vector3 tp = inst.World[s];
                    Vector3 d = tp - (pos - Vector3.up * r.FeetOff); float dy = Mathf.Abs(d.y); d.y = 0f;
                    if (d.sqrMagnitude < .64f && dy < .8f) { r.Index++; continue; }   // reached (on its level, not under it)
                    // skip ahead to a later node of the same surface in plain sight (smoother corners)
                    for (int k = r.Index + 1; k < r.Steps.Count && k <= r.Index + 3; k++)
                    {
                        int s2 = r.Steps[k]; if (s2 < 0) break;
                        if (inst.Data.Nodes[s2].Surface != inst.Data.Nodes[s].Surface || !Clear(pos, inst.World[s2])) break;
                        r.Index = k; s = s2;
                    }
                    next = inst.World[s];
                    break;
                }
                Vector3 from = StartOf(inst, s);
                Vector3 df = from - (pos - Vector3.up * r.FeetOff); float fy = Mathf.Abs(df.y); df.y = 0f;
                // close enough with nothing in between, ON THE SAME LEVEL as the link start: the traversal snaps the NPC onto it
                if (df.sqrMagnitude < 1.6f * 1.6f && fy < .6f && (df.sqrMagnitude < .36f || Clear(pos, from)))
                {
                    if (PoiNavTraverse.Begin(owner, inst, (-s - 1) >> 1, ((-s - 1) & 1) == 1)) { r.Index++; next = pos; result = true; return true; }
                    r.Guided = now; r.WatchPos = pos; r.WatchSince = now;      // the ladder is taken: wait here (waiting is not being stuck)
                    r.Next = pos; r.HasNext = true;
                    next = pos; result = true; return true;
                }
                next = from;
                break;
            }
            if (r.Index >= r.Steps.Count) { r.Steps.Clear(); r.Index = 0; return false; }   // retain the replan cooldown after arrival
            pathLeft = Vector3.Distance(pos, next);
            pathLeft += r.Suffix[r.Index];
            if (r.Steps[r.Index] < 0 && r.Index + 1 < r.Steps.Count)
            {
                int s = r.Steps[r.Index + 1]; Vector3 p = s >= 0 ? inst.World[s] : EndOf(inst, s);
                pathLeft = Vector3.Distance(pos, next) + Vector3.Distance(next, p) + r.Suffix[r.Index + 1];
            }
            int last = r.Steps[r.Steps.Count - 1];
            if (!r.GoalInside) pathLeft += Vector3.Distance(last >= 0 ? inst.World[last] : EndOf(inst, last), goal);
            r.Next = next; r.HasNext = true;
            r.Guided = now;
            result = true;
            return true;
        }

        /// <summary>a link step (-(code + 1), code = link * 2 + backwards): where it starts / lands</summary>
        static Vector3 StartOf(Inst inst, int step) { int code = -step - 1; var l = inst.Data.Links[code >> 1]; return inst.W((code & 1) == 0 ? l.From : l.To); }
        /// <summary>Scene point where a link step lands.</summary>
        static Vector3 EndOf(Inst inst, int step) { int code = -step - 1; var l = inst.Data.Links[code >> 1]; return inst.W((code & 1) == 0 ? l.To : l.From); }

        /// <summary>the ladder link the player is on (his goal is the player, he climbs one of this POI's ladders), or -1</summary>
        static int PlayerLadderLink(Inst inst, Vector3 goal)
        {
            var player = GameRefs.Player;
            if (Services.PlayerLadder == null || player == null) return -1;
            Vector3 p = player.transform.position;
            if ((goal - p).sqrMagnitude > 4f) return -1;
            int best = -1; float bd = 1.5f * 1.5f;
            for (int i = 0; i < inst.Data.Links.Count; i++)
            {
                var l = inst.Data.Links[i]; if (l.Off || l.Kind != PoiLinkKind.Ladder) continue;
                Vector3 b = inst.W(l.LineBottom), t = inst.W(l.LineTop);
                if (p.y < b.y - 1f || p.y > t.y + 2.5f) continue;
                Vector3 on = Vector3.Lerp(b, t, Mathf.InverseLerp(b.y, t.y, p.y));
                float d = new Vector2(p.x - on.x, p.z - on.z).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>True when the line 0.6 m above both points hits nothing solid.</summary>
        static bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 up = Vector3.up * .6f;
            return !Physics.Linecast(a + up, b + up, Layers.NavSolid, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ lifecycle
        /// <summary>Drops a POI type's cached data and every route (after an edit), then rescans.</summary>
        internal static void Forget(string poi)
        {
            PoiNavStore.Forget(poi);
            _routes.Clear();
            Rescan(true);
        }
        /// <summary>Old NPC-entry origin shift: forwards to <see cref="ShiftCore"/> unless the NPCAI entry shifts navigation.</summary>
        [System.Obsolete("refactor: NPCAIModule runs navigation (NavRuntime); no-op while it does")]
        internal static void Shift(Vector3 delta) { if (!NavRuntime.Active) ShiftCore(delta); }

        /// <summary>Floating origin: instance points, boxes, routes and running traversals move by delta.</summary>
        internal static void ShiftCore(Vector3 delta)
        {
            foreach (var i in _inst.Values)
            {
                if (i.World != null) for (int k = 0; k < (i.Ready ? i.World.Length : i.BuildIndex); k++) i.World[k] += delta;
                i.Box.center += delta; i.RootPos += delta;
            }
            foreach (var r in _routes.Values) { r.Goal += delta; r.Next += delta; }
            PoiNavTraverse.Shift(delta);
        }
        /// <summary>Old NPC-entry reset: forwards to <see cref="ResetCore"/> unless the NPCAI entry owns navigation.</summary>
        [System.Obsolete("refactor: NPCAIModule runs navigation (NavRuntime); no-op while it does")]
        internal static void Reset() { if (!NavRuntime.Active) ResetCore(); }

        /// <summary>Releases walkers and traversals and forgets every instance, route, graph and loaded data (scene load, start/stop).</summary>
        internal static void ResetCore()
        {
            foreach (int id in new List<int>(_walkers.Keys)) Release(id);
            PoiNavTraverse.Clear();
            _inst.Clear(); _routes.Clear(); _graphs.Clear(); PoiNavStore.Forget(); _nextScan = 0f;
            _nearNodes.Clear(); _planFrame = -1; _plansNow = 0;
            _builds.Clear(); _buildFrame = -1; _buildNodes = _buildRays = 0;
        }
    }
}
