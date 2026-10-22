using System;
using System.Collections.Generic;
using Den.Tools;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Core
{
    /// <summary>
    /// One sliced walker over the loaded scenes and every applied MapMagic tile, replacing 9 module walkers (CleanHUD SceneCache,
    /// Quests QuestWorld, EngineSimCompat EngineDiscovery, StewPot "Existing pots", NPC Nav structure sweep, Gungirl, Crosshair,
    /// Patrols TrafficLocations/Persistence). Modules register watches (exact name, name prefix, component type, or a custom
    /// filter); every matching Transform is reported once through Added and kept in <see cref="WorldWatch.Found"/>; destroyed
    /// ones are swept in slices and reported through Removed. Prune filters stop the walk below a node (vehicles never hold
    /// camps). Roots are queued on scene load, on tile Applied, by <see cref="QueueRoot"/> (spawned objects) and by
    /// <see cref="Rescan"/>. Everything is cleared on scene load (no Removed calls for that; modules clear on scene load too).
    /// Cost: one Core sliced job ("Core.WorldIndex", <see cref="BudgetMicros"/> per frame); one Transform.name read per node
    /// only when a name-based watch exists; one GetComponents per node per component watch. Nothing runs without watches.
    /// </summary>
    public static class WorldIndex
    {
        /// <summary>Per-frame budget of the walker job in microseconds (default 300).</summary>
        public static int BudgetMicros = 300;
        /// <summary>Incremented each time the queue runs empty (a full pass over everything queued finished).</summary>
        public static int Revision;
        /// <summary>Nodes visited since start (Debug census).</summary>
        public static long Visited;

        static readonly List<WorldWatch> _watches = new List<WorldWatch>();
        static readonly Dictionary<string, List<WorldWatch>> _byName = new Dictionary<string, List<WorldWatch>>(StringComparer.Ordinal);
        static readonly List<WorldWatch> _prefix = new List<WorldWatch>(), _component = new List<WorldWatch>(), _custom = new List<WorldWatch>();
        static readonly List<Prune> _prunes = new List<Prune>();
        static readonly Stack<Transform> _walk = new Stack<Transform>();
        static readonly HashSet<int> _queued = new HashSet<int>();
        static readonly List<GameObject> _roots = new List<GameObject>();
        static bool _installed, _rootsPending;
        static int _sweepWatch;
        static Job _job;

        /// <summary>Owner + filter that stops the walk below matching nodes.</summary>
        private struct Prune { public IModule Owner; public WorldWatch.Filter Filter; }

        /// <summary>Nodes waiting to be visited.</summary>
        public static int Pending { get { return _walk.Count; } }
        /// <summary>True while a walk is in progress or roots are about to be queued.</summary>
        public static bool Busy { get { return _walk.Count > 0 || _rootsPending; } }

        /// <summary>Reports every Transform whose name equals name exactly (e.g. "mapIcon", "Weapons", "__GameManager__").</summary>
        public static WorldWatch WatchName(IModule owner, string name, WorldWatch.Visitor added, Action<int> removed = null)
        {
            var w = new WorldWatch(owner, WorldWatch.Kind.Name, name, added, removed);
            List<WorldWatch> list;
            if (!_byName.TryGetValue(name, out list)) _byName[name] = list = new List<WorldWatch>(1);
            list.Add(w);
            return Add(w);
        }

        /// <summary>Reports every Transform whose name starts with prefix (ordinal; e.g. "Camp_", "Building_").</summary>
        public static WorldWatch WatchPrefix(IModule owner, string prefix, WorldWatch.Visitor added, Action<int> removed = null)
        {
            var w = new WorldWatch(owner, WorldWatch.Kind.Prefix, prefix, added, removed);
            _prefix.Add(w);
            return Add(w);
        }

        /// <summary>Reports every Transform carrying a T component (first component of the node is passed as Component).</summary>
        public static WorldWatch WatchComponent<T>(IModule owner, WorldWatch.Visitor added, Action<int> removed = null) where T : Component
        {
            var w = new ComponentWatch<T>(owner, added, removed);
            _component.Add(w);
            return Add(w);
        }

        /// <summary>Reports every Transform for which filter(t, name) is true (name is null unless a name-based watch exists or needName).</summary>
        public static WorldWatch Watch(IModule owner, WorldWatch.Filter filter, WorldWatch.Visitor added, Action<int> removed = null, bool needName = true)
        {
            var w = new WorldWatch(owner, WorldWatch.Kind.Custom, null, added, removed) { Match = filter, NeedsName = needName };
            _custom.Add(w);
            return Add(w);
        }

        /// <summary>Stops the walk below nodes matching filter (their own node is still visited).</summary>
        public static void PruneBelow(IModule owner, WorldWatch.Filter filter)
        {
            _prunes.Add(new Prune { Owner = owner, Filter = filter });
        }

        /// <summary>Common registration tail: list, install, and a full walk so the new watch sees existing objects.</summary>
        static WorldWatch Add(WorldWatch w)
        {
            _watches.Add(w);
            Install();
            Rescan();
            return w;
        }

        /// <summary>Removes one watch.</summary>
        public static void Remove(WorldWatch w)
        {
            if (w == null || !_watches.Remove(w)) return;
            _prefix.Remove(w); _component.Remove(w); _custom.Remove(w);
            List<WorldWatch> list;
            if (w.Text != null && w.WatchKind == WorldWatch.Kind.Name && _byName.TryGetValue(w.Text, out list)) { list.Remove(w); if (list.Count == 0) _byName.Remove(w.Text); }
            w.Clear();
        }

        /// <summary>Removes every watch and prune filter of owner (module shutdown).</summary>
        public static void RemoveAll(IModule owner)
        {
            for (int i = _watches.Count - 1; i >= 0; i--) if (_watches[i].Owner == owner) Remove(_watches[i]);
            for (int i = _prunes.Count - 1; i >= 0; i--) if (_prunes[i].Owner == owner) _prunes.RemoveAt(i);
        }

        /// <summary>Queues root (and its subtree) for a visit, e.g. a freshly spawned object; duplicates in one pass are ignored.</summary>
        public static void QueueRoot(Transform root)
        {
            if (root == null || _watches.Count == 0) return;
            if (_queued.Add(root.GetInstanceID())) _walk.Push(root);
        }

        /// <summary>Re-walks every loaded scene (already found objects are not reported again). Roots are enumerated on the next step.</summary>
        public static void Rescan() { if (_watches.Count > 0) _rootsPending = true; }

        /// <summary>Subscribes scene load and tile Applied and registers the sliced job once.</summary>
        static void Install()
        {
            if (_installed) return;
            _installed = true;
            SceneManager.sceneLoaded += SceneLoaded;
            WorldTiles.OnApplied(null, TileApplied);
            _job = Scheduler.Sliced(null, "Core.WorldIndex", BudgetMicros, Step);
        }

        /// <summary>Tile applied: walk the new tile's objects.</summary>
        static void TileApplied(TerrainTile tile, TileData data, StopToken stop)
        {
            if (tile != null) QueueRoot(tile.transform);
        }

        /// <summary>Scene load: forget everything found and walk the loaded scenes again.</summary>
        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _walk.Clear(); _queued.Clear();
            for (int i = 0; i < _watches.Count; i++) _watches[i].Clear();
            Rescan();
        }

        /// <summary>Queues the roots of every loaded scene.</summary>
        static void QueueScenes()
        {
            _rootsPending = false;
            _queued.Clear();   // a rescan re-queues roots already walked in this pass (new watches must see them)
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i); if (!scene.isLoaded) continue;
                _roots.Clear(); scene.GetRootGameObjects(_roots);
                for (int r = 0; r < _roots.Count; r++) if (_roots[r] != null) QueueRoot(_roots[r].transform);
            }
            _roots.Clear();
        }

        /// <summary>Sliced step: sweeps a few found entries for destroyed objects, then visits one node. False when idle.</summary>
        static bool Step()
        {
            if (_watches.Count == 0) return false;
            if (_job != null) _job.BudgetMicros = BudgetMicros;
            Sweep();
            if (_rootsPending) QueueScenes();
            if (_walk.Count == 0) return false;
            var t = _walk.Pop();
            if (t != null) Visit(t);
            if (_walk.Count == 0) { _queued.Clear(); Revision++; return false; }
            return true;
        }

        /// <summary>Checks up to 4 found entries of one watch (round robin) and reports destroyed ones.</summary>
        static void Sweep()
        {
            if (_sweepWatch >= _watches.Count) _sweepWatch = 0;
            _watches[_sweepWatch++].SweepSome(4);
        }

        /// <summary>Visits one node: matches it against every watch and pushes its children unless pruned.</summary>
        static void Visit(Transform t)
        {
            Visited++;
            bool needName = _byName.Count > 0 || _prefix.Count > 0 || _prunes.Count > 0;
            for (int i = 0; i < _custom.Count && !needName; i++) if (_custom[i].NeedsName) needName = true;
            string name = needName ? t.name : null;
            if (name != null)
            {
                List<WorldWatch> list;
                if (_byName.TryGetValue(name, out list)) for (int i = 0; i < list.Count; i++) list[i].Offer(t, name, null);
                for (int i = 0; i < _prefix.Count; i++) if (name.StartsWith(_prefix[i].Text, StringComparison.Ordinal)) _prefix[i].Offer(t, name, null);
            }
            for (int i = 0; i < _component.Count; i++) _component[i].TryComponent(t, name);
            for (int i = 0; i < _custom.Count; i++)
            {
                bool hit;
                try { hit = _custom[i].Match(t, name); } catch (Exception e) { hit = false; _custom[i].Failed(e); }
                if (hit) _custom[i].Offer(t, name, null);
            }
            for (int i = 0; i < _prunes.Count; i++)
            {
                bool stop;
                try { stop = _prunes[i].Filter(t, name); } catch (Exception) { stop = false; }
                if (stop) return;
            }
            for (int c = 0; c < t.childCount; c++) _walk.Push(t.GetChild(c));
        }

        /// <summary>Component watch: one reused buffer per T, the first component found is reported.</summary>
        private sealed class ComponentWatch<T> : WorldWatch where T : Component
        {
            readonly List<T> _buf = new List<T>(2);
            /// <summary>Creates a component watch.</summary>
            public ComponentWatch(IModule owner, Visitor added, Action<int> removed) : base(owner, Kind.Component, typeof(T).Name, added, removed) { }
            /// <summary>GetComponents into the reused buffer; offers the node with its first T.</summary>
            internal override void TryComponent(Transform t, string name)
            {
                _buf.Clear(); t.GetComponents(_buf);
                if (_buf.Count > 0) { var c = _buf[0]; _buf.Clear(); if (c != null) Offer(t, name, c); }
            }
        }
    }

    /// <summary>
    /// One <see cref="WorldIndex"/> registration: what it matches, the objects found so far (instance ids kept so each object
    /// is reported once per scene), and the Added/Removed callbacks (each in its own try/catch).
    /// </summary>
    public class WorldWatch
    {
        /// <summary>Called for every new match: the node, its name (null if no name-based watch needed it) and the matched component (component watches).</summary>
        public delegate void Visitor(Transform t, string name, Component component);
        /// <summary>Custom match / prune filter.</summary>
        public delegate bool Filter(Transform t, string name);
        /// <summary>How a watch matches.</summary>
        public enum Kind { Name, Prefix, Component, Custom }

        /// <summary>Owning module (null = Core).</summary>
        public readonly IModule Owner;
        /// <summary>Match kind.</summary>
        public readonly Kind WatchKind;
        /// <summary>Name / prefix / component type name.</summary>
        public readonly string Text;
        /// <summary>Live matches (destroyed ones are removed by the sweep; may briefly contain destroyed objects).</summary>
        public readonly List<Transform> Found = new List<Transform>();
        internal Filter Match;
        internal bool NeedsName;
        readonly Visitor _added;
        readonly Action<int> _removed;
        readonly HashSet<int> _ids = new HashSet<int>();
        readonly List<int> _foundIds = new List<int>();
        int _sweep, _errors;

        /// <summary>Creates a watch (use the WorldIndex.Watch* factories).</summary>
        internal WorldWatch(IModule owner, Kind kind, string text, Visitor added, Action<int> removed)
        { Owner = owner; WatchKind = kind; Text = text; _added = added; _removed = removed; }

        /// <summary>Overridden by component watches; plain watches never match on components.</summary>
        internal virtual void TryComponent(Transform t, string name) { }

        /// <summary>Records t and calls Added unless it was already found in this scene.</summary>
        internal void Offer(Transform t, string name, Component c)
        {
            if (!_ids.Add(t.GetInstanceID())) return;
            Found.Add(t); _foundIds.Add(t.GetInstanceID());
            if (_added == null) return;
            try { _added(t, name, c); } catch (Exception e) { Failed(e); }
        }

        /// <summary>Checks up to n found entries; destroyed ones are dropped and reported through Removed(instanceId).</summary>
        internal void SweepSome(int n)
        {
            for (int k = 0; k < n && Found.Count > 0; k++)
            {
                if (_sweep >= Found.Count) _sweep = 0;
                if (Found[_sweep] != null) { _sweep++; continue; }
                int id = _foundIds[_sweep], last = Found.Count - 1;
                Found[_sweep] = Found[last]; _foundIds[_sweep] = _foundIds[last];
                Found.RemoveAt(last); _foundIds.RemoveAt(last); _ids.Remove(id);
                if (_removed != null) { try { _removed(id); } catch (Exception e) { Failed(e); } }
            }
        }

        /// <summary>Forgets every found object (scene load / removal); no Removed calls.</summary>
        internal void Clear() { Found.Clear(); _foundIds.Clear(); _ids.Clear(); _sweep = 0; }

        /// <summary>Logs a callback failure (first 3 per watch, then silent).</summary>
        internal void Failed(Exception e)
        {
            if (++_errors > 3) return;
            Log.Warn(null,"[" + (Owner != null ? Owner.Id : "Core") + "] WorldIndex " + WatchKind + " " + Text + " callback failed: " + e.Message);
        }
    }
}
