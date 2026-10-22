using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// The service side of the grid navigation (README 6.2, P6): waypoint providers consulted before routing (towns, custom POI exits),
    /// instance overlays applied to a structure's map before its areas are flooded, the make/bake/write hooks the town overlay uses, and the
    /// queries behind <see cref="NavApi"/> (structure lookup, discover, forget, path, cover, templates). Replaces the Harmony patches and
    /// reflection Towns used on Nav (TownBakeBridge, TownOverlap, PoiDressNavigation, TownTowerKit).
    /// </summary>
    internal static partial class Nav
    {
        // ------------------------------------------------------------------ change counter

        /// <summary>Increments whenever structures, overlays or waypoint providers change (INavApi.Revision).</summary>
        internal static int Revision { get; private set; }

        // ------------------------------------------------------------------ hooks for the town overlay (same module, no Harmony)

        private static readonly List<Func<Transform, bool>> _makeGates = new List<Func<Transform, bool>>();

        /// <summary>Called after a structure was made for a root (ex Harmony postfix on Make).</summary>
        internal static Action<Transform, Structure> Made;

        /// <summary>Called first thing when a structure's bake (re)starts (ex Harmony prefix on BeginBake).</summary>
        internal static Action<Structure> BeforeBake;

        /// <summary>Asked before a baked map is written to the reusable custom cache; false keeps the bake in memory only (ex prefix on CreateMap).</summary>
        internal static Func<Structure, bool> MapWritable;

        /// <summary>Adds a gate asked before a structure is made for a root: false = not now (the sweep / Discover asks again later).</summary>
        internal static void AddMakeGate(Func<Transform, bool> gate) { if (gate != null && !_makeGates.Contains(gate)) _makeGates.Add(gate); }

        /// <summary>Removes a make gate.</summary>
        internal static void RemoveMakeGate(Func<Transform, bool> gate) { _makeGates.Remove(gate); }

        // ------------------------------------------------------------------ waypoint providers (ex Harmony prefix/postfix on Nav.Next)

        private sealed class WaypointEntry { internal int Id, Order; internal IModule Owner; internal IWaypointProvider Provider; }
        private static readonly List<WaypointEntry> _waypoints = new List<WaypointEntry>();
        private static int _nextWaypointId = 1;

        /// <summary>Registers a waypoint provider (lower order first; equal orders in registration order). Returns its id.</summary>
        internal static int AddWaypointProvider(IModule owner, IWaypointProvider provider, int order)
        {
            if (provider == null) return 0;
            var e = new WaypointEntry { Id = _nextWaypointId++, Order = order, Owner = owner, Provider = provider };
            int at = _waypoints.Count;
            while (at > 0 && _waypoints[at - 1].Order > order) at--;
            _waypoints.Insert(at, e); Revision++;
            return e.Id;
        }

        /// <summary>Removes a waypoint provider by id.</summary>
        internal static void RemoveWaypointProvider(int id)
        {
            for (int i = _waypoints.Count - 1; i >= 0; i--) if (_waypoints[i].Id == id) { _waypoints.RemoveAt(i); Revision++; }
        }

        /// <summary>Removes every waypoint provider and overlay of an owner (its module stopped).</summary>
        internal static void RemoveOwned(IModule owner)
        {
            for (int i = _waypoints.Count - 1; i >= 0; i--) if (_waypoints[i].Owner == owner) { _waypoints.RemoveAt(i); Revision++; }
            for (int i = _overlays.Count - 1; i >= 0; i--) if (_overlays[i].Owner == owner) RemoveOverlay(_overlays[i].Id);
        }

        /// <summary>
        /// A waypoint toward goal for an NPC at pos, or false when no structure is involved / the way is straight. pathLeft = path length to
        /// the goal (inside) or to the exit plus the straight rest (outside), for the brain's progress check. The first waypoint provider
        /// with an opinion turns the goal into its waypoint for the routing below; when the map can't route there the waypoint itself is
        /// taken if the provider forces it (<see cref="IForcedWaypointProvider"/>) or the body's way to it is clear; the rest of the way
        /// (waypoint to goal) is added to pathLeft. (Same rules as the Towns bridge that patched this method before.)
        /// </summary>
        internal static bool Next(GameObject owner, Vector3 pos, Vector3 goal, out Vector3 next, out float pathLeft)
        {
            Vector3 waypoint; bool force;
            if (_waypoints.Count == 0 || !Waypoint(owner, pos, goal, out waypoint, out force)) return NextCore(owner, pos, goal, out next, out pathLeft);
            bool result = NextCore(owner, pos, waypoint, out next, out pathLeft);
            if (!result && (force || WaypointClear(pos, waypoint))) { next = waypoint; pathLeft = Vector3.Distance(pos, next); result = true; }
            if (result) pathLeft += Vector3.Distance(waypoint, goal);
            return result;
        }

        /// <summary>The first provider's waypoint (in order); force = take it even when the map can't route there.</summary>
        private static bool Waypoint(GameObject owner, Vector3 pos, Vector3 goal, out Vector3 waypoint, out bool force)
        {
            waypoint = goal; force = false;
            for (int i = 0; i < _waypoints.Count; i++)
            {
                var p = _waypoints[i].Provider;
                try
                {
                    var forced = p as IForcedWaypointProvider;
                    if (forced != null ? forced.TryNext(owner, pos, goal, out waypoint, out force) : p.TryNext(owner, pos, goal, out waypoint)) return true;
                }
                catch (Exception e) { Log.Error(_waypoints[i].Owner, "waypoint provider failed", e); }
                waypoint = goal; force = false;
            }
            return false;
        }

        /// <summary>A body-sized capsule fits at both ends and passes from a to b (no big height change): the waypoint can be walked to straight.</summary>
        internal static bool WaypointClear(Vector3 a, Vector3 b)
        {
            var delta = b - a; float length = delta.magnitude;
            if (Mathf.Abs(delta.y) > Mathf.Max(.5f, length * .2f)) return false;
            var bottom = a + Vector3.up * .5f; var top = a + Vector3.up * 1.4f;
            if (Physics.CheckCapsule(bottom, top, .25f, BakeMask, QueryTriggerInteraction.Ignore)) return false;
            if (Physics.CheckCapsule(b + Vector3.up * .5f, b + Vector3.up * 1.4f, .25f, BakeMask, QueryTriggerInteraction.Ignore)) return false;
            RaycastHit hit;
            return length < .01f || !Physics.CapsuleCast(bottom, top, .25f, delta / length, out hit, length, BakeMask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ instance overlays (INavApi.RegisterOverlay)

        private sealed class OverlayEntry { internal int Id; internal IModule Owner; internal Transform Root; internal INavOverlay Overlay; }
        private static readonly List<OverlayEntry> _overlays = new List<OverlayEntry>();
        private static int _nextOverlayId = 1;

        /// <summary>Registers an overlay for the structure of root; its baked map is rebuilt (from the saved/template map) with the overlay applied.</summary>
        internal static int AddOverlay(IModule owner, Transform root, INavOverlay overlay)
        {
            if (root == null || overlay == null) return 0;
            var e = new OverlayEntry { Id = _nextOverlayId++, Owner = owner, Root = root, Overlay = overlay };
            _overlays.Add(e); Revision++;
            RebakeRoot(root);
            return e.Id;
        }

        /// <summary>Removes an overlay; the structure's map is rebuilt without it.</summary>
        internal static void RemoveOverlay(int id)
        {
            for (int i = _overlays.Count - 1; i >= 0; i--)
            {
                if (_overlays[i].Id != id) continue;
                var root = _overlays[i].Root; _overlays.RemoveAt(i); Revision++;
                if (root != null) RebakeRoot(root);
            }
        }

        /// <summary>Phase-2 entry of a bake: applies the overlays registered for s.Root (blocked cells, floor heights); a changed map gets its
        /// areas flooded again and is never written to the reusable cache or kept as a template.</summary>
        private static void ApplyOverlays(Structure s)
        {
            s.OverlayChecked = true;
            if (_overlays.Count == 0 || s.Root == null || s.FloorY == null) return;
            bool changed = false; int n = s.W * s.H;
            for (int k = 0; k < _overlays.Count; k++)
            {
                var e = _overlays[k];
                if (e.Root != s.Root) continue;
                try
                {
                    if (!e.Overlay.Affects(s.Box)) continue;
                    for (int i = 0; i < n; i++)
                    {
                        var p = CellCenter(s, i % s.W, i / s.W, float.IsNaN(s.FloorY[i]) ? s.RefY : s.FloorY[i]);
                        float y;
                        if (e.Overlay.IsBlocked(p)) { if (!float.IsNaN(s.FloorY[i])) { s.FloorY[i] = float.NaN; s.Walkable--; } s.Why[i] = 2; s.Edges[i] = 0; changed = true; }
                        else if (e.Overlay.TryFloorHeight(p, out y) && !float.IsNaN(y) && !float.IsInfinity(y)) { if (float.IsNaN(s.FloorY[i])) s.Walkable++; s.FloorY[i] = y; s.Why[i] = 0; changed = true; }
                    }
                }
                catch (Exception ex) { Log.Error(e.Owner, "navigation overlay failed on " + s.Name, ex); }
            }
            if (!changed) return;
            s.Overlaid = true; s.LoadedTemplate = false; s.Comp = null; s.CompSize = null; s.CompStack = null;
        }

        /// <summary>Drops the baked map of every structure of root so the bake scheduler builds it again (fast from the saved/template map).</summary>
        private static void RebakeRoot(Transform root)
        {
            bool any = false;
            foreach (var s in _structures)
            {
                if (s.Root != root || s.FloorY == null) continue;
                CloseMap(s);
                foreach (var f in s.Fields.Values) Return(f);
                s.Fields.Clear();
                s.Baked = false; s.FloorY = null; s.Mv = s.Hop = s.MvB = s.HopB = null; s.MaskNext = 0;
                _maskQueue.Remove(s); _compRing.Remove(s);
                if (_baking == s) _baking = null;
                any = true;
            }
            if (!any) return;
            PickReset(); _outPlans.Clear(); _outMemo.Clear(); _exits.Clear();
            foreach (var f in _relaxedFields.Values) Return(f);
            _relaxedFields.Clear();
        }

        // ------------------------------------------------------------------ queries (NavApi)

        /// <summary>The public view of the structure whose footprint contains p (nearest base height), refreshed; null outside every footprint.</summary>
        internal static NavStructure StructureAt(Vector3 p)
        {
            Structure best = null; float bestDy = float.MaxValue;
            foreach (var s in _structures)
            {
                if (s.Root == null || !Inside(s, p)) continue;
                float dy = Mathf.Abs(p.y - s.RefY);
                if (dy < bestDy) { bestDy = dy; best = s; }
            }
            return best == null ? null : Publish(best);
        }

        /// <summary>Refreshes and returns s's public view (one reused object per structure).</summary>
        private static NavStructure Publish(Structure s)
        {
            if (s.Pub == null) s.Pub = new NavStructure();
            var p = s.Pub;
            p.Root = s.Root; p.Box = s.Box; p.Baked = s.Baked; p.FloorY = s.FloorY; p.W = s.W; p.Cell = s.Cell;
            p.GridOrigin = s.GridOrigin; p.GridRight = s.GridRight; p.GridForward = s.GridForward;
            return p;
        }

        /// <summary>Discovers the structure under root at once (town buildings, custom POIs, spawned camps) instead of waiting for the sweep.</summary>
        internal static void Discover(Transform root)
        {
            if (root == null) return;
            int before = _structures.Count;
            Spawned(root.gameObject);
            if (_structures.Count != before) Revision++;
        }

        /// <summary>Forgets every structure of root (rebuilt / streamed out / re-dressed): its map job is closed, its floor colliders and
        /// queued masks dropped, the scheduler and route caches reset; a later sweep or Discover finds it again (ex PoiDressNavigation reflection).</summary>
        internal static void Forget(Transform root)
        {
            if (root == null) return;
            for (int i = _structures.Count - 1; i >= 0; i--)
            {
                var s = _structures[i];
                if (s.Root != root) continue;
                CloseMap(s);
                if (_baking == s) _baking = null;
                if (_needed == s) _needed = null;
                if (s.MapColliders != null) foreach (var c in s.MapColliders) if (c != null) _floorCols.Remove(c.GetInstanceID());
                _maskQueue.Remove(s); _structures.RemoveAt(i);
            }
            _known.Remove(root.GetInstanceID()); PickReset();
            _compRing.Clear(); _outPlans.Clear(); _outMemo.Clear(); _exits.Clear(); _relaxedFields.Clear(); _debug.Clear();
            Revision++;
        }

        /// <summary>
        /// Path of scene points from one point to another (cleared first): from, the corners of the map route (string-pulled on the grid's
        /// line of sight), to. Outside every footprint the straight line; false when the start's structure has no map yet or the map has no way.
        /// </summary>
        internal static bool TryPath(Vector3 from, Vector3 to, List<Vector3> into)
        {
            if (into == null) return false;
            into.Clear();
            int fc; var s = BakedAt(from, out fc);
            if (s == null)
            {
                if (StructureRootAt(from) != null) return false;      // in a footprint that is not mapped yet
                into.Add(from); into.Add(to); return true;
            }
            int gx, gz;
            bool inside = CellOf(s, to, out gx, out gz);
            int gc = inside ? NearestWalkable(s, gx, gz, 4) : -1;
            if (inside && gc < 0) return false;
            var f = FieldFor(s, to, inside, gc, fc);
            if (float.IsInfinity(f.Dist[fc])) return false;
            into.Add(from);
            int anchor = fc, prev = fc, cur = fc, guard = s.W * s.H;
            while (guard-- > 0)
            {
                int nb = Downhill(s, f, cur);
                if (nb < 0) break;
                if (!GridSight(s, anchor, nb)) { into.Add(CellCenter(s, prev % s.W, prev / s.W, s.FloorY[prev])); anchor = prev; }
                prev = cur = nb;
            }
            if (cur != anchor) into.Add(CellCenter(s, cur % s.W, cur / s.W, s.FloorY[cur]));
            into.Add(to);
            return true;
        }

        /// <summary>Template info for a POI prefab: the first saved map found (embedded vanilla pack, plugin vanilla folder, custom cache)
        /// with its map-key signature, plus the PoiNav link count of the type's data (when loaded).</summary>
        internal static bool TryGetTemplate(string prefabName, out NavTemplateInfo info)
        {
            info = new NavTemplateInfo { Prefab = prefabName };
            if (string.IsNullOrEmpty(prefabName)) return false;
            string prefix = VanillaResourcePrefix + prefabName + "-";
            string found = null; bool custom = false;
            foreach (string resource in DiskResources.Names)
                if (resource.StartsWith(prefix, StringComparison.Ordinal) && resource.EndsWith(".wwnav", StringComparison.Ordinal)) { found = resource.Substring(VanillaResourcePrefix.Length); break; }
            if (found == null) found = FirstMap(VanillaMapDir, prefabName);
            if (found == null) { found = FirstMap(CustomMapDir, prefabName); custom = found != null; }
            var data = PoiNavStore.Get(PoiNavStore.TypeOf(prefabName));
            info.Links = data != null ? data.Links.Count : 0;
            if (found == null) return data != null;
            string name = System.IO.Path.GetFileNameWithoutExtension(found);
            int dash = name.LastIndexOf('-');
            info.Signature = dash >= 0 ? name.Substring(dash + 1) : name;
            info.Custom = custom; info.Baked = true;
            return true;
        }

        /// <summary>The first (ordinal order) "&lt;prefab&gt;-*.wwnav" file name in a folder, or null.</summary>
        private static string FirstMap(string directory, string prefabName)
        {
            try
            {
                if (!Directory.Exists(directory)) return null;
                var files = Directory.GetFiles(directory, prefabName + "-*.wwnav");
                if (files.Length == 0) return null;
                Array.Sort(files, StringComparer.Ordinal);
                return System.IO.Path.GetFileName(files[0]);
            }
            catch (Exception) { return null; }
        }
    }

    /// <summary>A waypoint provider that can insist on its waypoint: the NPC walks to it even when the grid map has no route there
    /// (town approach detours and gate entrances lie outside every building map).</summary>
    internal interface IForcedWaypointProvider : IWaypointProvider
    {
        /// <summary>Like <see cref="IWaypointProvider.TryNext"/>; force = take the waypoint even when no map routes there.</summary>
        bool TryNext(GameObject npc, Vector3 fromScene, Vector3 goalScene, out Vector3 waypointScene, out bool force);
    }
}
