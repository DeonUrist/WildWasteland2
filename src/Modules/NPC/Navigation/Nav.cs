using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.NPC
{
    // Structure navigation: NPCs know the camps, buildings and caves they are in.
    //
    // Native camps, buildings, caves, wrecks and Starter_Area, plus tagged custom static POIs, are found by a slow
    // incremental sweep of the scene (MapMagic places them itself). Each is baked before the player could get near enough for his noise to
    // reach its NPCs (Pick: distance, the fastest he moved lately, the bake's estimated time), or when he comes within [Nav] BakeRange:
    // its footprint (the union of its solid colliders + Margin) is baked into a walkability grid
    // (CellSize, prefab-aligned): per cell one downward ray for the floor nearest the structure's base height (caves have a roof above the
    // floor), then a body-sized capsule from ankle (0.2 m) to head (1.7 m) height must be free of anything solid except cars, loose items
    // and creatures - so spikes at a cave mouth, a brazier, crates and walls are obstacles, the clean opening is not. Neighbouring cells
    // connect when their floors differ by at most MaxStep. Baking and map I/O are sliced; compatible prefab maps persist in NPC/NavMaps.
    //
    // Routing: an NPC standing inside a baked footprint whose goal (target or ghost) is not in straight sight on the grid follows a
    // distance field built for that goal (Dijkstra over the grid: from the goal cell when the goal is inside, otherwise from every edge
    // cell weighted by its distance to the goal, so the NPC leaves through the exit that is shortest overall; since 1.6.0 an outside goal
    // goes to ONE exit cell with a clear line out, routed as an inside goal, then straight - see OutNext). The next waypoint is the
    // farthest cell along the descent that is in grid sight, handed to the brain's feelers as a waypoint. Fields are cached per goal
    // (shared by every NPC heading there) for FieldSeconds. Outside any footprint nothing runs.
    internal static partial class Nav
    {
        // (WW) the roots of every known structure (POI navigation instances follow them)
        internal static void Roots(List<Transform> into) { into.Clear(); foreach (var t in _structures) if (t.Root != null) into.Add(t.Root); }

        internal sealed class Structure
        {
            public Transform Root; public string Name;
            public Bounds Box;                      // world bounds of the footprint incl. margin
            public float Cell, RefY; public int W, H;
            public Vector3 GridOrigin, GridRight, GridForward;
            public string MapFile, VanillaMapFile, LegacyMapFile, MapSource; public ulong MapKey; public Collider[] MapColliders;
            public float MapStep;
            public MapIo MapJob; public bool LoadedMap, LoadedVanilla, SkipVanilla, ReplaceMap;
            public bool LoadedTemplate, MapSaved;   // (WW perf) arrays cloned from the in-memory template of the same map; map written this session
            public float[] FloorY;                  // NaN = blocked
            public bool Baked; public int Next;     // bake progress (cell index)
            public int Walkable, NoFloor, Tight, Solid, ScanLimit; public float BakeMs; public int BakeFrames;
            public byte[] Why;                      // per cell: 0 walkable, 1 no floor, 2 too tight, 3 inside rock
            public bool[] Open;                     // walkable cell with open sky above (not under a cave roof / building)
            public bool[] Near;                     // (1.4.11) walkable cell next to a body-height obstacle (too tight / rock): routes avoid it, walked centre to centre
            public int EdgeCells, EdgeWalkable;     // the outer ring of the footprint
            public Dictionary<int, int> FloorHits;  // collider id -> walkable cells it is the floor of (during the bake)
            public float NextDump;
            public byte[] Edges;                    // per cell, 1 = open edge toward +x (bit 0), +z (1), +x+z (2), -x+z (3): no wall or spike between the two cells;
                                                    // bits 4 (+x) and 5 (+z): a HOP edge - a low lip / kerb / step of MaxStep..HopStep a body hops over
            public int[] Comp; public int[] CompSize;   // connected areas (flood fill over open edges) and their sizes
            public float StepAtBake;                // MaxStep the edges and areas were baked with (Next's area short cut holds while it is not lowered)
            public int Phase;                       // bake: 0 floors, 1 edges, 2 connected areas (sliced too), then Baked
            public int CompScan, CompCur, CompCurSize; public Stack<int> CompStack; public List<int> CompSizes;   // phase 2 progress
            public readonly Dictionary<long, Field> Fields = new Dictionary<long, Field>();
            public Core.NavStructure Pub;                // (WW) the Core.Services.Npc view of it (reused, refreshed by Structures())
            // (WW perf, N2) per cell, bit b = the move to neighbour b (loop order dz -1..1, dx -1..1) is allowed (Move: step + no corner
            // cutting) / is a hop; made after the bake for MaxStep = MaskStep, used while that holds and no _relax / _noHop is on
            public byte[] Mv, Hop; public float MaskStep;
            public byte[] MvB, HopB; public float MaskStepB; public int MaskNext;     // the masks being made (a slice per frame)
        }

        // (WW perf) a route field is expanded lazily: Build stops once the asking cell is settled and keeps its heap (H) to resume later
        // (Expand); Settled = the key of the last cell popped: every cell with Dist <= Settled is final (Dijkstra), H == null = complete
        // (WW perf) Pin: a field a sliced exit pick (PickJob) is still expanding / reading - never evicted while pinned
        internal sealed class Field { public float[] Dist; public float Made; public bool GoalInside; public int GoalCell; public Heap H; public float Settled; public bool Pin; }
        // distance arrays are big (W*H floats); fields that expire give theirs back here, Build takes one from here when it fits
        // (WW perf) pools of 32 (was 8: with 32 cached fields per structure an eviction burst threw most arrays away as garbage)
        private const int PoolCap = 32;
        private static readonly Dictionary<int, Stack<float[]>> _pool = new Dictionary<int, Stack<float[]>>();
        private static readonly Stack<Heap> _heaps = new Stack<Heap>();
        private static float[] Rent(int n)
        {
            Stack<float[]> st;
            if (_pool.TryGetValue(n, out st) && st.Count > 0) return st.Pop();
            return new float[n];
        }
        private static void Return(Field f)
        {
            if (f == null || f.Dist == null) return;
            Stack<float[]> st;
            if (!_pool.TryGetValue(f.Dist.Length, out st)) _pool[f.Dist.Length] = st = new Stack<float[]>();
            if (st.Count < PoolCap) st.Push(f.Dist);
            f.Dist = null; f.Pin = false;
            if (f.H != null) { if (_heaps.Count < PoolCap) _heaps.Push(f.H); f.H = null; }
        }
        // (WW perf) the per-structure cache: entries older than FieldSeconds go once there are 16; at 32 the least recently MADE one goes, one
        // at a time (was: everything cleared at 32 - a rebuild storm for every NPC on the next think)
        private static void MakeRoom(Structure s, float now, float life)
        {
            if (s.Fields.Count < 16) return;
            _expired.Clear();
            foreach (var kv in s.Fields) if (now - kv.Value.Made >= life && !kv.Value.Pin) _expired.Add(kv.Key);
            foreach (var k in _expired) { Return(s.Fields[k]); s.Fields.Remove(k); }
            while (s.Fields.Count >= 32)
            {
                long oldest = 0; float om = float.MaxValue; bool any = false;
                foreach (var kv in s.Fields) if (!kv.Value.Pin && kv.Value.Made < om) { om = kv.Value.Made; oldest = kv.Key; any = true; }
                if (!any) break;
                Return(s.Fields[oldest]); s.Fields.Remove(oldest);
            }
        }
        private static readonly List<long> _expired = new List<long>();
        internal static bool LogOn { get { return NpcModule.BrainLog != null && NpcModule.BrainLog.Value; } }

        private static readonly List<Structure> _structures = new List<Structure>();
        private static readonly HashSet<int> _known = new HashSet<int>();
        private static Structure _baking;
        private static float _nextPick;
        private static readonly Stopwatch _sw = new Stopwatch();
        // solid for baking: everything the feelers see, minus cars (8) and loose items (9) - those move
        private static readonly int BakeMask = ~((1 << 1) | (1 << 2) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 7) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 12) | (1 << 13) | (1 << 15) | (1 << 17) | (1 << 19) | (1 << 22));
        private const float Radius = 0.28f, Ankle = 0.2f, HeadTop = 1.5f;   // a human NPC: capsule r 0.28, 1.5 m tall
        // (1.4.11) clearance: from knee height (0.3 m) up the map tests a body 7 cm fatter than the real one - an NPC is never exactly on a
        // cell centre, and a spike passing 3 cm from the centre stopped the body 15 cm off it. Gaps under ~0.7 m at body height close.
        private const float BodyR = 0.35f, BodyLo = 0.3f;
        private const float NearCost = 1f;       // entering a cell next to an obstacle costs like 1 m more: routes keep to open ground
        private const int MinArea = 30;          // connected areas smaller than this (7.5 m2) are noise next to props: never start or end a route there
        internal const float HopStep = 0.5f;     // a floor step / lip up to this high is crossed with a hop (the brain's hop clears ~0.5 m)
        private const float HopCost = 2f;        // a hop edge costs like 2 m more walking: the map prefers a flat way when there is one
        private static bool _noHop;              // patrol / search points must be reachable without hops
        private static readonly HashSet<int> _floorCols = new HashSet<int>();   // colliders that are the floor of >= FloorColCells map cells (cave floors, camp decks)
        private const int FloorColCells = 40;

        internal static bool On { get { return NpcModule.Running && NpcModule.NavEnabled.Value; } }

        public static void OnSceneLoaded() { ResetMapHighlight(); foreach (var s in _structures) CloseMap(s); _needed = null; Needed = false; _structures.Clear(); _known.Clear(); _baking = null; _debug.Clear(); _floorCols.Clear(); _exits.Clear(); _relaxedFields.Clear(); _pool.Clear(); _heaps.Clear(); _player = null; _sweep.Clear(); _swept = false; _nodeKind.Clear(); _outPlans.Clear(); _outMemo.Clear(); _compRing.Clear(); PickReset(); _maskQueue.Clear(); }

        // ---------- discovery + baking (per frame) ----------
        public static void Tick()
        {
            TickMapHighlight();
            if (!On) return;
            float now = Time.unscaledTime;
            var player = Player();
            try { PickStep(); } catch (Exception e) { NpcModule.Log.LogError("Nav: exit pick: " + e); PickReset(); }   // (WW perf) the sliced exit pick (T5)
            try { MaskTick(); } catch (Exception e) { NpcModule.Log.LogError("Nav: move masks: " + e); _maskQueue.Clear(); }   // (WW perf) the move masks (N2)
            if (player == null) return;
            float udt = Time.unscaledDeltaTime;
            if (udt > 0f && udt < 0.5f) _dtAvg = _dtAvg <= 0f ? udt : _dtAvg + (udt - _dtAvg) * 0.1f;
            // discovery: a slow walk over the scene hierarchy, a few hundred nodes per frame (each node's name read once and remembered by id)
            try { SweepStep(now, player.position); } catch (Exception e) { NpcModule.Log.LogError("Nav: discovery: " + e); _sweep.Clear(); _nextSweep = now + 5f; }
            if (now >= _nextPick) { _nextPick = now + 0.5f; Pick(player.position, now); }
            if (_baking != null)
            {
                try { BakeStep(_baking); }
                catch (Exception e) { NpcModule.Log.LogError("Nav: bake of " + _baking.Name + " failed: " + e); CloseMap(_baking); _baking.Baked = true; _baking.FloorY = null; _baking = null; }
            }
        }

        // ---------- which structure to bake, and how fast ----------
        // (1.1.0) Simple: a bake, once started, is finished - dropped only when its root is gone or it is farther than FarDrop. Next in line:
        // the structure an NPC stands in without a map (Needed, set by Next()), else the nearest unbaked one within BakeRange x 3 of the
        // player. (The old scheduler predicted the player's speed and each bake's duration and switched whenever another structure looked
        // 5 s more urgent: while driving or flying none of ~100 found structures ever finished - probe 2026-10-06.)
        private static float _dtAvg, _msPerCell = 0.026f;      // _msPerCell: measured bake cost, for the log
        private static bool _urgent;
        private const float NearPreempt = 60f, FarDrop = 1500f;
        internal static bool Needed;      // set by Next(): an NPC asked for a route inside a structure that is not baked yet
        private static Structure _needed;
        private static float Dist(Structure s, Vector3 pp)
        {
            Vector3 c = s.Box.ClosestPoint(new Vector3(pp.x, s.Box.center.y, pp.z));
            return Mathf.Sqrt((c.x - pp.x) * (c.x - pp.x) + (c.z - pp.z) * (c.z - pp.z));
        }
        private static void Pick(Vector3 pp, float now)
        {
            if (_needed != null && (_needed.Baked || _needed.Root == null)) _needed = null;
            float curD = _baking != null && _baking.Root != null ? Dist(_baking, pp) : float.MaxValue;
            if (_baking != null && (_baking.Root == null || _baking.Baked || curD > FarDrop)) { _baking = null; curD = float.MaxValue; }
            Structure take = null;
            if (_needed != null && _needed != _baking) take = _needed;                                   // an NPC is waiting for it
            else if (_baking == null || curD > NearPreempt)
            {
                // the nearest unbaked structure within reach; it takes over a running bake only when the player is at it
                Structure best = null; float bd = NpcModule.NavBakeRange.Value * 3f;
                for (int i = 0; i < _structures.Count; i++)
                {
                    var s = _structures[i];
                    if (s.Baked || s.Root == null || s == _baking) continue;
                    float d = Dist(s, pp);
                    if (d < bd) { bd = d; best = s; }
                }
                if (best != null && (_baking == null || bd <= NearPreempt)) take = best;
            }
            if (take != null && take != _baking)
            {
                if (_baking != null && NpcModule.NavLog.Value) NpcModule.Log.LogInfo("Nav: pauses the bake of " + _baking.Name + " for " + take.Name);
                _baking = take;
                if (take.FloorY == null) BeginBake(take);        // else: resumes where it stopped
                curD = Dist(take, pp);
            }
            _urgent = _baking != null && curD <= NearPreempt;     // the player is at it: faster
            Needed = _baking != null && _baking == _needed;      // an NPC is waiting for it: fastest
        }

        private static float BaseBudget() { return Mathf.Max(0.2f, NpcModule.NavBakeBudgetMs.Value); }

        // this frame's slice: [Nav] BakeBudgetMs; x4 when an NPC waits for the map, x3 when the player is at the structure, x2 when frames
        // are cheap (above ~90 fps), x4 while the game is paused; halved below ~45 fps
        private static float FrameBudget()
        {
            float b = BaseBudget(), dt = _dtAvg > 0f ? _dtAvg : 1f / 60f;
            if (Time.timeScale <= 0f) return b * 4f;
            if (dt > 1f / 45f) return b * 0.5f;
            if (Needed && dt < 1f / 40f) return b * 4f;
            if (_urgent && dt < 1f / 50f) return b * 3f;
            if (dt < 1f / 90f) return b * 2f;
            return b;
        }

        // ---------- discovery: an incremental sweep ----------
        private static readonly Stack<KeyValuePair<Transform, int>> _sweep = new Stack<KeyValuePair<Transform, int>>();
        private static readonly Dictionary<int, byte> _nodeKind = new Dictionary<int, byte>();   // 1 descend, 2 leaf (body / too big), 3 structure
        private static float _nextSweep; private static int _sweepAdded;
        private const int SweepNodesPerFrame = 250;

        // (WW perf) after a full pass the next one waits 15 s, or 2 s once the player is > 30 m from where the last pass began (new MapMagic
        // tiles come with movement; structures made by CreateObject are taken at once by Spawned)
        private static Vector3 _sweepFrom; private static bool _swept;
        private static void SweepStep(float now, Vector3 pp)
        {
            if (_sweep.Count == 0)
            {
                if (now < _nextSweep) return;
                if (_swept && now < _nextSweep + 13f && (pp - _sweepFrom).sqrMagnitude <= 30f * 30f) return;
                _sweepFrom = pp; _swept = true;
                foreach (var s in _structures) if (s.Root == null) CloseMap(s);
                int gone = _structures.RemoveAll(s => s.Root == null);      // despawned camps: drop their grids and cached routes
                if (gone > 0) { _compRing.Clear(); if (NpcModule.NavLog.Value) NpcModule.Log.LogInfo("Nav: " + gone + " structure(s) gone"); }   // _compRing holds whole structures (grids)
                if (_baking != null && _baking.Root == null) _baking = null;
                if (_nodeKind.Count > 200000) _nodeKind.Clear();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (!sc.isLoaded) continue;
                    _roots.Clear(); sc.GetRootGameObjects(_roots);
                    foreach (var r in _roots) _sweep.Push(new KeyValuePair<Transform, int>(r.transform, 0));
                }
                if (_sweep.Count == 0) { _nextSweep = now + 2f; return; }
            }
            int budget = SweepNodesPerFrame;
            while (_sweep.Count > 0 && budget-- > 0)
            {
                var e = _sweep.Pop();
                var t = e.Key;
                if (t == null) continue;
                int id = t.GetInstanceID();
                byte k;
                if (!_nodeKind.TryGetValue(id, out k))
                {
                    k = IsStructure(t) ? (byte)3 : t.GetComponent<Rigidbody>() != null ? (byte)2 : (byte)1;   // creatures, cars, items never hold structures
                    _nodeKind[id] = k;
                }
                if (k == 3) { if (!_known.Contains(id) && t.gameObject.activeInHierarchy) _sweepAdded += Scan(t, 0); continue; }
                if (k == 2 || e.Value >= 4) continue;
                int cc = t.childCount;
                if (cc > 3000) continue;
                for (int i = 0; i < cc; i++) _sweep.Push(new KeyValuePair<Transform, int>(t.GetChild(i), e.Value + 1));
            }
            if (_sweep.Count == 0)
            {
                if (_sweepAdded > 0 && NpcModule.NavLog.Value) NpcModule.Log.LogInfo("Nav: " + _sweepAdded + " new structure(s), " + _structures.Count + " known");
                _sweepAdded = 0;
                _nextSweep = now + 2f;      // a full pass takes a few dozen frames; then a short pause
            }
        }

        private static Transform _player; private static float _nextPlayer;
        internal static Transform Player()
        {
            if (_player == null && Time.unscaledTime >= _nextPlayer) { _nextPlayer = Time.unscaledTime + 2f; var g = Core.GameRefs.Player; _player = g != null ? g.transform : null; }
            return _player;
        }

        // Native names and the game's static-POI root tag allow custom structures without scanning their geometry every frame.
        private static readonly List<GameObject> _roots = new List<GameObject>();
        // a structure instantiated with CreateObject: taken at once (MapMagic places its camps itself - those the sweep finds)
        internal static void Spawned(GameObject go)
        {
            if (go == null || !On || !IsStructure(go.transform)) return;
            Scan(go.transform, 0);
        }

        private static int Scan(Transform t, int depth)
        {
            if (IsStructure(t))
            {
                if (!t.gameObject.activeInHierarchy || _known.Contains(t.GetInstanceID())) return 0;
                var s = Make(t);
                if (s == null) return 0;
                _known.Add(t.GetInstanceID());
                _structures.Add(s);
                if (NpcModule.NavLog.Value) NpcModule.Log.LogInfo("Nav: found " + Path(t) + " at " + t.position + ", footprint " + s.Box.size.x.ToString("0") + " x " + s.Box.size.z.ToString("0") + " m");
                return 1;
            }
            if (depth >= 4 || t.childCount > 3000) return 0;
            if (t.GetComponent<Rigidbody>() != null) return 0;       // creatures, cars, items: never contain structures
            int n = 0;
            for (int i = 0; i < t.childCount; i++) n += Scan(t.GetChild(i), depth + 1);
            return n;
        }

        private static bool IsStructure(string name)
        {
            if (name == "Starter_Area" || name.StartsWith("Starter_Area(", StringComparison.Ordinal) || name.StartsWith("Starter_Area ", StringComparison.Ordinal)) return true;
            string p = name.StartsWith("Camp_", StringComparison.Ordinal) ? "Camp_" : name.StartsWith("Building_", StringComparison.Ordinal) ? "Building_" : name.StartsWith("Cave_", StringComparison.Ordinal) ? "Cave_" : name.StartsWith("Wreck_", StringComparison.Ordinal) ? "Wreck_" : null;
            if (p == null || name.Length <= p.Length || !char.IsDigit(name[p.Length])) return false;
            for (int i = p.Length; i < name.Length; i++) { char c = name[i]; if (!char.IsDigit(c)) return c == '(' || c == ' '; }
            return true;
        }

        private static bool IsStructure(Transform t)
        {
            return IsStructure(t.name) || (t.gameObject.layer != 22 && t.CompareTag("SpawnerStatic") && t.GetComponent<Rigidbody>() == null);
        }

        private static string Path(Transform t) { return t.parent != null ? t.parent.name + "/" + t.name : t.name; }

        private static Structure Make(Transform root)
        {
            bool any = false; Bounds b = new Bounds();
            var colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
            {
                if (c == null || c.isTrigger || !c.enabled) continue;
                int l = c.gameObject.layer;
                if (((1 << l) & BakeMask) == 0) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            if (!any) return null;
            float margin = Mathf.Max(1f, NpcModule.NavMargin.Value);
            b.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
            float cell = Mathf.Max(0.25f, NpcModule.NavCellSize.Value);
            float maxSide = Mathf.Max(b.size.x, b.size.z);
            if (maxSide / cell > 600f) cell = maxSide / 600f;           // very large footprints get coarser cells (<= 600 x 600)
            var s = new Structure { Root = root, Name = root.name, Box = b, Cell = cell };
            s.W = Mathf.Max(2, Mathf.CeilToInt(b.size.x / cell)); s.H = Mathf.Max(2, Mathf.CeilToInt(b.size.z / cell));
            s.GridOrigin = b.min; s.GridRight = Vector3.right; s.GridForward = Vector3.forward;
            ConfigureMap(s, colliders, margin);
            return s;
        }

        private static void BeginBake(Structure s, bool loadMap = true)
        {
            s.FloorY = new float[s.W * s.H]; s.Why = new byte[s.W * s.H]; s.Open = new bool[s.W * s.H]; s.FloorHits = new Dictionary<int, int>();
            s.Edges = new byte[s.W * s.H]; s.Comp = null; s.CompSize = null; s.Phase = 0; s.CompStack = null; s.CompSizes = null;
            s.Next = 0; s.Walkable = 0; s.NoFloor = 0; s.Tight = 0; s.Solid = 0; s.ScanLimit = 0; s.BakeMs = 0f; s.BakeFrames = 0; s.StepAtBake = Mathf.Max(0.1f, NpcModule.NavMaxStep.Value);
            // the base height: the floor under the structure's pivot (a ray from well above, first walkable surface below the pivot + 2 m)
            Vector3 p = s.Root.position;
            s.RefY = p.y;
            if (loadMap && OpenMap(s)) return;
            RaycastHit h;
            if (Physics.Raycast(new Vector3(p.x, p.y + 2f, p.z), Vector3.down, out h, 12f, BakeMask, QueryTriggerInteraction.Ignore)) s.RefY = h.point.y;
        }

        private static void BakeStep(Structure s)
        {
            if (s.Root == null) { _baking = null; return; }
            _sw.Reset(); _sw.Start();
            float budget = FrameBudget();
            int n = s.W * s.H;
            if (s.Phase == -1) { ReadMap(s, budget); return; }
            if (s.Phase == 3) { if (WriteMap(s, budget)) Finish(s); return; }
            if (s.Phase == 1) { BakeEdges(s, budget); return; }
            if (s.Phase == 2)
            {
                bool done = ComponentsStep(s, budget);
                _sw.Stop();
                s.BakeMs += (float)_sw.Elapsed.TotalMilliseconds; s.BakeFrames++;
                if (done && (s.LoadedMap || !CreateMap(s))) Finish(s);
                return;
            }
            while (s.Next < n && _sw.Elapsed.TotalMilliseconds < budget)
            {
                int i = s.Next++;
                Collider fc; byte why; bool open;
                s.FloorY[i] = Floor(s, i % s.W, i / s.W, out fc, out why, out open);
                s.Why[i] = why; s.Open[i] = open;
                if (!float.IsNaN(s.FloorY[i]))
                {
                    s.Walkable++;
                    if (fc != null) { int id = fc.GetInstanceID(), c; s.FloorHits.TryGetValue(id, out c); s.FloorHits[id] = c + 1; }
                }
            }
            _sw.Stop();
            s.BakeMs += (float)_sw.Elapsed.TotalMilliseconds; s.BakeFrames++;
            if (s.Next >= n) { s.Phase = 1; s.Next = 0; }
        }

        // second pass: which neighbouring walkable cells can a body walk between - a thin wall, a cave's rock shell or a spike between two
        // free cells blocks the edge (two lines, knee and chest high, with back faces on so a one-sided mesh blocks from both sides)
        private static readonly int[] EdgeDx = { 1, 0, 1, -1 }, EdgeDz = { 0, 1, 1, 1 };
        private static void BakeEdges(Structure s, float budget)
        {
            int n = s.W * s.H;
            bool old = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                while (s.Next < n && _sw.Elapsed.TotalMilliseconds < budget)
                {
                    int i = s.Next++;
                    float ya = s.FloorY[i];
                    if (float.IsNaN(ya)) continue;
                    int x = i % s.W, z = i / s.W; byte bits = 0;
                    Vector3 a = CellCenter(s, x, z, ya);
                    for (int k = 0; k < 4; k++)
                    {
                        int cx = x + EdgeDx[k], cz = z + EdgeDz[k];
                        if (cx < 0 || cz < 0 || cx >= s.W || cz >= s.H) continue;
                        float yb = s.FloorY[cz * s.W + cx];
                        if (float.IsNaN(yb)) continue;
                        float dy = Mathf.Abs(ya - yb);
                        Vector3 b = CellCenter(s, cx, cz, yb);
                        if (dy <= Mathf.Max(0.1f, NpcModule.NavMaxStep.Value)
                            && LowWayClear(a, b, Mathf.Max(ya, yb))                                                                      // a low rock lip / kerb
                            && BodySweep(a, b, Mathf.Max(ya, yb), 0.3f))                                                                 // the whole body, knee to shoulders
                        { bits |= (byte)(1 << k); continue; }
                        // not walkable: a hop edge? (straight neighbours only) a step / lip up to HopStep with the body's way clear above it
                        if (k > 1 || dy > HopStep) continue;
                        float top = Mathf.Max(ya, yb);
                        Vector3 a2 = new Vector3(a.x, top, a.z), b2 = new Vector3(b.x, top, b.z);
                        if (!BodySweep(a2, b2, top, 0.6f)) continue;
                        bits |= (byte)(1 << (k + 4));
                    }
                    s.Edges[i] = bits;
                }
            }
            finally { Physics.queriesHitBackfaces = old; }
            _sw.Stop();
            s.BakeMs += (float)_sw.Elapsed.TotalMilliseconds; s.BakeFrames++;
            if (s.Next >= n) { s.Phase = 2; s.Next = 0; }     // the connected areas next frame, sliced as well
        }

        private static void Finish(Structure s)
        {
            int n = s.W * s.H;
            {
                // (1.4.11) cells next to a body-height obstacle (8 neighbours too tight / inside rock)
                s.Near = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    if (float.IsNaN(s.FloorY[i])) continue;
                    int x = i % s.W, z = i / s.W;
                    for (int dz = -1; dz <= 1 && !s.Near[i]; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int cx = x + dx, cz = z + dz;
                            if (cx < 0 || cz < 0 || cx >= s.W || cz >= s.H) continue;
                            byte w = s.Why[cz * s.W + cx];
                            if (w == 2 || w == 3) { s.Near[i] = true; break; }
                        }
                }
                s.Baked = true; _baking = null;
                QueueMasks(s);
                if (!s.LoadedMap) _msPerCell = Mathf.Clamp(_msPerCell + (s.BakeMs / Math.Max(1, n) - _msPerCell) * 0.5f, 0.005f, 0.2f);   // learned for the next estimates
                RememberTemplate(s);
                int floorCols = 0;
                foreach (var kv in s.FloorHits) if (kv.Value >= FloorColCells && _floorCols.Add(kv.Key)) floorCols++;
                s.FloorHits = null;
                s.EdgeCells = 0; s.EdgeWalkable = 0;
                for (int i = 0; i < n; i++) if (IsEdge(s, i)) { s.EdgeCells++; if (!float.IsNaN(s.FloorY[i])) s.EdgeWalkable++; }
                if (s.LoadedMap) NpcModule.Log.LogInfo("Nav: loaded map " + s.Name + ": " + s.W + " x " + s.H + ", " + s.Walkable + " walkable, " + s.CompSize.Length + " areas (" + (s.LoadedVanilla ? "vanilla" : "custom cache") + "); " + s.MapSource);
                if (!NpcModule.NavLog.Value && !NpcModule.NavDump.Value) return;     // the rest is statistics for the log
                int open = 0; for (int i = 0; i < n; i++) if (s.Open[i]) open++;
                int areas = 0, biggest = 0, islands = 0, walls = 0;
                foreach (int sz in s.CompSize) { if (sz >= MinArea) areas++; else islands += sz; if (sz > biggest) biggest = sz; }
                for (int i = 0; i < n; i++) if (WallNext(s, i)) walls++;
                if (NpcModule.NavLog.Value && !s.LoadedMap) NpcModule.Log.LogInfo("Nav: baked " + s.Name + ": " + s.W + " x " + s.H + " cells of " + s.Cell.ToString("0.00") + " m, " + s.Walkable + " walkable (" + (100f * s.Walkable / Mathf.Max(1, s.W * s.H)).ToString("0") + " %, " + open + " under open sky), "
                    + s.NoFloor + " no floor (" + s.ScanLimit + " of them ran out of ray hits), " + s.Tight + " too tight, " + s.Solid + " inside rock; outer ring " + s.EdgeWalkable + "/" + s.EdgeCells + " walkable; " + floorCols + " new floor collider(s); " + areas + " area(s), largest " + biggest + " cells, " + islands + " cells in small islands, " + walls + " cells with a wall to a neighbour; "
                    + s.BakeMs.ToString("0") + " ms over " + s.BakeFrames + " frames");
                if (NpcModule.NavDump.Value) Dump(s, "baked", -1, -1, -1, null);
            }
        }

        // the floor of a cell: the lowest walkable surface (upward facing, room for a body above it, not inside rock) within 6 m of the base height.
        // Ray by ray from the top down (a multi-hit query reports one hit per collider, and a cave's roof and floor can be one mesh; ray
        // casts skip back faces, so the inside of a cave roof is passed through and its floor is found).
        // connected areas over open edges (flood fill); the size of each
        // connected areas, a slice per frame: a flood fill over open edges whose stack and scan position live on the structure
        private static bool ComponentsStep(Structure s, float budget)
        {
            int n = s.W * s.H;
            if (s.LoadedTemplate && s.CompStack == null && s.Comp != null && s.CompSize != null) return true;   // cloned with its areas
            if (s.CompStack == null)
            {
                s.Comp = new int[n];
                for (int i = 0; i < n; i++) s.Comp[i] = -1;
                s.CompSizes = new List<int>(); s.CompStack = new Stack<int>(); s.CompScan = 0; s.CompCur = -1; s.CompCurSize = 0;
            }
            int ops = 0;
            while (true)
            {
                if ((++ops & 255) == 0 && _sw.Elapsed.TotalMilliseconds >= budget) return false;
                if (s.CompStack.Count > 0)
                {
                    int c = s.CompStack.Pop(); s.CompCurSize++;
                    int x = c % s.W, z = c / s.W;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int cx = x + dx, cz = z + dz;
                            if (cx < 0 || cz < 0 || cx >= s.W || cz >= s.H) continue;
                            int j = cz * s.W + cx;
                            if (s.Comp[j] >= 0 || !Move(s, c, j)) continue;
                            s.Comp[j] = s.CompCur; s.CompStack.Push(j);
                        }
                    continue;
                }
                if (s.CompCur >= 0) { s.CompSizes.Add(s.CompCurSize); s.CompCur = -1; }
                while (s.CompScan < n && (s.Comp[s.CompScan] >= 0 || float.IsNaN(s.FloorY[s.CompScan]))) s.CompScan++;
                if (s.CompScan >= n) { s.CompSize = s.CompSizes.ToArray(); s.CompSizes = null; s.CompStack = null; return true; }
                s.CompCur = s.CompSizes.Count; s.CompCurSize = 0;
                s.Comp[s.CompScan] = s.CompCur; s.CompStack.Push(s.CompScan);
            }
        }

        private static void Components(Structure s)
        {
            int n = s.W * s.H;
            s.Comp = new int[n];
            for (int i = 0; i < n; i++) s.Comp[i] = -1;
            var sizes = new List<int>(); var stack = new Stack<int>();
            for (int i = 0; i < n; i++)
            {
                if (s.Comp[i] >= 0 || float.IsNaN(s.FloorY[i])) continue;
                int id = sizes.Count, size = 0;
                s.Comp[i] = id; stack.Push(i);
                while (stack.Count > 0)
                {
                    int c = stack.Pop(); size++;
                    int x = c % s.W, z = c / s.W;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int cx = x + dx, cz = z + dz;
                            if (cx < 0 || cz < 0 || cx >= s.W || cz >= s.H) continue;
                            int j = cz * s.W + cx;
                            if (s.Comp[j] >= 0 || !Move(s, c, j)) continue;
                            s.Comp[j] = id; stack.Push(j);
                        }
                }
                sizes.Add(size);
            }
            s.CompSize = sizes.ToArray();
        }

        private static float Floor(Structure s, int x, int z, out Collider col, out byte why, out bool open)
        {
            Vector3 c = CellCenter(s, x, z, s.RefY);
            // start just above the band a floor can be in (RefY +- 6): the rock and roofs above it never use up the hits. A cave under a big
            // rock has many stacked faces (the rock's top, the cave shell's outside and inside, the base rock): 32 hits at most.
            float y0 = s.RefY + 6.5f, bottom = s.RefY - 8f;
            float best = float.NaN; col = null; why = 1; open = false;
            RaycastHit h;
            int k = 0;
            for (; k < 32 && y0 > bottom; k++)
            {
                if (!Physics.Raycast(new Vector3(c.x, y0, c.z), Vector3.down, out h, y0 - bottom, BakeMask, QueryTriggerInteraction.Ignore)) break;
                y0 = h.point.y - 0.05f;
                if (h.normal.y < WalkNormal) continue;                 // a wall or a rock face steeper than a body can walk
                float y = h.point.y;
                if (y > s.RefY + 6f) continue;                          // roofs, rock tops above the structure
                if (y < s.RefY - 6f) break;
                Vector3 f = new Vector3(c.x, y, c.z);
                if (InsideSolid(f)) { s.Solid++; why = 3; continue; }
                if (!BodyFits(f)) { s.Tight++; why = 2; continue; }
                best = y; col = h.collider; why = 0;                                              // keep going: the LOWEST free surface is the floor (a wreck's deck,
            }                                                           // a crate top or a cave roof above it is not where NPCs walk)
            if (float.IsNaN(best)) { if (why == 1) { s.NoFloor++; if (k >= 32) { why = 4; s.ScanLimit++; } } }
            else open = !Physics.Raycast(new Vector3(c.x, best + 1.6f, c.z), Vector3.up, 40f, BakeMask, QueryTriggerInteraction.Ignore);
            return best;
        }

        // Something the brain's sweeps hit is floor, not an obstacle, when it faces up (a slope you can walk) and is the terrain or a collider
        // that the maps found to be the floor of a large area (a cave's rock mesh, which is one Default-layer collider for floor, walls and
        // roof; a camp's deck). A brazier, a crate or a spike is never one of those, so its top still counts as an obstacle.
        // ... and only where a body can actually go: a slope no steeper than WalkNormal (~37 deg) and a contact no higher than Climb above the
        // feet (a rock lip or a ledge the physics capsule can't get over is an obstacle even if its top faces up)
        internal const float WalkNormal = 0.8f, Climb = 0.25f;
        internal static bool IsFloor(Collider c, Vector3 normal, float pointY, float feetY)
        {
            if (c == null || normal.y < WalkNormal || pointY - feetY > Climb) return false;
            return c.gameObject.layer == 14 || _floorCols.Contains(c.GetInstanceID());
        }
        internal static bool IsFloorCollider(Collider c) { return c != null && (c.gameObject.layer == 14 || _floorCols.Contains(c.GetInstanceID())); }

        // An overlap test against a non-convex mesh collider (a cave's rock) only sees its triangles, so a capsule wholly inside the rock
        // passes as free and the terrain under the rock looked like floor. One ray up with back faces on: from inside solid rock it meets the
        // inside of the rock's surface; under a real roof there is open air up to head height.
        private static bool InsideSolid(Vector3 floor)
        {
            bool old = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try { return Physics.Raycast(floor + Vector3.up * 0.05f, Vector3.up, HeadTop, BakeMask, QueryTriggerInteraction.Ignore); }
            finally { Physics.queriesHitBackfaces = old; }
        }

        private static bool BodyFits(Vector3 f)
        {
            return !Physics.CheckCapsule(f + Vector3.up * (Ankle + Radius), f + Vector3.up * (HeadTop - Radius), Radius, BakeMask, QueryTriggerInteraction.Ignore)
                && !Physics.CheckCapsule(f + Vector3.up * (BodyLo + BodyR), f + Vector3.up * Mathf.Max(BodyLo + BodyR, HeadTop - BodyR), BodyR, BakeMask, QueryTriggerInteraction.Ignore);
        }

        // for the log: why the map has nothing walkable where an NPC stands
        internal static string Probe(Vector3 pos)
        {
            foreach (var s in _structures)
            {
                if (!s.Baked || s.FloorY == null || s.Root == null || !Inside(s, pos)) continue;
                int x, z; CellOf(s, pos, out x, out z);
                var sb = new System.Text.StringBuilder();
                sb.Append(" [cell ").Append(x).Append(',').Append(z).Append(" floor ").Append(float.IsNaN(s.FloorY[z * s.W + x]) ? "none" : s.FloorY[z * s.W + x].ToString("0.0")).Append(", NPC y ").Append(pos.y.ToString("0.0"));
                // re-run the tests at the NPC's own feet
                Vector3 f = new Vector3(pos.x, pos.y - 0.98f, pos.z);
                RaycastHit h;
                if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out h, 3f, BakeMask, QueryTriggerInteraction.Ignore)) { f.y = h.point.y; sb.Append(", ground ").Append(h.collider.name).Append(" n.y ").Append(h.normal.y.ToString("0.00")); }
                else sb.Append(", no ground under it");
                sb.Append(InsideSolid(f) ? ", inside solid" : ", not inside solid");
                sb.Append(BodyFits(f) ? ", body fits" : ", body does not fit (0.2-1.5 m, r 0.28)");
                int w = 0; for (int dz = -4; dz <= 4; dz++) for (int dx = -4; dx <= 4; dx++) { int cx = x + dx, cz = z + dz; if (cx >= 0 && cz >= 0 && cx < s.W && cz < s.H && !float.IsNaN(s.FloorY[cz * s.W + cx])) w++; }
                sb.Append(", walkable within 2 m: ").Append(w).Append("/81]");
                return sb.ToString();
            }
            return "";
        }

        private static Vector3 CellCenter(Structure s, int x, int z, float y)
        {
            Vector3 p = s.GridOrigin + s.GridRight * ((x + 0.5f) * s.Cell) + s.GridForward * ((z + 0.5f) * s.Cell);
            p.y = y; return p;
        }

        private static bool CellOf(Structure s, Vector3 p, out int x, out int z)
        {
            Vector3 d = p - s.GridOrigin;
            x = Mathf.FloorToInt(Vector3.Dot(d, s.GridRight) / s.Cell); z = Mathf.FloorToInt(Vector3.Dot(d, s.GridForward) / s.Cell);
            return x >= 0 && z >= 0 && x < s.W && z < s.H;
        }

        private static bool Inside(Structure s, Vector3 p) { int x, z; return CellOf(s, p, out x, out z); }

        // the nearest walkable cell within r cells (an NPC hugging a wall stands in a blocked cell)
        // the cell an NPC really stands in / next to: nearest cells first, not in a tiny island, with no wall between the NPC and the cell
        private static int NearestReachable(Structure s, Vector3 pos, int x, int z, int r)
        {
            if (x >= 0 && z >= 0 && x < s.W && z < s.H)
            {
                int own = z * s.W + x;
                if (!float.IsNaN(s.FloorY[own]) && BigArea(s, own)) return own;      // the usual case: standing on a mapped cell
            }
            // (WW perf) the cells of the (2r+1)^2 square nearest first from a precomputed offset table (was: a list sorted with a capturing lambda
            // per call); equal distances in row order
            Offsets(); int first = -1, tried = 0; long r2 = 2L * r * r;
            for (int k = 0; k < _offD2.Length && _offD2[k] <= r2; k++)
            {
                int dx = _offDx[k], dz = _offDz[k];
                if (dx < -r || dx > r || dz < -r || dz > r) continue;
                int cx = x + dx, cz = z + dz;
                if (cx < 0 || cz < 0 || cx >= s.W || cz >= s.H) continue;
                int i = cz * s.W + cx;
                if (float.IsNaN(s.FloorY[i]) || !BigArea(s, i)) continue;
                if (first < 0) first = i;
                if (tried >= 12) break;
                Vector3 c = CellCenter(s, i % s.W, i / s.W, s.FloorY[i]);
                if (!Physics.Linecast(new Vector3(pos.x, c.y + 0.5f, pos.z), c + Vector3.up * 0.5f, BakeMask, QueryTriggerInteraction.Ignore)) return i;
                tried++;
            }
            return first;
        }

        // (WW perf) cell offsets within +-OffsetMax, nearest first (squared distance), equal distances in row order (dz, then dx)
        private const int OffsetMax = 80;
        private static int[] _offDx, _offDz; private static long[] _offD2;
        private static int[] Offsets()
        {
            if (_offDx != null) return _offDx;
            int side = 2 * OffsetMax + 1, n = side * side;
            var key = new long[n]; var idx = new int[n];
            for (int k = 0; k < n; k++) { int dx = k % side - OffsetMax, dz = k / side - OffsetMax; key[k] = ((long)(dx * dx + dz * dz) << 20) + k; idx[k] = k; }
            Array.Sort(key, idx);
            var ox = new int[n]; var oz = new int[n]; var od = new long[n];
            for (int k = 0; k < n; k++) { ox[k] = idx[k] % side - OffsetMax; oz[k] = idx[k] / side - OffsetMax; od[k] = key[k] >> 20; }
            _offDz = oz; _offD2 = od; _offDx = ox;
            return _offDx;
        }

        private static int NearestWalkable(Structure s, int x, int z, int r)
        {
            int best = -1; int bestD = int.MaxValue;
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int cx = x + dx, cz = z + dz;
                    if (cx < 0 || cz < 0 || cx >= s.W || cz >= s.H) continue;
                    int i = cz * s.W + cx;
                    if (float.IsNaN(s.FloorY[i]) || !BigArea(s, i)) continue;
                    int dd = dx * dx + dz * dz;
                    if (dd < bestD) { bestD = dd; best = i; }
                }
            return best;
        }

        // Relaxed routing (the fallback when the NPC's area of the map is sealed off): cells with no floor found (black) may be crossed -
        // a floor the bake missed is more likely than a wall there; too-tight cells (walls, spikes) and rock never.
        private static bool _relax;
        private static bool Crossable(Structure s, int i) { return !float.IsNaN(s.FloorY[i]) || s.Why[i] == 1 || s.Why[i] == 4; }

        private static bool Step(Structure s, int a, int b) { return EdgeKind(s, a, b) > 0; }

        // 0 = no way, 1 = walk, 2 = hop (a lip / step the body hops; not while _noHop)
        private static int EdgeKind(Structure s, int a, int b)
        {
            float ya = s.FloorY[a], yb = s.FloorY[b];
            if (_relax && (float.IsNaN(ya) || float.IsNaN(yb))) return s.Why != null && Crossable(s, a) && Crossable(s, b) ? 1 : 0;
            if (float.IsNaN(ya) || float.IsNaN(yb)) return 0;
            bool walkStep = Mathf.Abs(ya - yb) <= Mathf.Max(0.1f, NpcModule.NavMaxStep.Value);
            if (s.Edges == null || s.Phase < 2) return walkStep ? 1 : 0;
            int ax = a % s.W, az = a / s.W, bx = b % s.W, bz = b / s.W;
            int dx = bx - ax, dz = bz - az;
            if (dz < 0 || (dz == 0 && dx < 0)) { int t = a; a = b; b = t; dx = -dx; dz = -dz; }
            int k = dz == 0 ? 0 : dx == 0 ? 1 : dx > 0 ? 2 : 3;
            if (walkStep && (s.Edges[a] & (1 << k)) != 0) return 1;
            if (!_noHop && k <= 1 && (s.Edges[a] & (1 << (k + 4))) != 0) return 2;
            return 0;
        }

        // the route from pos toward next (first 2 m) crosses a hop edge
        internal static bool HopAhead(Vector3 pos, Vector3 next)
        {
            int hc; var s = BakedAt(pos, out hc);
            if (s == null) return false;
            Vector3 d = next - pos; d.y = 0f;
            float len = Mathf.Min(2f, d.magnitude);
            if (len < 0.05f) return false;
            d /= d.magnitude;
            int prev = hc, x, z;
            for (float t = s.Cell * 0.5f; t <= len; t += s.Cell * 0.5f)
            {
                if (!CellOf(s, pos + d * t, out x, out z)) return false;
                int i = z * s.W + x;
                if (i == prev) continue;
                int px = prev % s.W, pz = prev / s.W;
                if (px != x && pz != z)
                {   // diagonal: through either straight neighbour
                    int m1 = pz * s.W + x, m2 = z * s.W + px;
                    if (EdgeKind(s, prev, m1) == 2 || EdgeKind(s, m1, i) == 2 || EdgeKind(s, prev, m2) == 2 || EdgeKind(s, m2, i) == 2) return true;
                }
                else if (EdgeKind(s, prev, i) == 2) return true;
                prev = i;
            }
            return false;
        }

        // a walkable cell with a walkable neighbour at a walkable height that it still can't reach (a wall / spike between them)
        private static bool WallNext(Structure s, int i)
        {
            if (float.IsNaN(s.FloorY[i]) || s.Edges == null) return false;
            int x = i % s.W, z = i / s.W;
            for (int k = 0; k < 4; k++)
            {
                int cx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), cz = z + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if (cx < 0 || cz < 0 || cx >= s.W || cz >= s.H) continue;
                int j = cz * s.W + cx; float o = s.FloorY[j];
                if (float.IsNaN(o) || Mathf.Abs(o - s.FloorY[i]) > Mathf.Max(0.1f, NpcModule.NavMaxStep.Value)) continue;
                if (!Step(s, i, j)) return true;
            }
            return false;
        }

        // ---------- (WW perf, N2) move masks ----------
        // Expand / Downhill / GridSight / OutClear ask Step/EdgeKind up to 12 times per cell; the answers depend only on the map, the bake's
        // edge bits and MaxStep, so they are worked out once per structure after its bake (a slice per frame, 0.5 ms) and read as bits.
        // Until they exist, after MaxStep was changed live (rebuilt then), or under _relax / _noHop the old calls run - the same answers.
        private static readonly List<Structure> _maskQueue = new List<Structure>();
        private static readonly Stopwatch _maskSw = new Stopwatch();
        private static readonly int[] _bitOf = { 0, 1, 2, 3, -1, 4, 5, 6, 7 };     // [(dz + 1) * 3 + dx + 1] -> bit
        private static readonly int[] _dOff = new int[8]; private static readonly bool[] _dDiag = { true, false, true, false, false, true, false, true };
        private static void QueueMasks(Structure s) { if (!_maskQueue.Contains(s)) _maskQueue.Add(s); }
        private static bool MaskOk(Structure s)
        {
            if (s.Mv == null || _relax || _noHop || s.Phase < 2 || s.Edges == null) return false;
            if (Mathf.Max(0.1f, NpcModule.NavMaxStep.Value) == s.MaskStep) return true;
            s.Mv = null; s.Hop = null; QueueMasks(s);      // MaxStep changed live: made again, the old calls meanwhile
            return false;
        }
        private static void MaskTick()
        {
            if (_maskQueue.Count == 0) return;
            var s = _maskQueue[0];
            if (s.Root == null || !s.Baked || s.FloorY == null || s.Edges == null || s.Phase < 2) { s.MvB = s.HopB = null; _maskQueue.RemoveAt(0); return; }
            _maskSw.Reset(); _maskSw.Start();
            if (BuildMasks(s, 0.5)) _maskQueue.RemoveAt(0);
            _maskSw.Stop();
        }
        internal static void MasksNow(Structure s) { _maskSw.Reset(); _maskSw.Start(); BuildMasks(s, double.MaxValue); _maskSw.Stop(); }     // (offline harness) at once
        private static bool BuildMasks(Structure s, double budgetMs)
        {
            float step = Mathf.Max(0.1f, NpcModule.NavMaxStep.Value);
            int n = s.W * s.H, W = s.W, H = s.H;
            if (s.MvB == null || s.MaskStepB != step || s.MvB.Length != n) { s.MvB = new byte[n]; s.HopB = new byte[n]; s.MaskNext = 0; s.MaskStepB = step; }
            bool relax = _relax, noHop = _noHop; _relax = false; _noHop = false;
            try
            {
                while (s.MaskNext < n)
                {
                    int end = Math.Min(n, s.MaskNext + 256);
                    for (int i = s.MaskNext; i < end; i++)
                    {
                        if (float.IsNaN(s.FloorY[i])) continue;
                        int x = i % W, z = i / W, mv = 0, hop = 0;
                        for (int dz = -1; dz <= 1; dz++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dz == 0) continue;
                                int cx = x + dx, cz = z + dz;
                                if (cx < 0 || cz < 0 || cx >= W || cz >= H) continue;
                                int j = cz * W + cx, b = _bitOf[(dz + 1) * 3 + dx + 1];
                                int k = EdgeKind(s, i, j);
                                if (k == 0) continue;
                                if (dx != 0 && dz != 0) { if (!Step(s, i, z * W + cx) || !Step(s, i, cz * W + x)) continue; }
                                else if (k == 2) hop |= 1 << b;
                                mv |= 1 << b;
                            }
                        s.MvB[i] = (byte)mv; s.HopB[i] = (byte)hop;
                    }
                    s.MaskNext = end;
                    if (s.MaskNext < n && _maskSw.Elapsed.TotalMilliseconds >= budgetMs) return false;
                }
            }
            finally { _relax = relax; _noHop = noHop; }
            s.Mv = s.MvB; s.Hop = s.HopB; s.MaskStep = step; s.MvB = s.HopB = null;
            return true;
        }

        // one step on the grid as a body takes it: a diagonal step also needs both cells beside it passable from here - no corner cutting
        // (the route search, the straight-line check and the area grouping all use this, so they agree)
        private static bool Move(Structure s, int a, int b)
        {
            if (!Step(s, a, b)) return false;
            int ax = a % s.W, az = a / s.W, bx = b % s.W, bz = b / s.W;
            if (ax == bx || az == bz) return true;
            return Step(s, a, az * s.W + bx) && Step(s, a, bz * s.W + ax);
        }

        private static bool BigArea(Structure s, int i) { return s.Comp == null || (s.Comp[i] >= 0 && s.CompSize[s.Comp[i]] >= MinArea); }

        // ---------- routing ----------
        // A waypoint toward goal for an NPC at pos, or false when no structure is involved / the way is straight. pathLeft = path length to the
        // goal (inside) or to the exit plus the straight rest (outside), for the brain's progress check.
        internal static string LastReason = "";
        internal static bool Next(GameObject owner, Vector3 pos, Vector3 goal, out Vector3 next, out float pathLeft)
        {
            next = goal; pathLeft = 0f; LastReason = "";
            _tS = null; _tFrom = _tGoal = _tExit = _tPick = -1; _tD0 = float.NaN; _tRelaxed = false;
            if (!On) return false;
            // (WW) pre-recorded POI navigation (every surface, ladders, climbs) for human NPCs inside a POI that has it
            { bool poiResult; if (PoiNav.TryNext(owner, pos, goal, out next, out pathLeft, out poiResult)) return poiResult; next = goal; pathLeft = 0f; }
            // footprints overlap (4 m margins): of the structures containing the NPC take the one whose floor is nearest its feet
            Structure s = null; int from = -1; float bestDy = float.MaxValue;
            foreach (var t in _structures)
            {
                if (!t.Baked && t.Root != null && Inside(t, pos)) { _needed = t; continue; }     // not mapped yet: the bake scheduler takes it next
                if (!t.Baked || t.FloorY == null || t.Root == null || !Inside(t, pos)) continue;
                int tx, tz; CellOf(t, pos, out tx, out tz);
                int tf = NearestReachable(t, pos, tx, tz, 6);
                if (tf < 0) { if (s == null) { s = t; } continue; }
                float dy = Mathf.Abs(pos.y - t.FloorY[tf]);
                if (dy < bestDy) { bestDy = dy; s = t; from = tf; }
            }
            if (s == null) { LastReason = ""; return false; }
            _tS = s; _tFrom = from; _tDy = bestDy;
            if (from < 0) { LastReason = LogOn ? "no free map cell near it in " + s.Name : "-"; return false; }
            if (bestDy > 2.5f) { LastReason = LogOn ? "not on the floor of " + s.Name : "-"; return false; }   // on the roof of a cave, on a rock above a camp: not on this map
            int gx, gz;
            bool goalInside = CellOf(s, goal, out gx, out gz);
            int goalCell = goalInside ? NearestWalkable(s, gx, gz, 4) : -1;
            if (goalInside && (goalCell < 0 || Mathf.Abs(goal.y - s.FloorY[goalCell]) > 3f)) goalInside = false;   // in a wall / above the map: outside
            _tGoal = goalInside ? goalCell : -1;
            if (goalInside && GridSight(s, from, goalCell)) { LastReason = LogOn ? "straight line to the goal on the " + s.Name + " map" : "-"; return false; }  // straight across the floor: the feelers do the rest
            if (!goalInside)
            {
                // (1.6.0) the walk out = the walk in reversed: the map to one exit cell, then the straight line (OutNext)
                bool handled;
                bool r = OutNext(owner, s, from, goal, out next, out pathLeft, out handled);
                if (handled) return r;
                next = goal; pathLeft = 0f;      // no cell with a clear line out: the pre-1.6 way below
            }
            if (!goalInside && IsEdge(s, from)) { LastReason = LogOn ? "at the edge of " + s.Name : "-"; return false; }             // already at the edge of the footprint: out we go

            // (WW perf) the bake's areas use the very step rule of the field (Move = Step + the corner checks): cells of different areas have
            // no route between them, no need for a field to say so (as long as MaxStep was not lowered live since the bake: a lower step
            // closes edges the areas still contain)
            Field f; float d0;
            if (goalInside && s.Comp != null && s.Comp[from] != s.Comp[goalCell] && Mathf.Max(0.1f, NpcModule.NavMaxStep.Value) >= s.StepAtBake) { f = null; d0 = float.PositiveInfinity; }
            else { f = FieldFor(s, goal, goalInside, goalCell, from); d0 = f.Dist[from]; }
            if (float.IsInfinity(d0))
            {
                // The map has no way from here to the goal (the goal is outside and the footprint's outer ring can't be reached, or the goal sits
                // on a part of the map this spot doesn't connect to). Then the map's only job is to get the NPC out into the open: head for the
                // reachable open-sky cell that is best overall (path to it + straight line from it to the goal) - the cave mouth, the yard
                // outside a building - and the feelers take it from there.
                bool relaxed;
                int exit = ExitCell(owner, s, from, goal, goalInside, goalCell, out relaxed);
                _tExit = exit; _tRelaxed = relaxed;
                if (exit < 0 || exit == from) { LastReason = LogOn ? "no way out of this spot on the " + s.Name + " map" + (exit == from ? " (already at the best open spot)" : "") : "-"; return false; }
                if (relaxed) return RelaxedNext(owner, s, from, exit, pos, goal, out next, out pathLeft);
                f = FieldFor(s, CellCenter(s, exit % s.W, exit / s.W, s.FloorY[exit]), true, exit, from);
                d0 = f.Dist[from];
                if (float.IsInfinity(d0)) { LastReason = LogOn ? "no way to the exit on the " + s.Name + " map" : "-"; return false; }
                Vector3 ec = CellCenter(s, exit % s.W, exit / s.W, 0f);
                d0 += new Vector2(goal.x - ec.x, goal.z - ec.z).magnitude;
                if (GridSight(s, from, exit))
                {
                    _tD0 = d0; _tPick = exit;
                    next = CellCenter(s, exit % s.W, exit / s.W, s.FloorY[exit]);
                    pathLeft = d0; Remember(owner, next, s); return true;
                }
            }
            pathLeft = d0;
            // descend the field up to 16 cells, keep the farthest cell still in grid sight - but next to an obstacle (careful mode, 1.4.11)
            // the waypoint is no farther than the first such cell, so the body walks that stretch centre to centre instead of cutting across
            int cur = from, pick = from;
            bool careful = s.Near != null && s.Near[from];
            for (int k = 0; k < 16; k++)
            {
                int nb = Downhill(s, f, cur);
                if (nb < 0) break;
                cur = nb;
                if (GridSight(s, from, cur)) pick = cur; else break;
                if (careful || (s.Near != null && s.Near[cur])) break;
            }
            if (pick == from) { int nb = Downhill(s, f, from); if (nb < 0) { LastReason = LogOn ? "no downhill cell on the " + s.Name + " map" : "-"; return false; } pick = nb; }
            _tD0 = d0; _tPick = pick;
            next = CellCenter(s, pick % s.W, pick / s.W, s.FloorY[pick]);
            Remember(owner, next, s);
            return true;
        }

        // ---------- [Debug] NavTrace: what the last Next call saw (set by Next, read right after it by the brain's trace) ----------
        private static Structure _tS; private static int _tFrom, _tGoal, _tExit, _tPick; private static float _tD0, _tDy; private static bool _tRelaxed;
        internal static string TraceInfo()
        {
            var s = _tS;
            if (s == null) return "no baked map here";
            var sb = new System.Text.StringBuilder();
            sb.Append(s.Name.Replace("(Clone)", ""));
            if (_tFrom < 0) return sb.Append(" no cell").ToString();
            int comp = s.Comp != null ? s.Comp[_tFrom] : -1;
            sb.Append(" cell ").Append(_tFrom % s.W).Append(',').Append(_tFrom / s.W).Append(" dy ").Append(_tDy.ToString("0.0"));
            if (comp >= 0) sb.Append(" area#").Append(comp).Append(" (").Append(s.CompSize[comp]).Append(CompReachesRing(s, comp) ? ", reaches ring)" : ", NO ring)");
            sb.Append(_tGoal >= 0 ? " goal cell " + (_tGoal % s.W) + "," + (_tGoal / s.W) + (s.Comp != null && s.Comp[_tGoal] == comp ? " same area" : " area#" + (s.Comp != null ? s.Comp[_tGoal] : -1)) : " goal outside");
            if (_tExit >= 0) sb.Append(" EXIT ").Append(_tExit % s.W).Append(',').Append(_tExit / s.W).Append(_tRelaxed ? " relaxed" : "");
            if (!float.IsNaN(_tD0)) sb.Append(" path ").Append(_tD0.ToString("0.0")).Append(" m");
            if (_tPick >= 0) sb.Append(" wp ").Append(_tPick % s.W).Append(',').Append(_tPick / s.W);
            return sb.ToString();
        }

        private static readonly Dictionary<Structure, bool[]> _compRing = new Dictionary<Structure, bool[]>();
        private static bool CompReachesRing(Structure s, int comp)
        {
            bool[] r;
            if (!_compRing.TryGetValue(s, out r) || r.Length != s.CompSize.Length)
            {
                r = new bool[s.CompSize.Length];
                for (int i = 0; i < s.W * s.H; i++) if (IsEdge(s, i) && s.Comp[i] >= 0 && s.Comp[i] < r.Length) r[s.Comp[i]] = true;
                _compRing[s] = r;
            }
            return comp >= 0 && comp < r.Length && r[comp];
        }

        // the cell of a world point on the map the NPC was traced on ("x,z" or "-")
        internal static string CellText(Vector3 p)
        {
            var s = _tS; int x, z;
            if (s == null || s.Root == null || !CellOf(s, p, out x, out z)) return "-";
            int i = z * s.W + x;
            return x + "," + z + (float.IsNaN(s.FloorY[i]) ? (s.Why != null ? " (" + WhyName(s.Why[i]) + ")" : " (no floor)") : "");
        }
        private static string WhyName(byte w) { return w == 2 ? "too tight" : w == 3 ? "in rock" : w == 4 ? "why4" : "no floor"; }

        // A trace picture: the map, the NPC's area (from where the trace started) tinted, the route the map plans from the start cell to the
        // goal (cyan cells; to the exit when the goal is not reachable), and the NPC's real trail (white = on the map, orange = steering
        // without the map, red = backing up / resting). File <structure>_<x>_<z>_trace_<npc>.bmp
        internal static void TraceDump(string who, List<Vector3> trail, List<byte> kinds, Vector3 goal)
        {
            if (trail == null || trail.Count < 2) return;
            Structure s = null; int from = -1;
            foreach (var p in trail)
            {
                int c; s = BakedAt(p, out c);
                if (s != null) { from = c; break; }
            }
            if (s == null) { NpcModule.Log.LogInfo("Nav: trace of " + who + ": the trail is on no baked map"); return; }
            int gx, gz; bool inside = CellOf(s, goal, out gx, out gz);
            int gc = inside ? NearestWalkable(s, gx, gz, 4) : -1;
            if (inside && gc < 0) inside = false;
            Field f = Build(s, goal, inside, gc);
            Field mine = Build(s, goal, true, from);
            var route = new List<int>();
            int cur = from;
            if (!float.IsInfinity(f.Dist[from]))
            {
                route.Add(cur);
                for (int k = 0; k < 4000; k++) { int nb = Downhill(s, f, cur); if (nb < 0) break; cur = nb; route.Add(cur); }
            }
            var tr = new List<int>(); var tk = new List<byte>();
            for (int i = 0; i < trail.Count; i++) { int x, z; if (CellOf(s, trail[i], out x, out z)) { tr.Add(z * s.W + x); tk.Add(kinds[i]); } }
            Dump(s, "trace_" + who.Replace("(Clone)", ""), from, inside ? gc : -1, route.Count > 0 ? route[route.Count - 1] : -1, mine.Dist, route, tr, tk);
            NpcModule.Log.LogInfo("Nav: trace of " + who + " on " + s.Name + ": start cell " + (from % s.W) + "," + (from / s.W) + ", goal " + (inside ? "cell " + (gc % s.W) + "," + (gc / s.W) : "outside")
                + (route.Count > 0 ? ", map route " + route.Count + " cells" : ", NO map route from the start cell") + ", trail " + trail.Count + " points");
            Return(f); Return(mine);
        }

        // ---------- (1.6.0) a goal outside the footprint: the walk out is the walk in, reversed ----------
        // Before 1.6 an outside goal was routed by a field seeded at every ring cell with its straight distance to the goal - a straight line
        // that ignored the structure itself - and the map let go at the first ring cell. A ghost behind the camp: out of the gate, then the
        // feelers aimed back through the wall, the map pulled outward again inside the margin (wall hugging, turnarounds).
        // Now, like the walk home (a field seeded at ONE cell inside the map, then a straight line):
        // - leg 1: the map walks the NPC to one exit cell, routed as an inside goal. The exit = the cell of the NPC's area with the shortest
        //   map path + straight line to the goal among the cells whose straight line to the goal is clear on the map until it leaves the
        //   footprint (OutClear): the cave mouth's apron when the goal is in front of it, the margin on the goal's side when it is behind.
        //   Shared per (structure, area, goal 8 m bucket) for 5 s, then kept per NPC for the trip; re-picked when the goal moves > 8 m.
        // - leg 2: standing on a cell with a clear line out, Next says "straight" (false). Drifting off the line picks a new exit from there
        //   (the nearest clear line), never the old one behind it.
        private sealed class OutPlan { public Structure S; public Vector3 Goal; public int Exit; public float Made; public bool Out; }
        private static readonly Dictionary<int, OutPlan> _outPlans = new Dictionary<int, OutPlan>();
        private sealed class OutMemo { public Structure S; public int Exit; public float Until; }
        private static readonly Dictionary<long, OutMemo> _outMemo = new Dictionary<long, OutMemo>();
        private const float OutReplan = 8f; private const int MaxOutTests = 6000;
        private static float[] _ocKeys = new float[0]; private static int[] _ocIdx = new int[0];

        // the straight line from cell a toward the goal crosses only cells a body walks through (the map's own step rule), up to the edge of
        // the footprint (or the goal's cell)
        private static bool OutClear(Structure s, int a, Vector3 goal)
        {
            int x0 = a % s.W, z0 = a / s.W;
            int x1, z1; CellOf(s, goal, out x1, out z1);
            int dx = Math.Abs(x1 - x0), dz = Math.Abs(z1 - z0), sx = x0 < x1 ? 1 : -1, sz = z0 < z1 ? 1 : -1, err = dx - dz;
            int prev = a, guard = 2 * (s.W + s.H) + 4;
            byte[] mv = MaskOk(s) ? s.Mv : null;
            while (guard-- > 0)
            {
                if (x0 == x1 && z0 == z1) return true;
                int e2 = 2 * err, ox = x0, oz = z0;
                if (e2 > -dz) { err -= dz; x0 += sx; }
                if (e2 < dx) { err += dx; z0 += sz; }
                if (x0 < 0 || z0 < 0 || x0 >= s.W || z0 >= s.H) return true;     // out of the footprint: open ground from here
                int i = z0 * s.W + x0;
                if (mv != null ? (mv[prev] & (1 << _bitOf[(z0 - oz + 1) * 3 + x0 - ox + 1])) == 0 : !Move(s, prev, i)) return false;
                prev = i;
            }
            return true;
        }

        private static bool OutNext(GameObject owner, Structure s, int from, Vector3 goal, out Vector3 next, out float pathLeft, out bool handled)
        {
            next = goal; pathLeft = 0f; handled = true;
            int id = owner != null ? owner.GetInstanceID() : 0;
            OutPlan p = null;
            if (owner != null) _outPlans.TryGetValue(id, out p);
            if (p != null && (p.S != s || FlatDist(p.Goal, goal) > OutReplan)) p = null;     // another map, or news about another spot: plan again
            if (OutClear(s, from, goal))
            {
                if (p != null) p.Out = true;
                LastReason = LogOn ? "straight out of " + s.Name : "-";
                return false;
            }
            float now = Time.time;
            if (p != null && p.Exit < 0 && now - p.Made < 3f) { handled = false; return false; }   // no exit a moment ago: the old way, no new search yet
            Field f = null; float d0 = float.PositiveInfinity; int pick = -1;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (attempt > 0 || p == null || p.Out || p.Exit < 0 || p.Exit == from || float.IsNaN(s.FloorY[p.Exit]))
                {
                    bool fresh = attempt > 0 || (p != null && p.Exit >= 0);   // drifted off its line / plan gone stale / exit doesn't connect: an exit from here (after a failed search: the area's memo)
                    int exit = PickOutExit(owner, s, from, goal, fresh);
                    if (exit == Pending) { handled = false; return false; }      // (WW perf) the pick is being worked out (PickJob): the old way meanwhile
                    p = new OutPlan { S = s, Goal = goal, Exit = exit, Made = now };
                    if (owner != null)
                    {
                        if (_outPlans.Count > 256) PruneOutPlans(now);
                        _outPlans[id] = p;
                    }
                    if (exit < 0) { handled = false; return false; }
                }
                f = ExitField(s, p.Exit, now, 2, from);
                d0 = f.Dist[from];
                if (float.IsInfinity(d0) || d0 <= 0f) continue;
                pick = Descend(s, f, from);
                if (pick >= 0) break;
            }
            if (pick < 0) { p.Exit = -1; p.Made = now; handled = false; return false; }
            Vector3 ec = CellCenter(s, p.Exit % s.W, p.Exit / s.W, 0f);
            pathLeft = d0 + new Vector2(goal.x - ec.x, goal.z - ec.z).magnitude;
            _tExit = p.Exit; _tD0 = pathLeft; _tPick = pick;
            next = CellCenter(s, pick % s.W, pick / s.W, s.FloorY[pick]);
            Remember(owner, next, s);
            return true;
        }

        // the field to one exit cell: seeded at exactly that cell (FieldFor shares a field between goal cells up to 2 cells apart - an exit
        // whose own line out was never checked), expanded up to cell until (< 0: whole). Key space 2: exit cells (OutNext); 3: the NPC's own
        // cell (ExitCell / PickOutExit read the whole field from it). Seeded at a fixed cell it depends on nothing but the map, so it lives
        // at least FixedLife s (longer than FieldSeconds, which is for moving goals).
        private const float FixedLife = 5f;
        private static Field ExitField(Structure s, int exit, float now, long space, int until)
        {
            long key = (space << 40) | (long)(uint)exit;
            float life = Mathf.Max(0.2f, NpcModule.NavFieldSeconds.Value);
            Field f;
            if (s.Fields.TryGetValue(key, out f) && (f.Pin || now - f.Made < Mathf.Max(FixedLife, life))) { Expand(s, f, until); return f; }
            if (f != null) { Return(f); s.Fields.Remove(key); }
            MakeRoom(s, now, life);
            f = Build(s, Vector3.zero, true, exit, until);
            s.Fields[key] = f;
            return f;
        }

        // (WW perf, T5) One pick is a whole-area Dijkstra from the NPC's cell, a key per reachable cell and up to MaxOutTests line walks: tens
        // of ms. It runs as a PickJob, resumed by Nav.Tick under PickBudgetMs a frame (one job at a time, a queue de-duplicated by structure +
        // cell + 8 m goal bucket, and per area + bucket for the shared memo); until it is done PickOutExit answers Pending and OutNext takes
        // the old (pre-1.6) way, exactly as when no exit was found. A finished pick goes into _outMemo exactly as the direct pick did (not
        // for "fresh" picks), and is handed to the NPC that asked for it even after it walked on a few cells (one outstanding job per NPC).
        // The candidates are taken cheapest first from a heap instead of a full sort (same order; equal costs: the lower cell first).
        private const int Pending = -2;
        private const float PickBudgetMs = 1f;
        internal static bool PickInline = false;     // (offline equivalence harness) the whole pick at once, inside the call, as before
        private sealed class PickJob
        {
            public Structure S; public int From, Comp, GKey; public Vector3 Goal; public long Key, MemoKey; public bool Memo, Fresh;
            public GameObject Who; public int Stage, Scan, Cnt, Total, Tested, Exit = -1; public float LastKey; public Field F; public double Ms; public int Frames;
        }
        private sealed class PickDone { public Structure S; public int Exit, GKey; public float Until; }
        private static readonly List<PickJob> _pickQueue = new List<PickJob>();
        private static readonly Dictionary<long, PickJob> _pickByKey = new Dictionary<long, PickJob>();     // queued + running
        private static readonly Dictionary<long, PickDone> _pickDone = new Dictionary<long, PickDone>();
        private static readonly Dictionary<int, long> _pickWait = new Dictionary<int, long>();               // NPC -> the job it waits for
        private static PickJob _pickRun;
        private static readonly Stopwatch _pickSw = new Stopwatch();
        internal static int PickQueued { get { return _pickQueue.Count + (_pickRun != null ? 1 : 0); } }

        private static void PickReset()
        {
            if (_pickRun != null && _pickRun.F != null) _pickRun.F.Pin = false;
            _pickRun = null; _pickQueue.Clear(); _pickByKey.Clear(); _pickDone.Clear(); _pickWait.Clear();
        }

        // the exit for an outside goal: the cheapest (map path + straight line) cell of the NPC's area with a clear line out; Pending while
        // its job runs
        private static int PickOutExit(GameObject owner, Structure s, int from, Vector3 goal, bool fresh)
        {
            int comp = s.Comp != null ? s.Comp[from] : 0;
            int gkey = Mathf.FloorToInt(goal.x / OutReplan) * 73856093 ^ Mathf.FloorToInt(goal.z / OutReplan) * 19349663;
            int rid = s.Root.GetInstanceID();
            long key = ((long)rid << 40) ^ ((long)(comp & 0xFFFFF) << 20) ^ (long)(gkey & 0xFFFFF);
            float now = Time.time;
            int id = owner != null ? owner.GetInstanceID() : 0;
            OutMemo m;
            if (!fresh && _outMemo.TryGetValue(key, out m) && m.S == s && now < m.Until && (m.Exit < 0 || (m.Exit != from && !float.IsNaN(s.FloorY[m.Exit])))) { if (owner != null) _pickWait.Remove(id); return m.Exit; }
            if (PickInline)
            {
                var ij = new PickJob { S = s, From = from, Comp = comp, GKey = gkey, Goal = goal, MemoKey = key, Memo = !fresh, Fresh = fresh, Who = owner };
                while (!PickRun(ij, now, double.MaxValue)) { }
                return ij.Exit;
            }
            long jk = ((long)rid << 40) ^ ((long)(from & 0xFFFFF) << 20) ^ (long)(gkey & 0xFFFFF);
            PickDone dn; PickJob j;
            // the job this NPC already waits for (asked from the cell it stood on then): its answer, or keep waiting - one job per NPC
            long wk;
            if (owner != null && _pickWait.TryGetValue(id, out wk))
            {
                if (_pickDone.TryGetValue(wk, out dn) && dn.S == s && dn.GKey == gkey && now < dn.Until) { _pickWait.Remove(id); return dn.Exit; }
                if (_pickByKey.TryGetValue(wk, out j) && j.S == s && j.GKey == gkey) { if (!fresh) j.Memo = true; else j.Fresh = true; return Pending; }
                _pickWait.Remove(id);
            }
            if (_pickDone.TryGetValue(jk, out dn) && dn.S == s && now < dn.Until) return dn.Exit;     // another NPC on this cell asked a moment ago
            if (!_pickByKey.TryGetValue(jk, out j) && !fresh)
            {
                // the area's memo is being worked out from another NPC's cell (as the direct pick: the first one asking decides for the area)
                if (_pickRun != null && _pickRun.Memo && _pickRun.MemoKey == key && _pickRun.S == s) j = _pickRun;
                else foreach (var q in _pickQueue) if (q.Memo && q.MemoKey == key && q.S == s) { j = q; break; }
            }
            if (j == null)
            {
                if (_pickQueue.Count >= 32) return Pending;       // a flood: the old way meanwhile, asked again next think
                j = new PickJob { S = s, From = from, Comp = comp, GKey = gkey, Goal = goal, Key = jk, MemoKey = key, Who = owner };
                _pickQueue.Add(j); _pickByKey[jk] = j;
            }
            if (!fresh) j.Memo = true; else j.Fresh = true;
            if (owner != null) { if (_pickWait.Count > 256) _pickWait.Clear(); _pickWait[id] = j.Key; }
            return Pending;
        }

        // Nav.Tick: the running pick, PickBudgetMs a frame
        private static void PickStep()
        {
            if (_pickRun == null && _pickQueue.Count == 0) return;
            float now = Time.time;
            _pickSw.Reset(); _pickSw.Start();
            while (_pickSw.Elapsed.TotalMilliseconds < PickBudgetMs)
            {
                if (_pickRun == null)
                {
                    if (_pickQueue.Count == 0) break;
                    _pickRun = _pickQueue[0]; _pickQueue.RemoveAt(0);
                    var q = _pickRun; OutMemo m;
                    // only the area memo was wanted and another job made it meanwhile: nothing to do
                    if (!q.Fresh && _outMemo.TryGetValue(q.MemoKey, out m) && m.S == q.S && now < m.Until) { _pickByKey.Remove(q.Key); _pickRun = null; continue; }
                }
                var j = _pickRun;
                if (j.S.Root == null || !j.S.Baked || j.S.FloorY == null) { if (j.F != null) j.F.Pin = false; _pickByKey.Remove(j.Key); _pickRun = null; continue; }
                double left = PickBudgetMs - _pickSw.Elapsed.TotalMilliseconds;
                double t0 = _pickSw.Elapsed.TotalMilliseconds;
                bool done = PickRun(j, now, left);
                j.Ms += _pickSw.Elapsed.TotalMilliseconds - t0;
                if (!done) { j.Frames++; break; }
                j.Frames++;
                _pickByKey.Remove(j.Key); _pickRun = null;
                if (_pickDone.Count > 64) { _expired.Clear(); foreach (var kv in _pickDone) if (now >= kv.Value.Until) _expired.Add(kv.Key); foreach (var k in _expired) _pickDone.Remove(k); if (_pickDone.Count > 64) _pickDone.Clear(); }
                _pickDone[j.Key] = new PickDone { S = j.S, Exit = j.Exit, GKey = j.GKey, Until = now + 5f };
            }
            _pickSw.Stop();
        }

        // one slice of a pick (budget in ms; double.MaxValue = to the end). True when done (j.Exit set, memo stored, logged).
        private static bool PickRun(PickJob j, float now, double budget)
        {
            var s = j.S; int n = s.W * s.H;
            var sw = _pickSw; bool timed = budget < double.MaxValue; double start = timed ? sw.Elapsed.TotalMilliseconds : 0d;
            Stopwatch own = null;
            if (!timed) own = Stopwatch.StartNew();
            while (true)
            {
                if (timed && sw.Elapsed.TotalMilliseconds - start >= budget) return false;
                if (j.Stage == 0)
                {
                    // (WW perf) the whole field from the NPC's cell, shared with ExitCell and every NPC on that cell (cache key space 3);
                    // fetched settled at its own cell only, then expanded here a slice at a time - pinned meanwhile
                    j.F = ExitField(s, j.From, now, 3, j.From); j.F.Pin = true;
                    if (_ocKeys.Length < n) { _ocKeys = new float[n]; _ocIdx = new int[n]; }
                    j.Stage = 1; continue;
                }
                if (j.Stage <= 2 && (j.F == null || j.F.Dist == null)) { j.Stage = 0; j.Scan = 0; j.Cnt = 0; continue; }    // evicted after all (scene): from the start
                if (j.Stage == 1)
                {
                    if (ExpandCore(s, j.F, -1, timed ? 512 : int.MaxValue)) { j.Stage = 2; j.Scan = 0; j.Cnt = 0; }
                    continue;
                }
                if (j.Stage == 2)
                {
                    float[] dist = j.F.Dist; Vector3 goal = j.Goal;
                    int end = timed ? Math.Min(n, j.Scan + 4096) : n, cnt = j.Cnt;
                    for (int i = j.Scan; i < end; i++)
                    {
                        float d = dist[i];
                        if (float.IsInfinity(d) || !BigArea(s, i)) continue;
                        Vector3 c = CellCenter(s, i % s.W, i / s.W, 0f);
                        _ocKeys[cnt] = d + new Vector2(goal.x - c.x, goal.z - c.z).magnitude; _ocIdx[cnt] = i; cnt++;
                    }
                    j.Cnt = cnt; j.Scan = end;
                    if (end >= n) { j.F.Pin = false; j.Total = j.Cnt; j.Scan = j.Cnt / 2; j.Stage = 3; }     // the distances are read: the field is the cache's again
                    continue;
                }
                if (j.Stage == 3)
                {
                    // a min-heap over (cost, cell), built bottom-up (sift-down from the middle), a slice at a time
                    int stop = timed ? Math.Max(-1, j.Scan - 1024) : -1;
                    for (int i = j.Scan; i > stop; i--) SiftDown(i, j.Cnt);
                    j.Scan = stop;
                    if (stop < 0) { j.Stage = 4; j.Tested = 0; j.Exit = -1; }
                    continue;
                }
                // stage 4: cheapest first, the first cell (not the NPC's own) with a clear line out; at most MaxOutTests
                int tests = 0;
                while (j.Cnt > 0 && j.Tested < MaxOutTests)
                {
                    int ci = _ocIdx[0]; float ck = _ocKeys[0];
                    j.Cnt--; _ocIdx[0] = _ocIdx[j.Cnt]; _ocKeys[0] = _ocKeys[j.Cnt]; SiftDown(0, j.Cnt);
                    j.Tested++; j.LastKey = ck;
                    if (ci == j.From) continue;          // its own line was just found blocked
                    if (OutClear(s, ci, j.Goal)) { j.Exit = ci; j.Cnt = 0; break; }
                    if (timed && (++tests & 7) == 0 && sw.Elapsed.TotalMilliseconds - start >= budget) return false;
                }
                if (j.Cnt > 0 && j.Tested < MaxOutTests && j.Exit < 0) return false;
                break;
            }
            if (j.Memo)
            {
                if (_outMemo.Count > 64) _outMemo.Clear();
                _outMemo[j.MemoKey] = new OutMemo { S = s, Exit = j.Exit, Until = now + 5f };
            }
            if (LogOn)
            {
                double ms = timed ? j.Ms + (sw.Elapsed.TotalMilliseconds - start) : own.Elapsed.TotalMilliseconds;
                NpcModule.Log.LogInfo("Nav: " + (j.Who != null ? j.Who.name : "?") + " way out of " + s.Name + " toward " + j.Goal.x.ToString("0") + "," + j.Goal.z.ToString("0") + ": "
                    + (j.Exit < 0 ? "no cell with a clear line out (old routing)" : "exit cell " + (j.Exit % s.W) + "," + (j.Exit / s.W) + ", path + line " + j.LastKey.ToString("0") + " m")
                    + " (" + j.Tested + "/" + j.Total + " cells tested, " + ms.ToString("0.0") + " ms" + (timed ? " over " + (j.Frames + 1) + " frame(s)" : "") + (j.Fresh && !j.Memo ? ", from here" : "") + ")");
            }
            return true;
        }

        // the candidate heap: ordered by cost, equal costs by cell index
        private static bool Less(int a, int b) { float ka = _ocKeys[a], kb = _ocKeys[b]; return ka < kb || (ka == kb && _ocIdx[a] < _ocIdx[b]); }
        private static void SiftDown(int i, int cnt)
        {
            while (true)
            {
                int l = 2 * i + 1; if (l >= cnt) return;
                int r = l + 1, m = r < cnt && Less(r, l) ? r : l;
                if (!Less(m, i)) return;
                float tk = _ocKeys[i]; _ocKeys[i] = _ocKeys[m]; _ocKeys[m] = tk;
                int ti = _ocIdx[i]; _ocIdx[i] = _ocIdx[m]; _ocIdx[m] = ti;
                i = m;
            }
        }

        // down the field up to 16 cells: the farthest cell still in grid sight (next to an obstacle: no farther than the first such cell)
        private static int Descend(Structure s, Field f, int from)
        {
            int cur = from, pick = from;
            bool careful = s.Near != null && s.Near[from];
            for (int k = 0; k < 16; k++)
            {
                int nb = Downhill(s, f, cur);
                if (nb < 0) break;
                cur = nb;
                if (GridSight(s, from, cur)) pick = cur; else break;
                if (careful || (s.Near != null && s.Near[cur])) break;
            }
            if (pick == from) pick = Downhill(s, f, from);
            return pick;
        }

        private static void PruneOutPlans(float now)
        {
            _expired.Clear();
            foreach (var kv in _outPlans) if (now - kv.Value.Made > 60f) _expired.Add(kv.Key);
            foreach (var k in _expired) _outPlans.Remove((int)k);
            if (_outPlans.Count > 256) _outPlans.Clear();
        }

        private static float FlatDist(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        // ---------- the way out when the map can't reach the goal ----------
        // The answer depends on the area the NPC is in and the goal, not on the NPC: one memo per (structure, area, goal bucket), 2 s
        private sealed class ExitMemo { public int Exit; public float Until; public bool Relaxed; public Structure S; }
        private static readonly Dictionary<long, ExitMemo> _exits = new Dictionary<long, ExitMemo>();
        private static int ExitCell(GameObject owner, Structure s, int from, Vector3 goal, bool goalInside, int goalCell, out bool exitRelaxed)
        {
            exitRelaxed = false;
            int gkey = Mathf.FloorToInt(goal.x / 4f) * 73856093 ^ Mathf.FloorToInt(goal.z / 4f) * 19349663;
            int comp = s.Comp != null ? s.Comp[from] : from;
            long mkey = ((long)s.Root.GetInstanceID() << 40) ^ ((long)(comp & 0xFFFFF) << 20) ^ (long)(gkey & 0xFFFFF);
            ExitMemo m;
            float now = Time.time;
            if (_exits.TryGetValue(mkey, out m) && m.S == s && now < m.Until && m.Exit >= 0 && m.Exit < s.W * s.H && !float.IsNaN(s.FloorY[m.Exit])) { exitRelaxed = m.Relaxed; return m.Exit; }
            if (_exits.Count > 64) _exits.Clear();
            // distances from the NPC over its part of the map (the whole field; cached per cell, shared with PickOutExit)
            var mine = ExitField(s, from, now, 3, -1);
            int n = s.W * s.H, best = -1, bestAny = -1, size = 0, open = 0; bool edge = false;
            float bc = float.MaxValue, bca = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float d = mine.Dist[i];
                if (float.IsInfinity(d)) continue;
                size++;
                if (IsEdge(s, i)) edge = true;
                Vector3 c = CellCenter(s, i % s.W, i / s.W, 0f);
                float cost = d + new Vector2(goal.x - c.x, goal.z - c.z).magnitude;
                if (cost < bca) { bca = cost; bestAny = i; }
                if (s.Open == null || !s.Open[i]) continue;
                open++;
                if (cost < bc) { bc = cost; best = i; }
            }
            bool relaxed = false;
            if (open == 0 && !edge)
            {
                // the area is sealed on the map (no open sky, no way to the outer ring): the bake must have missed a floor or a passage. Cross
                // no-floor cells: the nearest open-sky cell of the largest area, by relaxed path + straight line to the goal.
                int bigId = -1, bigSize = 0;
                for (int c2 = 0; c2 < s.CompSize.Length; c2++) if (s.CompSize[c2] > bigSize) { bigSize = s.CompSize[c2]; bigId = c2; }
                _relax = true;
                Field rel;
                try { rel = Build(s, goal, true, from); }
                finally { _relax = false; }
                float rb = float.MaxValue; int ri = -1;
                for (int i = 0; i < n; i++)
                {
                    if (float.IsInfinity(rel.Dist[i]) || float.IsNaN(s.FloorY[i]) || !s.Open[i] || s.Comp[i] != bigId) continue;
                    Vector3 c = CellCenter(s, i % s.W, i / s.W, 0f);
                    float cost = rel.Dist[i] + new Vector2(goal.x - c.x, goal.z - c.z).magnitude;
                    if (cost < rb) { rb = cost; ri = i; }
                }
                if (ri >= 0) { best = ri; relaxed = true; }
                Return(rel);
            }
            if (best < 0) best = bestAny;
            _exits[mkey] = new ExitMemo { Exit = best, Until = now + 2f, Relaxed = relaxed, S = s };
            if (NpcModule.NavLog.Value)
                NpcModule.Log.LogInfo("Nav: " + (owner != null ? owner.name : "?") + " has no map route to its goal on " + s.Name + " (goal " + (goalInside ? "inside, cell " + (goalCell % s.W) + "," + (goalCell / s.W) : "outside the footprint")
                    + " at " + goal.x.ToString("0") + "," + goal.z.ToString("0") + "; its area " + size + " cells, " + open + " under open sky, " + (edge ? "reaches" : "does NOT reach") + " the outer ring ("
                    + s.EdgeWalkable + "/" + s.EdgeCells + " ring cells walkable)) -> " + (best < 0 ? "nowhere to go" : "heads for cell " + (best % s.W) + "," + (best / s.W) + (relaxed ? " (open sky, across cells the map has no floor for)" : s.Open != null && s.Open[best] ? " (open sky)" : " (no open sky in its area)")));
            if (NpcModule.NavDump.Value && now >= s.NextDump)
            {
                s.NextDump = now + 20f;
                Dump(s, "noroute_" + (owner != null ? owner.name.Replace("(Clone)", "") : "npc"), from, goalInside ? goalCell : -1, best, mine.Dist);
            }
            exitRelaxed = relaxed;
            return best;
        }

        // ---------- [Debug] NavDump: the map as a picture ----------
        // (also: orange = a wall or spike between this cell and a walkable neighbour; purple = a small island, never used for routes)
        // BepInEx/config/Apocaraider/NavDump/<structure>_<x>_<z>_<tag>.bmp, 2 px per cell, north up. Walkable: grey by height (open sky
        // greenish, under a roof bluish); a walkable cell next to a walkable one more than MaxStep higher or lower: yellow; no floor: black;
        // too tight for a body: red; inside rock: brown. On a "no route" dump: the NPC's reachable area is tinted, the NPC white, the goal
        // magenta, the chosen exit cyan.
        private static void Dump(Structure s, string tag, int npc, int goalCell, int exit, float[] area) { Dump(s, tag, npc, goalCell, exit, area, null, null, null); }
        private static void Dump(Structure s, string tag, int npc, int goalCell, int exit, float[] area, List<int> route, List<int> trail, List<byte> trailKind)
        {
            try
            {
                int W = s.W, H = s.H, sc = 2, pw = W * sc, ph = H * sc, row = (pw * 3 + 3) & ~3;
                var px = new byte[row * ph];
                float step = Mathf.Max(0.1f, NpcModule.NavMaxStep.Value);
                for (int z = 0; z < H; z++)
                    for (int x = 0; x < W; x++)
                    {
                        int i = z * W + x; byte r, g, b;
                        if (float.IsNaN(s.FloorY[i]))
                        {
                            switch (s.Why[i]) { case 2: r = 200; g = 30; b = 30; break; case 3: r = 110; g = 70; b = 30; break; case 4: r = 20; g = 20; b = 110; break; default: r = 0; g = 0; b = 0; break; }
                            if (area != null && !float.IsInfinity(area[i])) { r = (byte)Math.Min(255, r + 60); g = (byte)Math.Min(255, g + 90); b = (byte)Math.Min(255, b + 80); }
                        }
                        else
                        {
                            float t = Mathf.Clamp01((s.FloorY[i] - s.RefY + 3f) / 6f);
                            int v = (int)(90 + 140 * t);
                            if (s.Open[i]) { r = (byte)(v * 0.85f); g = (byte)v; b = (byte)(v * 0.8f); } else { r = (byte)(v * 0.75f); g = (byte)(v * 0.8f); b = (byte)v; }
                            bool cliff = false;
                            for (int k = 0; k < 4 && !cliff; k++)
                            {
                                int cx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), cz = z + (k == 2 ? 1 : k == 3 ? -1 : 0);
                                if (cx < 0 || cz < 0 || cx >= W || cz >= H) continue;
                                float o = s.FloorY[cz * W + cx];
                                if (!float.IsNaN(o) && Mathf.Abs(o - s.FloorY[i]) > step) cliff = true;
                            }
                            if (cliff) { r = 240; g = 220; b = 40; }
                            if (WallNext(s, i)) { r = 255; g = 130; b = 0; }
                            if (!BigArea(s, i)) { r = 150; g = 60; b = 170; }
                            if (area != null && !float.IsInfinity(area[i])) { r = (byte)(r * 0.6f); g = (byte)(g * 0.6f + 80); b = (byte)(b * 0.6f + 60); }
                        }
                        if (i == exit) { r = 0; g = 255; b = 255; }
                        if (i == goalCell) { r = 255; g = 0; b = 255; }
                        if (i == npc) { r = 255; g = 255; b = 255; }
                        for (int dy = 0; dy < sc; dy++)
                            for (int dx = 0; dx < sc; dx++)
                            {
                                int o = (z * sc + dy) * row + (x * sc + dx) * 3;
                                px[o] = b; px[o + 1] = g; px[o + 2] = r;
                            }
                    }
                if (route != null) foreach (int i in route) Paint(px, row, sc, i % W, i / W, 0, 200, 255);
                if (trail != null)
                    for (int t = 0; t < trail.Count; t++)
                    {
                        byte k = trailKind[t];
                        if (k == 0) Paint(px, row, sc, trail[t] % W, trail[t] / W, 255, 255, 255);
                        else if (k == 1) Paint(px, row, sc, trail[t] % W, trail[t] / W, 255, 140, 0);
                        else Paint(px, row, sc, trail[t] % W, trail[t] / W, 255, 0, 0);
                    }
                // marks a little bigger so they show
                foreach (var m in new[] { npc, goalCell, exit })
                {
                    if (m < 0) continue;
                    int mx = m % W, mz = m / W;
                    byte r = m == npc ? (byte)255 : m == goalCell ? (byte)255 : (byte)0, g = m == npc ? (byte)255 : m == goalCell ? (byte)0 : (byte)255, b = 255;
                    for (int dz = -2; dz <= 2; dz++) for (int dx = -2; dx <= 2; dx++)
                    {
                        int cx = mx + dx, cz = mz + dz; if (cx < 0 || cz < 0 || cx >= W || cz >= H) continue;
                        for (int yy = 0; yy < sc; yy++) for (int xx = 0; xx < sc; xx++) { int o = (cz * sc + yy) * row + (cx * sc + xx) * 3; px[o] = b; px[o + 1] = g; px[o + 2] = r; }
                    }
                }
                string dir = System.IO.Path.Combine(NpcModule.DataDir ?? Core.Assets.ModuleConfigDir("NPC"), "NavDump");
                System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir, s.Name.Replace("(Clone)", "") + "_" + Mathf.RoundToInt(s.Root.position.x) + "_" + Mathf.RoundToInt(s.Root.position.z) + "_" + tag + ".bmp");
                using (var fs = new System.IO.FileStream(file, System.IO.FileMode.Create))
                using (var w = new System.IO.BinaryWriter(fs))
                {
                    w.Write((byte)'B'); w.Write((byte)'M'); w.Write(54 + px.Length); w.Write(0); w.Write(54);
                    w.Write(40); w.Write(pw); w.Write(ph); w.Write((short)1); w.Write((short)24); w.Write(0); w.Write(px.Length); w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
                    w.Write(px);
                }
                NpcModule.Log.LogInfo("Nav: map picture " + file + " (cell " + s.Cell.ToString("0.00") + " m, grid origin " + s.GridOrigin.x.ToString("0.0") + "," + s.GridOrigin.z.ToString("0.0") + ", base y " + s.RefY.ToString("0.0") + ")");
            }
            catch (Exception e) { NpcModule.Log.LogError("Nav: dump failed: " + e.Message); }
        }

        private static void Paint(byte[] px, int row, int sc, int x, int z, byte r, byte g, byte b)
        {
            for (int dy = 0; dy < sc; dy++) for (int dx = 0; dx < sc; dx++) { int o = (z * sc + dy) * row + (x * sc + dx) * 3; if (o >= 0 && o + 2 < px.Length) { px[o] = b; px[o + 1] = g; px[o + 2] = r; } }
        }

        // the relaxed route to an exit: a field seeded at the exit with no-floor cells crossable, cached like the others; the waypoint is the
        // farthest cell down it in (relaxed) grid sight - the brain's leg check still vetoes a waypoint behind a real wall
        private static readonly Dictionary<long, Field> _relaxedFields = new Dictionary<long, Field>();
        private static bool RelaxedNext(GameObject owner, Structure s, int from, int exit, Vector3 pos, Vector3 goal, out Vector3 next, out float pathLeft)
        {
            next = goal; pathLeft = 0f;
            long key = ((long)s.Root.GetInstanceID() << 32) ^ exit;
            Field f;
            if (!_relaxedFields.TryGetValue(key, out f) || Time.time - f.Made > 5f)
            {
                if (f != null) Return(f);
                if (_relaxedFields.Count > 32) { foreach (var kv in _relaxedFields) Return(kv.Value); _relaxedFields.Clear(); }
                _relax = true;
                try { f = Build(s, goal, true, exit); }
                finally { _relax = false; }
                _relaxedFields[key] = f;
            }
            if (float.IsInfinity(f.Dist[from])) { LastReason = LogOn ? "no relaxed way out on the " + s.Name + " map" : "-"; return false; }
            _relax = true;
            int pick = from;
            try
            {
                int cur = from;
                for (int k = 0; k < 16; k++)
                {
                    int nb = Downhill(s, f, cur);
                    if (nb < 0) break;
                    cur = nb;
                    if (GridSight(s, from, cur)) pick = cur; else break;
                }
                if (pick == from) { int nb = Downhill(s, f, from); if (nb >= 0) pick = nb; }
            }
            finally { _relax = false; }
            if (pick == from) { LastReason = LogOn ? "no downhill cell on the relaxed " + s.Name + " map" : "-"; return false; }
            float y = float.IsNaN(s.FloorY[pick]) ? pos.y - 0.98f : s.FloorY[pick];
            next = CellCenter(s, pick % s.W, pick / s.W, y);
            Vector3 ec = CellCenter(s, exit % s.W, exit / s.W, 0f);
            pathLeft = f.Dist[from] + new Vector2(goal.x - ec.x, goal.z - ec.z).magnitude;
            Remember(owner, next, s);
            return true;
        }

        private static bool IsEdge(Structure s, int i) { int x = i % s.W, z = i / s.W; return x == 0 || z == 0 || x == s.W - 1 || z == s.H - 1; }

        private static int Downhill(Structure s, Field f, int i)
        {
            int x = i % s.W, z = i / s.W; float best = f.Dist[i]; int bi = -1;
            int m = MaskOk(s) ? s.Mv[i] : -1;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int cx = x + dx, cz = z + dz;
                    if (cx < 0 || cz < 0 || cx >= s.W || cz >= s.H) continue;
                    int j = cz * s.W + cx;
                    if (m >= 0) { if ((m & (1 << _bitOf[(dz + 1) * 3 + dx + 1])) == 0) continue; }
                    else
                    {
                        if (!Step(s, i, j)) continue;
                        if (dx != 0 && dz != 0 && (!Step(s, i, z * s.W + cx) || !Step(s, i, cz * s.W + x))) continue;
                    }
                    if (f.Dist[j] < best) { best = f.Dist[j]; bi = j; }
                }
            return bi;
        }

        // every cell on the grid line between two cells walkable and step-connected (a body-wide corridor, since cells are body-checked)
        private static bool GridSight(Structure s, int a, int b)
        {
            int x0 = a % s.W, z0 = a / s.W, x1 = b % s.W, z1 = b / s.W;
            int dx = Math.Abs(x1 - x0), dz = Math.Abs(z1 - z0), sx = x0 < x1 ? 1 : -1, sz = z0 < z1 ? 1 : -1, err = dx - dz;
            int prev = a;
            byte[] mv = MaskOk(s) ? s.Mv : null;
            while (true)
            {
                if (x0 == x1 && z0 == z1) return true;
                int e2 = 2 * err, ox = x0, oz = z0;
                if (e2 > -dz) { err -= dz; x0 += sx; }
                if (e2 < dx) { err += dx; z0 += sz; }
                int i = z0 * s.W + x0;
                if (mv != null ? (mv[prev] & (1 << _bitOf[(z0 - oz + 1) * 3 + x0 - ox + 1])) == 0 : !Move(s, prev, i)) return false;     // diagonal steps never cut a corner (a pinhole in a one-cell wall is no way through)
                prev = i;
            }
        }

        // the field for a goal, expanded (at least) up to cell until - the cell the caller reads
        private static Field FieldFor(Structure s, Vector3 goal, bool inside, int goalCell, int until)
        {
            float now = Time.time;
            // an inside goal is bucketed to 4 x 4 cells (2 m): a running target doesn't force a new Dijkstra every think; the cached field
            // is kept while its goal cell is still within 2 cells of the real one (the feelers close the last metres anyway)
            long key = inside ? ((long)(goalCell % s.W / 4) << 20) | (long)(goalCell / s.W / 4)
                              : (long)1 << 40 | (long)(Mathf.FloorToInt(goal.x / 4f) & 0xFFFFF) << 20 | (long)(Mathf.FloorToInt(goal.z / 4f) & 0xFFFFF);
            float life = Mathf.Max(0.2f, NpcModule.NavFieldSeconds.Value);
            Field f;
            if (s.Fields.TryGetValue(key, out f) && now - f.Made < life && (!inside || f.GoalCell == goalCell || Settle(s, f, goalCell) <= s.Cell * 2.9f)) { Expand(s, f, until); return f; }
            if (f != null) { Return(f); s.Fields.Remove(key); }
            MakeRoom(s, now, life);
            f = Build(s, goal, inside, goalCell, until);
            s.Fields[key] = f;
            return f;
        }
        private static float Settle(Structure s, Field f, int cell) { Expand(s, f, cell); return f.Dist[cell]; }

        // Dijkstra over the grid (8 neighbours, no corner cutting, step limit); the whole reachable area (until < 0) or only up to the cell
        // until (then Expand resumes it when a farther cell is asked for)
        private static Field Build(Structure s, Vector3 goal, bool inside, int goalCell) { return Build(s, goal, inside, goalCell, -1); }
        private static Field Build(Structure s, Vector3 goal, bool inside, int goalCell, int until)
        {
            int n = s.W * s.H;
            var dist = Rent(n);
            for (int i = 0; i < n; i++) dist[i] = float.PositiveInfinity;
            var heap = _heaps.Count > 0 ? _heaps.Pop() : new Heap(Math.Max(64, n / 2));    // a large map's heap is ~1.4 MB: pooled like the arrays (n/2: a full build of a big area never resizes)
            heap.Clear();
            if (inside) { dist[goalCell] = 0f; heap.Push(goalCell, 0f); }
            else
            {
                for (int x = 0; x < s.W; x++) { Seed(s, x, 0, goal, dist, heap); Seed(s, x, s.H - 1, goal, dist, heap); }
                for (int z = 1; z < s.H - 1; z++) { Seed(s, 0, z, goal, dist, heap); Seed(s, s.W - 1, z, goal, dist, heap); }
            }
            var f = new Field { Dist = dist, Made = Time.time, GoalInside = inside, GoalCell = inside ? goalCell : -1, H = heap, Settled = float.NegativeInfinity };
            Expand(s, f, until);
            return f;
        }

        // resumes the field's Dijkstra until cell until is settled (its distance final: Dist <= Settled) or the area is exhausted (until < 0:
        // the whole field, for the readers that scan every cell). The pops continue exactly where they stopped, so every settled distance
        // equals the full build's; Downhill only steps to cells with a smaller distance than a settled one, which are settled too.
        private static void Expand(Structure s, Field f, int until) { ExpandCore(s, f, until, int.MaxValue); }

        // (WW perf) the same, at most maxPops pops (the sliced exit pick): stops with Settled = the last popped key (every cell at or below
        // it is final, exactly as when it stops at until). True = the field is complete.
        private static bool ExpandCore(Structure s, Field f, int until, int maxPops)
        {
            var heap = f.H;
            if (heap == null) return true;
            if (until >= 0 && f.Dist[until] <= f.Settled) return false;
            float[] dist = f.Dist;
            float c1 = s.Cell, c2 = s.Cell * 1.41421356f;
            int i0; float d;
            if (MaskOk(s))
            {
                // (N2) the same relaxation from the move masks, neighbours in the same order (so the same pushes, pops and partial states)
                byte[] mv = s.Mv, hp = s.Hop; bool[] near = s.Near; int W = s.W;
                _dOff[0] = -W - 1; _dOff[1] = -W; _dOff[2] = -W + 1; _dOff[3] = -1; _dOff[4] = 1; _dOff[5] = W - 1; _dOff[6] = W; _dOff[7] = W + 1;
                while (heap.Pop(out i0, out d))
                {
                    if (d > dist[i0]) continue;
                    int m = mv[i0];
                    if (m != 0)
                    {
                        int h = hp[i0];
                        for (int b = 0; b < 8; b++)
                        {
                            if ((m & (1 << b)) == 0) continue;
                            int j = i0 + _dOff[b];
                            float nd = d + (_dDiag[b] ? c2 : c1);
                            if ((h & (1 << b)) != 0) nd += HopCost;
                            if (near != null && near[j]) nd += NearCost;
                            if (nd < dist[j]) { dist[j] = nd; heap.Push(j, nd); }
                        }
                    }
                    if (i0 == until) { f.Settled = d; return false; }
                    if (--maxPops <= 0) { f.Settled = d; if (heap.Count > 0) return false; break; }
                }
                f.Settled = float.PositiveInfinity; f.H = null;
                if (_heaps.Count < PoolCap) _heaps.Push(heap);
                return true;
            }
            while (heap.Pop(out i0, out d))
            {
                if (d > dist[i0]) continue;
                int x = i0 % s.W, z = i0 / s.W;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int cx = x + dx, cz = z + dz;
                        if (cx < 0 || cz < 0 || cx >= s.W || cz >= s.H) continue;
                        int j = cz * s.W + cx;
                        if (!Step(s, i0, j)) continue;
                        if (dx != 0 && dz != 0 && (!Step(s, i0, z * s.W + cx) || !Step(s, i0, cz * s.W + x))) continue;
                        float nd = d + (dx != 0 && dz != 0 ? c2 : c1);
                        if (dx == 0 || dz == 0) { if (EdgeKind(s, i0, j) == 2) nd += HopCost; }
                        if (s.Near != null && s.Near[j]) nd += NearCost;
                        if (nd < dist[j]) { dist[j] = nd; heap.Push(j, nd); }
                    }
                if (i0 == until) { f.Settled = d; return false; }
                if (--maxPops <= 0) { f.Settled = d; if (heap.Count > 0) return false; break; }
            }
            f.Settled = float.PositiveInfinity; f.H = null;
            if (_heaps.Count < PoolCap) _heaps.Push(heap);
            return true;
        }

        private static void Seed(Structure s, int x, int z, Vector3 goal, float[] dist, Heap heap)
        {
            int i = z * s.W + x;
            if (float.IsNaN(s.FloorY[i])) return;
            Vector3 c = CellCenter(s, x, z, 0f); c.y = goal.y = 0f;
            float d = Vector3.Distance(c, new Vector3(goal.x, 0f, goal.z));
            if (d < dist[i]) { dist[i] = d; heap.Push(i, d); }
        }

        internal sealed class Heap
        {
            private int[] _i; private float[] _k; private int _n;
            public Heap(int cap) { _i = new int[cap]; _k = new float[cap]; }
            public void Clear() { _n = 0; }
            public int Count { get { return _n; } }
            public void Push(int i, float k)
            {
                if (_n == _i.Length) { Array.Resize(ref _i, _n * 2); Array.Resize(ref _k, _n * 2); }
                int c = _n++;
                while (c > 0) { int p = (c - 1) >> 1; if (_k[p] <= k) break; _i[c] = _i[p]; _k[c] = _k[p]; c = p; }
                _i[c] = i; _k[c] = k;
            }
            public bool Pop(out int i, out float k)
            {
                if (_n == 0) { i = -1; k = 0f; return false; }
                i = _i[0]; k = _k[0];
                int li = _i[--_n]; float lk = _k[_n];
                int c = 0;
                while (true)
                {
                    int a = 2 * c + 1; if (a >= _n) break;
                    int b = a + 1; int m = b < _n && _k[b] < _k[a] ? b : a;
                    if (_k[m] >= lk) break;
                    _i[c] = _i[m]; _k[c] = _k[m]; c = m;
                }
                if (_n > 0) { _i[c] = li; _k[c] = lk; }
                return true;
            }
        }

        // ---------- debug ([Debug] ShowNav) ----------
        private struct Mark { public Vector3 Next; public string Where; public float At; }
        private static readonly Dictionary<GameObject, Mark> _debug = new Dictionary<GameObject, Mark>();
        private static readonly List<GameObject> _dead = new List<GameObject>();
        private static void Remember(GameObject owner, Vector3 next, Structure s)
        {
            if (owner == null || !NpcModule.ShowNav.Value) return;
            _debug[owner] = new Mark { Next = next, Where = s.Name, At = Time.time };
        }

        // (1.1.0) a cover spot for an NPC at pos against a target at target: the nearest walkable cell of its own area on its map, 2 m or
        // more away, at least 4 m from the target, whose line to the target's chest is blocked (walls, rock, a car). Up to 80 lines.
        private const int CoverBlockers = (1 << 0) | (1 << 8) | (1 << 9) | (1 << 11) | (1 << 14);
        internal static bool CoverSpot(Vector3 pos, Vector3 target, float range, out Vector3 spot)
        {
            spot = pos;
            int from; var s = BakedAt(pos, out from);
            if (s == null || s.Comp == null) return false;
            int comp = s.Comp[from];
            int x0 = from % s.W, z0 = from / s.W, r = Mathf.Clamp(Mathf.CeilToInt(range / s.Cell), 2, 80);
            float minD2 = (2f / s.Cell) * (2f / s.Cell);
            // (WW perf) nearest first from the precomputed offset table (was: up to 161 x 161 cells collected and sorted with a capturing lambda)
            Offsets();
            Vector3 chest = target + Vector3.up * 1f;
            int tested = 0; long r2 = 2L * r * r;
            for (int k = 0; k < _offD2.Length && _offD2[k] <= r2; k++)
            {
                int dx = _offDx[k], dz = _offDz[k];
                if (dx < -r || dx > r || dz < -r || dz > r) continue;
                int cx = x0 + dx, cz = z0 + dz;
                if (cx < 0 || cz < 0 || cx >= s.W || cz >= s.H) continue;
                int i = cz * s.W + cx;
                if (float.IsNaN(s.FloorY[i]) || s.Comp[i] != comp || dx * dx + dz * dz < minD2) continue;
                Vector3 c = CellCenter(s, i % s.W, i / s.W, s.FloorY[i]);
                if ((c - target).sqrMagnitude < 16f) continue;
                if (++tested > 80) break;
                RaycastHit h;
                if (!Physics.Linecast(c + Vector3.up * 1.2f, chest, out h, CoverBlockers, QueryTriggerInteraction.Ignore)) continue;
                spot = c; return true;
            }
            return false;
        }

        // (1.1.0) Passthrough: the NPC stands on a map cell next to a wall / obstacle (a corridor, a doorway, a tunnel)
        internal static bool NearWall(Vector3 pos)
        {
            int c; var s = BakedAt(pos, out c);
            return s != null && s.Near != null && c >= 0 && s.Near[c];
        }

        // ---------- (1.7.2) for Nests (read-only) ----------
        // Floor points of root's map: walkable cells (room for a body), with roofedOnly only cells under a roof (a cave's inside, not the
        // ground around its mouth or the rock between its inner and outer walls), all in the one connected area that has the most such
        // cells. Up to max points, spread evenly over them. known = root is a mapped structure; baked = its map is ready.
        private static int[] _compCount = new int[0];
        internal static int FloorCells(Transform root, bool roofedOnly, List<Vector3> into, int max, out bool known, out bool baked)
        {
            known = false; baked = false; into.Clear();
            Structure s = null;
            foreach (var x in _structures) if (x.Root == root) { s = x; break; }
            if (s == null) return 0;
            known = true;
            if (!s.Baked || s.FloorY == null) return 0;
            baked = true;
            int n = s.W * s.H;
            int big = -1;
            if (s.Comp != null && s.CompSize != null && s.CompSize.Length > 0)
            {
                if (_compCount.Length < s.CompSize.Length) _compCount = new int[s.CompSize.Length];
                System.Array.Clear(_compCount, 0, s.CompSize.Length);
                for (int i = 0; i < n; i++)
                {
                    if (float.IsNaN(s.FloorY[i]) || (roofedOnly && (s.Open == null || s.Open[i]))) continue;
                    int c = s.Comp[i]; if (c >= 0 && c < s.CompSize.Length) _compCount[c]++;
                }
                int bs = 0; for (int c = 0; c < s.CompSize.Length; c++) if (_compCount[c] > bs) { bs = _compCount[c]; big = c; }
            }
            int eligible = 0;
            for (int i = 0; i < n; i++) if (Eligible(s, i, roofedOnly, big)) eligible++;
            if (eligible == 0) return 0;
            int stride = Mathf.Max(1, eligible / Mathf.Max(1, max)), k = 0;
            for (int i = 0; i < n && into.Count < max; i++)
            {
                if (!Eligible(s, i, roofedOnly, big)) continue;
                if (k++ % stride != 0) continue;
                into.Add(CellCenter(s, i % s.W, i / s.W, s.FloorY[i]));
            }
            return into.Count;
        }

        private static bool Eligible(Structure s, int i, bool roofedOnly, int big)
        {
            if (float.IsNaN(s.FloorY[i])) return false;
            if (roofedOnly && (s.Open == null || s.Open[i])) return false;
            if (big >= 0 && s.Comp != null && s.Comp[i] != big) return false;
            return true;
        }

        // ---------- for Idle (read-only) ----------
        // the root of the structure whose footprint contains p (any bake state), or null
        internal static Transform StructureRootAt(Vector3 p)
        {
            Transform best = null; float bestDy = float.MaxValue;
            foreach (var s in _structures)
            {
                if (s.Root == null || !Inside(s, p)) continue;
                float dy = Mathf.Abs(p.y - s.RefY);
                if (dy < bestDy) { bestDy = dy; best = s.Root; }
            }
            return best;
        }

        // the baked structure under p whose floor there is closest to p.y (the NPC is on that map), or null (not baked yet / outside)
        private static Structure BakedAt(Vector3 p, out int cell)
        {
            cell = -1; Structure best = null; float bestDy = float.MaxValue;
            foreach (var s in _structures)
            {
                int x, z;
                if (!s.Baked || s.FloorY == null || s.Root == null || !CellOf(s, p, out x, out z)) continue;
                int i = z * s.W + x;
                if (float.IsNaN(s.FloorY[i])) continue;
                float dy = Mathf.Abs(p.y - s.FloorY[i]);
                if (dy < 2.5f && dy < bestDy) { bestDy = dy; best = s; cell = i; }
            }
            return best;
        }

        // Up to max patrol points around home, each CLEARLY reachable on a straight line: every cell of the line walkable and connected to
        // the next one (Move: no wall, spike or step), the cells half a metre either side walkable too (elbow room), the end at least 1 m
        // from anything unwalkable, and a body-sized capsule swept along the line hits nothing solid. 16 bearings x 5 distances (longest
        // clear one per bearing), then the points are picked >= 60 deg apart, longest first. -1 = home's map is not baked (yet), 0 = none.
        internal static int PatrolPoints(Vector3 home, List<Vector3> pts, int max, float minD, float maxD)
        {
            _noHop = true;
            try { return PatrolPointsNoHop(home, pts, max, minD, maxD); }
            finally { _noHop = false; }
        }

        private static int PatrolPointsNoHop(Vector3 home, List<Vector3> pts, int max, float minD, float maxD)
        {
            pts.Clear();
            int hc; var s = BakedAt(home, out hc);
            if (s == null) return -1;
            float hy = s.FloorY[hc];
            Vector3 h0 = new Vector3(home.x, hy, home.z);
            var cand = new List<KeyValuePair<float, Vector3>>();     // bearing (deg), point
            float[] dists = { maxD, maxD * 0.8f, maxD * 0.6f, (maxD + minD) * 0.5f * 0.7f, minD };
            for (int b = 0; b < 16; b++)
            {
                float ang = b * 22.5f;
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                foreach (float d in dists)
                {
                    if (d < minD - 0.01f) continue;
                    Vector3 end;
                    if (ClearLine(s, h0, dir, d, out end)) { cand.Add(new KeyValuePair<float, Vector3>(ang, end)); break; }
                }
            }
            cand.Sort((a, c) => (c.Value - h0).sqrMagnitude.CompareTo((a.Value - h0).sqrMagnitude));
            foreach (var c in cand)
            {
                bool far = true;
                foreach (var p in pts) { if (Mathf.Abs(Mathf.DeltaAngle(Bearing(h0, p), c.Key)) < 60f) { far = false; break; } }
                if (!far) continue;
                pts.Add(c.Value);
                if (pts.Count >= max) break;
            }
            return pts.Count;
        }

        private static float Bearing(Vector3 from, Vector3 to) { Vector3 d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

        private static bool ClearLine(Structure s, Vector3 h0, Vector3 dir, float dist, out Vector3 end)
        {
            end = h0;
            int prev = -1, x, z;
            if (!CellOf(s, h0, out x, out z)) return false;
            int comp = s.Comp != null ? s.Comp[z * s.W + x] : -2;
            Vector3 side = new Vector3(dir.z, 0f, -dir.x) * 0.5f;
            float step = s.Cell * 0.5f;
            for (float t = 0f; t <= dist + 1e-3f; t += step)
            {
                Vector3 p = h0 + dir * t;
                if (!CellOf(s, p, out x, out z)) return false;
                int i = z * s.W + x;
                if (float.IsNaN(s.FloorY[i]) || (comp != -2 && s.Comp[i] != comp)) return false;
                if (prev >= 0 && prev != i && !Move(s, prev, i)) return false;
                prev = i;
                if (!Walkable(s, p + side) || !Walkable(s, p - side)) return false;
            }
            Vector3 e = h0 + dir * dist;
            if (!CellOf(s, e, out x, out z)) return false;
            for (int dz = -2; dz <= 2; dz++)                      // >= 1 m from anything unwalkable
                for (int dx = -2; dx <= 2; dx++)
                {
                    int cx = x + dx, cz = z + dz;
                    if (cx < 0 || cz < 0 || cx >= s.W || cz >= s.H || float.IsNaN(s.FloorY[cz * s.W + cx])) return false;
                }
            float ey = s.FloorY[z * s.W + x];
            end = new Vector3(e.x, ey, e.z);
            // physics: the body swept along the line (floors differ a little: from the higher end, 0.25 m clear of the floor)
            float top = Mathf.Max(h0.y, ey);
            Vector3 a1 = new Vector3(h0.x, top + 0.25f + Radius, h0.z), a2 = new Vector3(h0.x, top + HeadTop - Radius, h0.z);
            Vector3 flat = new Vector3(e.x - h0.x, 0f, e.z - h0.z);
            if (Physics.CapsuleCast(a1, a2, Radius + 0.05f, flat.normalized, flat.magnitude, BakeMask, QueryTriggerInteraction.Ignore)) return false;
            return true;
        }

        private static bool Walkable(Structure s, Vector3 p)
        {
            int x, z;
            return CellOf(s, p, out x, out z) && !float.IsNaN(s.FloorY[z * s.W + x]);
        }

        // Points for a search round around origin: the camp map's patrol points when origin is on a baked map, otherwise physics only:
        // 12 bearings, the longest of 3 distances where a body sweep is clear, the ground under the end (and the middle) is walkable
        // (normal >= WalkNormal, within 1.5 m of origin's height) - no cliffs, no rock; picked >= 60 deg apart. Returns the count, or
        // -(count) - 1 when it had to go by physics (so the caller can tell; 0 points then = -1).
        internal static int SearchPoints(Vector3 origin, List<Vector3> pts, int max, float minD, float maxD)
        {
            int r = PatrolPoints(origin, pts, max, minD, maxD);
            if (r >= 0) return r;
            pts.Clear();
            RaycastHit g;
            Vector3 o = origin;
            if (Physics.Raycast(origin + Vector3.up * 1f, Vector3.down, out g, 3f, (1 << 0) | (1 << 14), QueryTriggerInteraction.Ignore)) o = g.point;
            var cand = new List<KeyValuePair<float, Vector3>>();
            float[] dists = { maxD, (maxD + minD) * 0.5f, minD };
            for (int b = 0; b < 12; b++)
            {
                float ang = b * 30f;
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                foreach (float d in dists)
                {
                    Vector3 e = o + dir * d, m = o + dir * (d * 0.5f);
                    Vector3 ge, gm;
                    if (!Ground(e, o.y, out ge) || !Ground(m, o.y, out gm)) continue;
                    if (!BodyPathClear(o, ge)) continue;
                    if (Physics.Linecast(o + Vector3.up * 0.3f, ge + Vector3.up * 0.3f, BakeMask, QueryTriggerInteraction.Ignore)) continue;   // a kerb, a low wall, a fence
                    cand.Add(new KeyValuePair<float, Vector3>(ang, ge)); break;
                }
            }
            cand.Sort((a, c) => (c.Value - o).sqrMagnitude.CompareTo((a.Value - o).sqrMagnitude));
            foreach (var c in cand)
            {
                bool far = true;
                foreach (var p in pts) { if (Mathf.Abs(Mathf.DeltaAngle(Bearing(o, p), c.Key)) < 60f) { far = false; break; } }
                if (!far) continue;
                pts.Add(c.Value);
                if (pts.Count >= max) break;
            }
            return -pts.Count - 1;
        }

        private static bool Ground(Vector3 p, float refY, out Vector3 hit)
        {
            hit = p; RaycastHit h;
            if (!Physics.Raycast(new Vector3(p.x, refY + 2f, p.z), Vector3.down, out h, 4f, (1 << 0) | (1 << 14), QueryTriggerInteraction.Ignore)) return false;
            if (h.normal.y < WalkNormal || Mathf.Abs(h.point.y - refY) > 1.5f) return false;
            hit = h.point;
            return true;
        }

        // The ankle-high line between two neighbouring cells. Hitting something there used to make a wall - but the rim of a camp's base plate
        // (camp_base_rock, a few cm higher than the line) is just ground the body walks over: that made the orange ring around Camp_7 that
        // sealed the camp and its cave off from the outside. So when the line hits, measure the top of what it hit: no higher than
        // MaxStep above the higher of the two floors = a rim the body steps over (clear); higher = a lip / kerb / wall.
        private static bool LowWayClear(Vector3 a, Vector3 b, float topFloor)
        {
            RaycastHit h;
            Vector3 d = b - a;
            if (!Physics.Linecast(a + Vector3.up * 0.15f, b + Vector3.up * 0.15f, out h, BakeMask, QueryTriggerInteraction.Ignore)) return true;
            float step = Mathf.Max(0.1f, NpcModule.NavMaxStep.Value);
            // just past the hit point (into the obstacle), from above the highest a step could be, straight down
            Vector3 flat = new Vector3(d.x, 0f, d.z); if (flat.sqrMagnitude > 1e-6f) flat.Normalize();
            Vector3 probe = new Vector3(h.point.x, topFloor + step + 0.6f, h.point.z) + flat * 0.02f;
            RaycastHit top;
            if (!Physics.Raycast(probe, Vector3.down, out top, step + 0.6f + 0.3f, BakeMask, QueryTriggerInteraction.Ignore)) return false;
            // the top of the very thing the line hit (a thin rail the probe misses lands on the ground behind it: still a wall),
            // facing up, no higher than a step
            return top.collider == h.collider && top.point.y - topFloor <= step && top.normal.y >= 0.5f;
        }

        // (1.4.9) The way between two neighbouring cells for the whole body: the NPC's own capsule (r 0.28, up to the shoulders at 1.5 m)
        // swept from one cell centre to the other, from lo above the higher floor (0.3: what LowWayClear / a step leaves; 0.6 over a hop).
        // The old test was two thin lines (0.5 and 1.2 m) between the centres: a diagonal spike or a pipe at chest height passing between
        // them, or between two cell centres, was invisible to the map while the body ran into it.
        private static bool BodySweep(Vector3 a, Vector3 b, float top, float lo)
        {
            Vector3 d = new Vector3(b.x - a.x, 0f, b.z - a.z);
            float len = d.magnitude;
            if (len < 1e-4f) return true;
            Vector3 p1 = new Vector3(a.x, top + lo + BodyR, a.z), p2 = new Vector3(a.x, top + HeadTop - BodyR, a.z);
            if (p2.y < p1.y) p2 = p1;
            return !Physics.CapsuleCast(p1, p2, BodyR, d / len, len, BakeMask, QueryTriggerInteraction.Ignore);   // with the clearance margin
        }

        // a body capsule swept from a to b (patrol legs are re-checked before walking: a car parked there since the bake)
        internal static bool BodyPathClear(Vector3 a, Vector3 b)
        {
            Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
            float top = Mathf.Max(a.y, b.y);
            Vector3 a1 = new Vector3(a.x, top + 0.25f + Radius, a.z), a2 = new Vector3(a.x, top + HeadTop - Radius, a.z);
            return flat.sqrMagnitude < 0.01f || !Physics.CapsuleCast(a1, a2, Radius, flat.normalized, flat.magnitude, BakeMask, QueryTriggerInteraction.Ignore);
        }

        internal static void DrawDebug()
        {
            if (!On || !NpcModule.ShowNav.Value) return;
            var cyan = new Color(0.3f, 0.9f, 1f);
            var p = Player();
            foreach (var s in _structures)
            {
                if (s.Root == null) continue;
                if (p != null && s.Box.SqrDistance(p.position) > 150f * 150f) continue;
                DebugOverlay.Label(new Vector3(s.Box.center.x, s.RefY + 3f, s.Box.center.z), s.Name + (s.Baked ? (s.FloorY != null ? " baked, " + s.Walkable + "/" + (s.W * s.H) + " cells" : " (bake failed)") : _baking == s ? " baking " + (100 * s.Next / Math.Max(1, s.W * s.H)) + " %" : " not baked"), cyan);
            }
            float now = Time.time;
            var dead = _dead; dead.Clear();
            foreach (var kv in _debug)
            {
                if (kv.Key == null || now - kv.Value.At > 1.5f) { dead.Add(kv.Key); continue; }
                DebugOverlay.Mark(kv.Value.Next + Vector3.up * 0.3f, cyan, 8f);
                DebugOverlay.Label(kv.Value.Next + Vector3.up * 0.7f, "nav " + kv.Value.Where, cyan);
            }
            foreach (var k in dead) _debug.Remove(k);
        }

        // (WW) Core.Services.Npc.Structures: what Apocapatrol read from _structures by reflection (Root, Box, Baked, FloorY, W, Cell)
        internal static void Structures(List<Core.NavStructure> into)
        {
            if (into == null) return;
            into.Clear();
            foreach (var s in _structures)
            {
                if (s.Pub == null) s.Pub = new Core.NavStructure();
                var p = s.Pub;
                p.Root = s.Root; p.Box = s.Box; p.Baked = s.Baked; p.FloorY = s.FloorY; p.W = s.W; p.Cell = s.Cell;
                p.GridOrigin = s.GridOrigin; p.GridRight = s.GridRight; p.GridForward = s.GridForward;
                into.Add(p);
            }
        }

        // (WW) floating origin: the prefab-aligned grids are scene-space data. Box, grid origins and floor heights move with the world; the grid
        // (cells, edges, areas) is relative and stays. Route caches keyed by world positions (fields to outside goals, exit plans, memos,
        // debug marks) are dropped and rebuilt on the next think. A bake in progress continues on the shifted box.
        internal static void Shift(Vector3 d)
        {
            foreach (var s in _structures)
            {
                var b = s.Box; b.center += d; s.Box = b;
                s.GridOrigin += d;
                s.RefY += d.y;
                if (s.FloorY != null && d.y != 0f) for (int i = 0; i < s.FloorY.Length; i++) s.FloorY[i] += d.y;      // NaN stays NaN
                // (WW perf) only the fields seeded from a world point go (outside goals: ring cells weighted by the straight line to the goal);
                // fields seeded at a cell (inside goals, exit cells, NPC cells - keyed and computed in grid space) stay valid
                _expired.Clear();
                foreach (var kv in s.Fields) if (!kv.Value.GoalInside) _expired.Add(kv.Key);
                foreach (var k in _expired) { Return(s.Fields[k]); s.Fields.Remove(k); }
            }
            // relaxed fields are seeded at an exit cell (grid space): kept too
            PickReset();     // queued / running exit picks are for world-space goals (8 m buckets): dropped, asked again on the next think
            _outPlans.Clear(); _outMemo.Clear(); _exits.Clear(); _debug.Clear();
        }

        internal static int StructureCount { get { return _structures.Count; } }
        internal static int MaskedCount { get { int b = 0; foreach (var s in _structures) if (s.Mv != null) b++; return b; } }
        internal static int BakedCount { get { int b = 0; foreach (var s in _structures) if (s.Baked) b++; return b; } }
        internal static int LoadedMapCount { get { int b = 0; foreach (var s in _structures) if (s.Baked && s.LoadedMap) b++; return b; } }
        internal static int VanillaMapCount { get { int b = 0; foreach (var s in _structures) if (s.Baked && s.LoadedVanilla) b++; return b; } }
        internal static string BakingText { get { var s = _baking; return s == null ? "-" : s.Name + " " + (s.Phase == -1 ? "loading map" : s.Phase == 3 ? "saving map" : "phase " + s.Phase) + ", " + (100 * s.Next / Math.Max(1, s.W * s.H)) + " % of " + (s.W * s.H) + " cells, " + s.BakeMs.ToString("0") + " ms / " + s.BakeFrames + " frames"; } }
        internal static float MsPerCell { get { return _msPerCell; } }
        internal static bool Urgent { get { return _urgent; } }

        internal static string Status()
        {
            int b = 0; foreach (var s in _structures) if (s.Baked) b++;
            return _structures.Count + " structures, " + b + " baked";
        }
    }
}
