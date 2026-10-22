using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;


namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// Instance overlays only: a town never edits or writes a prefab's reusable .wwnav map.
    /// </summary>
    internal sealed class TownOverlap : IDisposable
    {
        /// <summary>One building's captured map with its overlaid copy.</summary>
        private sealed class Grid
        {
            internal Nav.Structure Native;
            internal int W, H;
            internal float Cell, AnchorY;
            internal Vector3 Origin, Right, Forward;
            internal float[] Original, Floor, Local;
            internal byte[] OriginalWhy, OriginalEdges, Why, Edges;
            internal bool[] OriginalNear, Near;
            internal int[] OriginalComp, OriginalSizes, Comp;
            internal List<int> Sizes = new List<int>();
            internal Stack<int> Stack = new Stack<int>();
            internal int Scan, Area = -1, Size;
            internal bool Applied;
            internal bool Props, Changed;
            internal int Lot;
            internal TownNavPatch Patch;   // worker-computed overlay for this lot; null = runtime projection fallback
            internal Grid[] Neighbours;
            internal Vector4 Bounds;
            /// <summary>Anchor-relative centre of cell i.</summary>
            internal Vector3 Point(int i) { return Origin + Right * ((i % W + .5f) * Cell) + Forward * ((i / W + .5f) * Cell); }
            /// <summary>Cell index of an anchor-relative point, or -1 outside.</summary>
            internal int Index(Vector3 p)
            { var d = p - Origin; int x = Mathf.FloorToInt(Vector3.Dot(d, Right) / Cell), z = Mathf.FloorToInt(Vector3.Dot(d, Forward) / Cell); return x < 0 || z < 0 || x >= W || z >= H ? -1 : z * W + x; }
            /// <summary>True when cell i of the original map is wall or rock.</summary>
            internal bool Solid(int i) { return i >= 0 && (OriginalWhy[i] == 2 || OriginalWhy[i] == 3); }
            /// <summary>True when two neighbouring cells are joined (original or overlaid arrays): walk edge within the step or hop edge.</summary>
            internal bool Step(int a, int b, bool original)
            {
                var floor = original ? Local : Floor; var edge = original ? OriginalEdges : Edges;
                if (float.IsNaN(floor[a]) || float.IsNaN(floor[b])) return false;
                int dx = b % W - a % W, dz = b / W - a / W;
                if (Mathf.Abs(dx) > 1 || Mathf.Abs(dz) > 1) return false;
                if (dx == 0 && dz == 0) return true;
                if (dz < 0 || dz == 0 && dx < 0) { int t = a; a = b; b = t; dx = -dx; dz = -dz; }
                int k = dz == 0 ? 0 : dx == 0 ? 1 : dx > 0 ? 2 : 3;
                return Mathf.Abs(floor[a] - floor[b]) <= _step && (edge[a] & (1 << k)) != 0 || k < 2 && (edge[a] & (1 << (k + 4))) != 0;
            }
            /// <summary>A step without cutting a corner (both side cells joined for a diagonal).</summary>
            internal bool Move(int a, int b)
            {
                if (!Step(a, b, false)) return false;
                int ax = a % W, az = a / W, bx = b % W, bz = b / W;
                return ax == bx || az == bz || Step(a, az * W + bx, false) && Step(a, bz * W + ax, false);
            }
        }
        private static readonly Dictionary<Transform, Nav.Structure> Native = new Dictionary<Transform, Nav.Structure>();
        private static readonly Dictionary<Nav.Structure, TownOverlap> Owners = new Dictionary<Nav.Structure, TownOverlap>();
        private static float _step;
        private readonly TownNavContext _site;
        private Grid[] _grids;
        private int _grid, _cell, _phase, _blocked, _edges;
        private float _retry;
        private bool _warming, _dressing, _planned;
        /// <summary>The overlay is applied (or given up on: FinishPartial).</summary>
        internal bool Ready { get; private set; }
        /// <summary>Preparation progress over captured grids and their three phases.</summary>
        internal float Progress { get { return _grids == null ? 0 : _grid >= _grids.Length ? 1
            : (_grid + (_phase + (_phase == 2 ? _grids[_grid].Scan : _cell) / (float)_grids[_grid].Floor.Length) / 3) / _grids.Length; } }
        /// <summary>Overlay of one town (dressing = wall/tower dressing boxes count too).</summary>
        internal TownOverlap(TownNavContext site, bool dressing) { _site = site; _dressing = dressing; }
        /// <summary>Walls/towers/props arrived after the first overlay (ownership confirmed later): redo it with the dressing boxes.</summary>
        internal void Reapply(bool dressing)
        { Restore(); _grids = null; _grid = _cell = _phase = _blocked = _edges = 0; _dressing = dressing; _warming = false; _retry = 0; Ready = false; }
        /// <summary>Hooks the town overlay into the NPCAI baker (ex Harmony patches on Nav.Make / BeginBake / CreateMap).</summary>
        internal static void Install()
        {
            _step = NavSettings.Step;
            Nav.Made += Made; Nav.BeforeBake += BeforeBake; Nav.MapWritable = Writable;
        }

        /// <summary>Removes the hooks (Towns stopped).</summary>
        internal static void Uninstall()
        {
            Nav.Made -= Made; Nav.BeforeBake -= BeforeBake;
            if (Nav.MapWritable == (Func<Nav.Structure, bool>)Writable) Nav.MapWritable = null;
        }

        /// <summary>A town instance's rebake must never replace a reusable standalone building template on disk.</summary>
        private static bool Writable(Nav.Structure s)
        {
            if (!(ModApi.Get<ITownsApi>()?.IsTownBuilding(s.Root)??false)) return true;
            // A manual rebake here sees neighbouring town geometry through physics. It may be used
            // for this instance, but must never replace a reusable standalone building template.
            Log.Main(NavRuntime.Owner, "instance bake kept in memory; reusable template file preserved."); return false;
        }

        /// <summary>Remembers the structure made for a town building (the overlay grids read it).</summary>
        private static void Made(Transform root, Nav.Structure s) { if (s != null && (ModApi.Get<ITownsApi>()?.IsTownBuilding(root)??false)) Native[root] = s; }
        /// <summary>Forgets the structure remembered for a removed town building.</summary>
        internal static void Forget(Transform root)
        {
            Nav.Structure native;
            if (Native.TryGetValue(root, out native)) { Native.Remove(root); Owners.Remove(native); }
        }
        /// <summary>A town building's map is rebaked: its overlay is undone and planned again; street routing restarts.</summary>
        private static void BeforeBake(Nav.Structure s)
        {
            TownOverlap owner;
            if (Owners.TryGetValue(s, out owner))
            { owner.Restore(); owner._grids = null; owner._grid = owner._cell = owner._phase = 0; owner.Ready = false; TownNavigationApi.ResetRouting(owner._site); }
        }
        /// <summary>Captures every building's baked map as an overlay grid (warming unbaked templates first); false = not ready yet.</summary>
        private bool Capture()
        {
            _warming = false;
            for (int lot = 0; lot < _site.Buildings.Length; lot++)
            {
                if (_site.Plan.Lots[lot].IsParking || _site.Plan.Lots[lot].Skipped || _site.Buildings[lot] == null) continue;
                var building = _site.Buildings[lot];
                Nav.Structure native;
                if (building == null || !Native.TryGetValue(building.transform, out native)) return false;
                if (!native.Baked)
                {
                    // Load the matching native template in its existing sliced pipeline, even when
                    // a distant town lies outside the player-driven bake discovery radius.
                    if (native.FloorY == null) Nav.BeginBake(native, true);
                    if (native.Phase == 0)
                        throw new InvalidOperationException("Matching saved bake unavailable for " + building.name + "; refusing an in-town physics bake during loading");
                    Nav.BakeStep(native); _warming = true; return false;
                }
            }
            var lots = new List<int>(); for (int lot = 0; lot < _site.Buildings.Length; lot++)
                if (!_site.Plan.Lots[lot].IsParking && !_site.Plan.Lots[lot].Skipped && _site.Buildings[lot] != null) lots.Add(lot);
            // Parking shells have their own navigation; publish only a completely captured overlay.
            var grids = new Grid[lots.Count]; var overlay = _site.Plan.Overlay; _planned = overlay != null && overlay != null && overlay.Length == _site.Plan.LotCount;
            for (int i = 0; i < grids.Length; i++)
            {
                var g = new Grid { Native = Native[_site.Buildings[lots[i]].transform], AnchorY = _site.Anchor.position.y, Lot = lots[i] };
                grids[i] = g;
                g.W = g.Native.W; g.H = g.Native.H; g.Cell = g.Native.Cell;
                g.Origin = g.Native.GridOrigin - _site.Anchor.position;
                g.Right = g.Native.GridRight; g.Forward = g.Native.GridForward;
                g.Bounds = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);
                for (int c = 0; c < 4; c++)
                {
                    var p = g.Origin + g.Right * ((c & 1) == 0 ? 0 : g.W * g.Cell) + g.Forward * ((c & 2) == 0 ? 0 : g.H * g.Cell);
                    g.Bounds.x = Mathf.Min(g.Bounds.x, p.x - g.Cell); g.Bounds.y = Mathf.Min(g.Bounds.y, p.z - g.Cell);
                    g.Bounds.z = Mathf.Max(g.Bounds.z, p.x + g.Cell); g.Bounds.w = Mathf.Max(g.Bounds.w, p.z + g.Cell);
                }
                g.Original = g.Native.FloorY; g.OriginalWhy = g.Native.Why; g.OriginalEdges = g.Native.Edges;
                g.OriginalNear = g.Native.Near; g.OriginalComp = g.Native.Comp; g.OriginalSizes = g.Native.CompSize;
                g.Local = new float[g.Original.Length]; for (int c = 0; c < g.Local.Length; c++) g.Local[c] = g.Original[c] - g.AnchorY;
                g.Floor = (float[])g.Local.Clone(); g.Why = (byte[])g.OriginalWhy.Clone(); g.Edges = (byte[])g.OriginalEdges.Clone();
                g.Near = new bool[g.Floor.Length]; g.Comp = new int[g.Floor.Length]; for (int c = 0; c < g.Comp.Length; c++) g.Comp[c] = -1;
            }
            foreach (var g in grids)
            {
                var neighbours = new List<Grid>();
                foreach (var other in grids)
                    if (other != g && other.Bounds.x <= g.Bounds.z && other.Bounds.z >= g.Bounds.x
                        && other.Bounds.y <= g.Bounds.w && other.Bounds.w >= g.Bounds.y) neighbours.Add(other);
                g.Neighbours = neighbours.ToArray();
                foreach (var prop in _site.DebrisBlockers)
                    if (prop.min.x <= g.Bounds.z && prop.max.x >= g.Bounds.x && prop.min.z <= g.Bounds.w && prop.max.z >= g.Bounds.y) { g.Props = true; break; }
                g.Changed = g.Neighbours.Length > 0 || g.Props;
                var patch = _planned ? overlay[g.Lot] : null;
                if (patch != null && patch.W == g.W && patch.H == g.H && Mathf.Abs(patch.Cell - g.Cell) < .0001f) { g.Patch = patch; g.Changed = patch.Any(_dressing); }
                else if (patch != null) Log.Warn(NavRuntime.Owner, "Towns: overlay plan mismatch for " + _site.Plan.Lots[g.Lot].Prefab + " (" + patch.W + "x" + patch.H + " vs " + g.W + "x" + g.H + "); runtime projection used.");
            }
            _grids = grids; foreach (var g in grids) Owners[g.Native] = this;
            return true;
        }
        /// <summary>Applies the worker-computed lot patch: solid cells and cleared edges.</summary>
        private void ApplyPatch(Grid g)
        {
            var patch = g.Patch;
            foreach (int i in patch.BaseSolid)
            { if (g.Why[i] != 2 && g.Why[i] != 3) _blocked++; g.Floor[i] = float.NaN; g.Why[i] = 2; g.Edges[i] = 0; }
            if (_dressing) foreach (int i in patch.DressSolid)
            { if (g.Why[i] != 2 && g.Why[i] != 3) _blocked++; g.Floor[i] = float.NaN; g.Why[i] = 2; g.Edges[i] = 0; }
            for (int k = 0; k < patch.EdgeCells.Length; k++)
            { int i = patch.EdgeCells[k]; if ((g.Edges[i] & patch.EdgeClear[k]) != 0) { g.Edges[i] &= (byte)~patch.EdgeClear[k]; _edges++; } }
        }
        /// <summary>Up to 32 walkable cells of root's map within 4 m of an entrance, nearest first, in the entrance's area.</summary>
        internal static int PortalCandidates(Transform root, Vector3 entrance, List<Vector3> into, out bool known, out bool baked)
        {
            into.Clear(); Nav.Structure native; known = Native.TryGetValue(root, out native); baked = known && native.Baked;
            if (!baked) return 0;
            var floor = native.FloorY; if (floor == null) return 0;
            var comp = native.Comp;
            int w = native.W, h = native.H; float cell = native.Cell;
            var origin = native.GridOrigin; var right = native.GridRight; var forward = native.GridForward;
            var delta = entrance - origin; int x = Mathf.FloorToInt(Vector3.Dot(delta, right) / cell), z = Mathf.FloorToInt(Vector3.Dot(delta, forward) / cell);
            int entry = x >= 0 && z >= 0 && x < w && z < h ? z * w + x : -1;
            int component = entry >= 0 && comp != null && !float.IsNaN(floor[entry]) ? comp[entry] : -1;
            int radius = Mathf.CeilToInt(4 / cell);
            for (int cz = Mathf.Max(0, z - radius); cz <= Mathf.Min(h - 1, z + radius); cz++)
                for (int cx = Mathf.Max(0, x - radius); cx <= Mathf.Min(w - 1, x + radius); cx++)
                {
                    int i = cz * w + cx;
                    if (float.IsNaN(floor[i]) || component >= 0 && comp != null && comp[i] != component) continue;
                    var p = origin + right * ((cx + .5f) * cell) + forward * ((cz + .5f) * cell); p.y = floor[i];
                    float cost = (p - entrance).sqrMagnitude; if (cost > 16) continue;
                    int at = into.FindIndex(old => (old - entrance).sqrMagnitude > cost); if (at < 0) at = into.Count;
                    if (at >= 32) continue; into.Insert(at, p); if (into.Count > 32) into.RemoveAt(32);
                }
            return into.Count;
        }
        /// <summary>One preparation slice: capture, project neighbours/props or apply the patch, near flags, areas; commits every grid together at the end.</summary>
        internal bool Step()
        {
            if (Ready || Time.unscaledTime < _retry) return false;
            if (_grids == null && !Capture()) { if (!_warming) _retry = Time.unscaledTime + .5f; return _warming; }
            if (_grid == _grids.Length)
            {
                // Commit all copies together; native NPC queries never see half-projected maps.
                foreach (var g in _grids) if (g.Changed) Apply(g);
                Invalidate(); Ready = true;
                if (Log.L1) Log.Main(NavRuntime.Owner, "Towns: global blockers " + _site.Plan.Key + ": " + _blocked + " cells, " + _edges + " edges shared across " + _grids.Length + " separate baked maps" + (_planned ? " (planned overlay" + (_dressing ? ", with perimeter dressing)" : ")") : " (runtime projection)") + ".");
                return true;
            }
            var target = _grids[_grid]; int n = target.Floor.Length;
            if (!target.Changed) { _grid++; _cell = _phase = 0; return true; }
            for (int ops = 0; ops < 64; ops++)
            {
                if (_phase == 0)
                {
                    if (target.Patch != null) { ApplyPatch(target); _phase = 1; _cell = 0; continue; }
                    if (_cell == n) { _phase = 1; _cell = 0; continue; }
                    Project(target, _cell++);
                }
                else if (_phase == 1)
                {
                    if (_cell == n) { _phase = 2; _cell = 0; continue; }
                    int i = _cell++; int x = i % target.W, z = i / target.W;
                    if (float.IsNaN(target.Floor[i])) continue;
                    for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int cx = x + dx, cz = z + dz; if (cx < 0 || cz < 0 || cx >= target.W || cz >= target.H) continue;
                        int j = cz * target.W + cx;
                        if (target.Why[j] == 2 || target.Why[j] == 3 || !float.IsNaN(target.Floor[j]) && !target.Step(i, j, false)) target.Near[i] = true;
                    }
                }
                else
                {
                    if (Components(target)) { _grid++; _cell = _phase = 0; break; }
                }
            }
            return true;
        }
        /// <summary>Runtime projection of one cell: debris props and neighbouring buildings' walls block it, closed neighbour edges cut its edges.</summary>
        private void Project(Grid target, int i)
        {
            var point = target.Point(i);
            if (target.Props && !float.IsNaN(target.Local[i])) foreach (var prop in _site.DebrisBlockers)
                if (point.x >= prop.min.x && point.x <= prop.max.x && point.z >= prop.min.z && point.z <= prop.max.z
                    && target.Local[i] + 1.8f >= prop.min.y && target.Local[i] + .1f <= prop.max.y)
                { target.Floor[i] = float.NaN; target.Why[i] = 2; target.Edges[i] = 0; _blocked++; break; }
            foreach (var source in target.Neighbours)
            {
                if (source == target) continue;
                // Why 2/3 are body-height obstructions relative to this building's floor band.
                if (Mathf.Abs(source.AnchorY - target.AnchorY) > 2.5f) continue;
                int a = source.Index(point);
                if (source.Solid(a))
                {
                    if (target.Why[i] != 2 && target.Why[i] != 3) _blocked++;
                    target.Floor[i] = float.NaN; target.Why[i] = 2; target.Edges[i] = 0; continue;
                }
                if (float.IsNaN(target.Local[i])) continue;
                // Project closed baked edges too: a thin wall can have valid floor on both sides.
                for (int k = 0; k < 4; k++)
                {
                    int dx = k == 0 || k == 2 ? 1 : k == 3 ? -1 : 0, dz = k == 0 ? 0 : 1;
                    int x = i % target.W + dx, z = i / target.W + dz;
                    if (x < 0 || z < 0 || x >= target.W || z >= target.H) continue;
                    int bits = (1 << k) | (k < 2 ? 1 << (k + 4) : 0);
                    if ((target.Edges[i] & bits) == 0) continue;
                    var end = target.Point(z * target.W + x);
                    int previous = a, samples = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(point, end) / (source.Cell * .25f)));
                    for (int t = 1; t <= samples; t++)
                    {
                        int b = source.Index(Vector3.Lerp(point, end, t / (float)samples));
                        bool blocked = source.Solid(b) || previous >= 0 && b >= 0 && previous != b && !float.IsNaN(source.Local[previous]) && !float.IsNaN(source.Local[b]) && !source.Step(previous, b, true);
                        if (blocked) { target.Edges[i] &= (byte)~bits; _edges++; break; }
                        previous = b;
                    }
                }
            }
        }
        /// <summary>One slice of the area flood fill over the overlaid grid; true when done.</summary>
        private static bool Components(Grid g)
        {
            if (g.Stack.Count > 0)
            {
                int c = g.Stack.Pop(); g.Size++; int x = c % g.W, z = c / g.W;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = x + dx, cz = z + dz; if (dx == 0 && dz == 0 || cx < 0 || cz < 0 || cx >= g.W || cz >= g.H) continue;
                    int j = cz * g.W + cx; if (g.Comp[j] >= 0 || !g.Move(c, j)) continue;
                    g.Comp[j] = g.Area; g.Stack.Push(j);
                }
                return false;
            }
            if (g.Area >= 0) { g.Sizes.Add(g.Size); g.Area = -1; }
            // Bound the scan as well as the flood; large blocked regions must yield.
            for (int n = 0; n < 64 && g.Scan < g.Floor.Length; n++, g.Scan++)
                if (g.Comp[g.Scan] < 0 && !float.IsNaN(g.Floor[g.Scan]))
                { g.Area = g.Sizes.Count; g.Size = 0; g.Comp[g.Scan] = g.Area; g.Stack.Push(g.Scan++); return false; }
            return g.Scan == g.Floor.Length;
        }
        /// <summary>Swaps the overlaid arrays into the structure (heights back to scene space).</summary>
        private void Apply(Grid g)
        {
            for (int i = 0; i < g.Floor.Length; i++) g.Floor[i] += _site.Anchor.position.y;
            g.Native.FloorY = g.Floor; g.Native.Why = g.Why; g.Native.Edges = g.Edges; g.Native.Near = g.Near;
            g.Native.Comp = g.Comp; g.Native.CompSize = g.Sizes.ToArray(); g.Applied = true;
            Reset(g);
        }
        /// <summary>The structure's arrays changed: cached route fields and move masks go, masks are queued again.</summary>
        private static void Reset(Grid g) { Nav.ResetDerived(g.Native); }

        /// <summary>Every structure-independent route cache goes (exit picks, out plans, memos, relaxed fields, ring areas).</summary>
        private static void Invalidate() { Nav.InvalidateRouteCaches(); }
        /// <summary>Puts the original arrays back into every overlaid structure.</summary>
        private void Restore()
        {
            if (_grids == null) return;
            foreach (var g in _grids)
            {
                Owners.Remove(g.Native);
                if (!g.Applied || !ReferenceEquals(g.Native.FloorY, g.Floor)) continue;
                // The detached original array stopped participating in native floating-origin shifts.
                float anchorY = _site.Anchor != null ? _site.Anchor.position.y : g.AnchorY;
                for (int i = 0; i < g.Original.Length; i++) g.Original[i] = g.Local[i] + anchorY;
                g.Native.FloorY = g.Original; g.Native.Why = g.OriginalWhy; g.Native.Edges = g.OriginalEdges;
                g.Native.Near = g.OriginalNear; g.Native.Comp = g.OriginalComp; g.Native.CompSize = g.OriginalSizes;
                g.Applied = false; Reset(g);
            }
            Invalidate();
        }
        /// <summary>Town removed: originals restored, building structures forgotten.</summary>
        public void Dispose()
        {
            Restore();
            foreach (var building in _site.Buildings) if (building != null) Forget(building.transform);
        }
        /// <summary>Loading must go on: applies the grids done so far and reports ready.</summary>
        internal void FinishPartial()
        {
            if (_grids != null)
            {
                for (int i = 0; i < _grid; i++) if (_grids[i].Changed) Apply(_grids[i]);
                Invalidate();
            }
            Ready = true;
        }
    }
}
