using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// Maps GameObjects to <see cref="EntityId"/>s and back (core-modapi.md 3). First use of an object assigns its ES3 reference id
    /// when the object is known to the scene's ES3 reference manager, otherwise a runtime id. Both maps are cleared on scene load;
    /// destroyed objects are dropped lazily (on lookup and in a periodic sweep during assignment). Main thread only.
    /// </summary>
    public static class Entities
    {
        static readonly Dictionary<int, EntityId> _byInstance = new Dictionary<int, EntityId>();
        static readonly Dictionary<long, GameObject> _byId = new Dictionary<long, GameObject>();
        static readonly List<int> _dead = new List<int>();
        static readonly HashSet<long> _gone = new HashSet<long>();
        static long _next = 1;
        static int _sinceSweep;
        const int SweepEvery = 512;

        /// <summary>The local player entity (constant id <see cref="EntityId.PlayerValue"/>).</summary>
        public static EntityId Player { get { return new EntityId(EntityId.PlayerValue); } }

        /// <summary>Id of <paramref name="go"/>, assigning one on first use; <see cref="EntityId.None"/> for null.
        /// The player object (GameRefs.Player) always maps to <see cref="Player"/>.</summary>
        public static EntityId Of(GameObject go)
        {
            if (go == null) return EntityId.None;
            if (ReferenceEquals(go, GameRefs.Player)) return Player;
            int key = go.GetInstanceID();
            EntityId id;
            if (_byInstance.TryGetValue(key, out id)) return id;
            id = new EntityId(SavedId(go));
            if (!id.IsValid || _byId.ContainsKey(id.Value)) id = new EntityId(EntityId.RuntimeBit | _next++);
            Store(key, go, id);
            return id;
        }

        /// <summary>Id of the GameObject of <paramref name="c"/> (convenience for components).</summary>
        public static EntityId Of(Component c) { return c != null ? Of(c.gameObject) : EntityId.None; }

        /// <summary>Id already assigned to <paramref name="go"/> without assigning one; false when it has none yet.</summary>
        public static bool TryPeek(GameObject go, out EntityId id)
        {
            id = EntityId.None;
            if (go == null) return false;
            if (ReferenceEquals(go, GameRefs.Player)) { id = Player; return true; }
            return _byInstance.TryGetValue(go.GetInstanceID(), out id);
        }

        /// <summary>Live object for an id, or null (unknown id or destroyed object).</summary>
        public static GameObject Find(EntityId id)
        {
            if (!id.IsValid) return null;
            if (id.Value == EntityId.PlayerValue) return GameRefs.Player;
            GameObject go;
            if (!_byId.TryGetValue(id.Value, out go)) return null;
            if (go == null) { _byId.Remove(id.Value); return null; }
            return go;
        }

        /// <summary>Registers a known id for <paramref name="go"/> (load or replication); replaces any id it had.</summary>
        public static void Bind(GameObject go, EntityId id)
        {
            if (go == null || !id.IsValid || id.Value == EntityId.PlayerValue) return;
            int key = go.GetInstanceID();
            EntityId old;
            if (_byInstance.TryGetValue(key, out old)) _byId.Remove(old.Value);
            Store(key, go, id);
            if (id.IsRuntime && (id.Value & ~EntityId.RuntimeBit) >= _next) _next = (id.Value & ~EntityId.RuntimeBit) + 1;
        }

        /// <summary>Drops the mapping of <paramref name="go"/> (call when a module destroys an object it published events about).</summary>
        public static void Forget(GameObject go)
        {
            if (go == null) return;
            int key = go.GetInstanceID();
            EntityId id;
            if (_byInstance.TryGetValue(key, out id)) { _byInstance.Remove(key); _byId.Remove(id.Value); }
        }

        /// <summary>Seeds the runtime counter (a multiplayer host hands its counter to joining peers). Ignored when lower than the current one.</summary>
        public static void SeedRuntimeCounter(long next) { if (next > _next) _next = next; }

        /// <summary>Number of tracked objects (Debug census).</summary>
        public static int Count { get { return _byId.Count; } }

        /// <summary>Clears both maps; called by Core on every scene load (instance ids are not stable across scenes).</summary>
        internal static void OnSceneLoaded() { _byInstance.Clear(); _byId.Clear(); _sinceSweep = 0; }

        /// <summary>Stores both directions and sweeps destroyed objects every <see cref="SweepEvery"/> assignments.</summary>
        static void Store(int key, GameObject go, EntityId id)
        {
            _byInstance[key] = id; _byId[id.Value] = go;
            if (++_sinceSweep < SweepEvery) return;
            _sinceSweep = 0;
            foreach (var kv in _byId) if (kv.Value == null) _gone.Add(kv.Key);
            if (_gone.Count == 0) return;
            foreach (var kv in _byInstance) if (_gone.Contains(kv.Value.Value)) _dead.Add(kv.Key);
            foreach (long gone in _gone) _byId.Remove(gone);
            for (int i = 0; i < _dead.Count; i++) _byInstance.Remove(_dead[i]);
            _gone.Clear(); _dead.Clear();
        }

        /// <summary>ES3 reference id of <paramref name="go"/> when the scene's reference manager knows it, else 0.</summary>
        static long SavedId(GameObject go)
        {
            try
            {
                var mgr = ES3Internal.ES3ReferenceMgrBase.Current;
                if (mgr == null) return 0L;
                long id = mgr.Get(go);
                return id > 0L && id != EntityId.PlayerValue && (id & EntityId.RuntimeBit) == 0L ? id : 0L;
            }
            catch (Exception) { return 0L; }
        }
    }
}
