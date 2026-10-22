using System;
using System.Collections.Generic;
using ES3Internal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Core
{
    /// <summary>
    /// One shared index of asset-root prefabs (GameObjects not in a scene, without a parent) and ES3 reference-manager prefabs,
    /// replacing the Patrols Prefabs name index, Towns PoiPropCatalog/PoiSpawnCatalog asset scans, Quests FindPrefab /
    /// Takeover.ScanForPrefab and Patrols PickupCatalog scans (each did its own Resources.FindObjectsOfTypeAll).
    /// Name rules = Patrols Prefabs.ScanExact: first asset root per exact name, then every ES3 manager prefab overrides its
    /// name; a case-insensitive fold prefers ES3-backed roots. Also kept: every asset-side PlayMakerFSM
    /// (<see cref="AssetFsms"/>, PoiSpawnCatalog/Patrols Scan input) and ES3 prefab ids (<see cref="ById"/>).
    /// Warming: started after a scene load (never in Plugin.Awake) by <see cref="Warm"/> or the first query; the two
    /// FindObjectsOfTypeAll calls happen once per generation, the indexing runs in a Core sliced job ("Core.PrefabCatalog",
    /// 500 us/frame). A query before the index is complete finishes it synchronously (Patrols behaviour: misses never rescan).
    /// Invalidated on scene load and when an ES3 manager's prefab count changes (checked by <see cref="Refresh"/>).
    /// </summary>
    public static class PrefabCatalog
    {
        static readonly Dictionary<string, GameObject> _exact = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        static readonly Dictionary<string, GameObject> _folded = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<long, GameObject> _byId = new Dictionary<long, GameObject>();
        static readonly List<PlayMakerFSM> _fsms = new List<PlayMakerFSM>();
        static GameObject[] _scanGo;
        static PlayMakerFSM[] _scanFsm;
        static ES3ReferenceMgrBase[] _managers;
        static int[] _managerCounts;
        static int _goIndex, _fsmIndex;
        static bool _ready, _building, _installed;

        /// <summary>Incremented on every invalidation (consumers re-resolve cached prefab references when it changes).</summary>
        public static int Generation;
        /// <summary>True when the index of the current generation is complete.</summary>
        public static bool Ready { get { return _ready; } }
        /// <summary>Indexed names (Debug census).</summary>
        public static int Count { get { return _exact.Count; } }

        /// <summary>Drops the index (next query or Warm rebuilds it).</summary>
        public static void Invalidate()
        {
            _exact.Clear(); _folded.Clear(); _byId.Clear(); _fsms.Clear();
            _scanGo = null; _scanFsm = null; _managers = null; _managerCounts = null;
            _goIndex = _fsmIndex = 0; _ready = _building = false;
            Generation++;
        }

        /// <summary>Starts a sliced build of the index if none is ready or running (call at world ready).</summary>
        public static void Warm()
        {
            Install();
            if (_ready || _building) return;
            Begin();
        }

        /// <summary>Invalidates when any ES3 manager's prefab list changed size (mods appending to ES3's public list); then warms.</summary>
        public static void Refresh()
        {
            if (_managers != null)
                for (int i = 0; i < _managers.Length; i++)
                    if ((_managers[i] != null && _managers[i].prefabs != null ? _managers[i].prefabs.Count : -1) != _managerCounts[i]) { Invalidate(); break; }
            Warm();
        }

        /// <summary>Exact (ordinal) name, then the case-insensitive fold; null when unknown.</summary>
        public static GameObject Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Complete();
            GameObject hit;
            if (_exact.TryGetValue(name, out hit) && hit != null) return hit;
            return _folded.TryGetValue(name, out hit) ? hit : null;
        }

        /// <summary>Exact (ordinal) name only.</summary>
        public static GameObject Exact(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Complete();
            GameObject hit;
            return _exact.TryGetValue(name, out hit) ? hit : null;
        }

        /// <summary>The prefab registered in an ES3 reference manager under prefabId, or null.</summary>
        public static GameObject ById(long prefabId)
        {
            Complete();
            GameObject hit;
            return _byId.TryGetValue(prefabId, out hit) ? hit : null;
        }

        /// <summary>An item prefab by name: asset root (Find rules) carrying an "ItemName" FSM (Quests FindPrefab); null otherwise.</summary>
        public static GameObject Item(string name)
        {
            var go = Find(name);
            return go != null && FsmUtil.Find(go, "ItemName") != null ? go : null;
        }

        /// <summary>Every PlayMakerFSM that lives on an asset (not in a scene). Shared list: do not modify.</summary>
        public static List<PlayMakerFSM> AssetFsms { get { Complete(); return _fsms; } }

        /// <summary>Copies every indexed asset root (distinct exact names) into into (editor/catalog use; allocates nothing itself).</summary>
        public static void AllRoots(List<GameObject> into)
        {
            Complete();
            foreach (var pair in _exact) if (pair.Value != null) into.Add(pair.Value);
        }

        /// <summary>Subscribes the scene-load invalidation and registers the sliced job once.</summary>
        static void Install()
        {
            if (_installed) return;
            _installed = true;
            SceneManager.sceneLoaded += SceneLoaded;
            Scheduler.Sliced(null, "Core.PrefabCatalog", 500, Step);
        }

        /// <summary>Scene load: ES3 managers are recreated, so the index starts over (lazily).</summary>
        static void SceneLoaded(Scene scene, LoadSceneMode mode) { Invalidate(); }

        /// <summary>Takes the two global snapshots (the only FindObjectsOfTypeAll calls) and the ES3 manager prefab lists.</summary>
        static void Begin()
        {
            Install();
            _building = true;
            _scanGo = Resources.FindObjectsOfTypeAll<GameObject>();
            _scanFsm = Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
            _goIndex = _fsmIndex = 0;
        }

        /// <summary>Finishes the index synchronously (first query before the sliced build completed).</summary>
        static void Complete()
        {
            if (_ready) return;
            if (!_building) Begin();
            while (Step()) { }
        }

        /// <summary>Sliced step: indexes 256 GameObjects or FSMs; on the end applies the ES3 overrides and the fold. False when idle.</summary>
        static bool Step()
        {
            if (!_building) return false;
            if (_goIndex < _scanGo.Length)
            {
                for (int end = Math.Min(_scanGo.Length, _goIndex + 256); _goIndex < end; _goIndex++)
                {
                    var go = _scanGo[_goIndex];
                    if (go != null && !go.scene.IsValid() && go.transform.parent == null && !_exact.ContainsKey(go.name)) _exact[go.name] = go;
                }
                return true;
            }
            if (_fsmIndex < _scanFsm.Length)
            {
                for (int end = Math.Min(_scanFsm.Length, _fsmIndex + 256); _fsmIndex < end; _fsmIndex++)
                {
                    var f = _scanFsm[_fsmIndex];
                    if (f != null && !f.gameObject.scene.IsValid()) _fsms.Add(f);
                }
                return true;
            }
            _managers = Resources.FindObjectsOfTypeAll<ES3ReferenceMgrBase>();
            _managerCounts = new int[_managers.Length];
            for (int i = 0; i < _managers.Length; i++)
            {
                var manager = _managers[i];
                _managerCounts[i] = manager != null && manager.prefabs != null ? manager.prefabs.Count : -1;
                if (_managerCounts[i] < 0) continue;
                foreach (var prefab in manager.prefabs)
                {
                    if (prefab == null) continue;
                    _exact[prefab.name] = prefab.gameObject;
                    if (!_byId.ContainsKey(prefab.prefabId)) _byId[prefab.prefabId] = prefab.gameObject;
                }
            }
            foreach (var pair in _exact)
            {
                GameObject previous;
                if (!_folded.TryGetValue(pair.Key, out previous) || previous == null
                    || (pair.Value.GetComponent<ES3Prefab>() != null && previous.GetComponent<ES3Prefab>() == null)) _folded[pair.Key] = pair.Value;
            }
            _scanGo = null; _scanFsm = null;
            _building = false; _ready = true;
            if (Log.Verbose) Log.Info("[Core] Prefab catalog: " + _exact.Count + " names, " + _fsms.Count + " asset FSMs (generation " + Generation + ")");
            return false;
        }
    }
}
