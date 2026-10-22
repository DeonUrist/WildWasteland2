using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ES3Internal;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // =============================================================== vanilla spawn recipe
    // The counter FSM and the registry lists are looked up once and kept (GameObject.Find is a scene-wide name search; a loot
    // truck used to run it twice per item, sixty-odd times in one frame). Re-found when the objects are gone (scene change).
    internal static class Register
    {
        private static PlayMakerFSM _counterFsm;
        private static FsmInt _counter;
        private static GameObject _reg;
        private static PlayMakerArrayListProxy _cars, _items;

        internal static void Invalidate() { _counterFsm = null; _counter = null; _reg = null; _cars = null; _items = null; _pending.Clear(); }

        // cars being built: what Add() would register for them (the car, its parts, its cargo) waits here until Commit
        private static readonly Dictionary<GameObject, List<KeyValuePair<GameObject, bool>>> _pending = new Dictionary<GameObject, List<KeyValuePair<GameObject, bool>>>();

        internal static void Defer(GameObject car) { if (car != null && !_pending.ContainsKey(car)) _pending[car] = new List<KeyValuePair<GameObject, bool>>(); }
        internal static bool Pending(GameObject car) { return !ReferenceEquals(car, null) && _pending.Count > 0 && _pending.ContainsKey(car); }
        internal static void Discard(GameObject car) { if (!ReferenceEquals(car, null)) _pending.Remove(car); }

        // registers everything deferred for this car (objects destroyed meanwhile are skipped); returns the count
        internal static int Commit(GameObject car)
        {
            List<KeyValuePair<GameObject, bool>> list;
            if (ReferenceEquals(car, null) || !_pending.TryGetValue(car, out list)) return 0;
            _pending.Remove(car);
            int n = 0;
            foreach (var kv in list) if (kv.Key != null) { AddNow(kv.Key, kv.Value); n++; }
            return n;
        }

        private static GameObject PendingOwner(GameObject go)
        {
            if (_pending.Count == 0 || go == null) return null;
            for (var t = go.transform; t != null; t = t.parent) if (_pending.ContainsKey(t.gameObject)) return t.gameObject;
            return null;
        }

        private static FsmInt Counter()
        {
            if (_counterFsm != null && _counterFsm.gameObject != null && _counter != null) return _counter;
            _counterFsm = null; _counter = null;
            var counterGo = Core.GameRefs.ItemNameId;
            if (counterGo == null) return null;
            foreach (var f in counterGo.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "itemNameID") { _counterFsm = f; _counter = f.FsmVariables.GetFsmInt("intName"); break; }
            return _counter;
        }

        private static GameObject Registry()
        {
            if (_reg != null) return _reg;
            _cars = _items = null;
            _reg = Core.GameRefs.NewGOArrayList;
            if (_reg == null) return null;
            foreach (var p in _reg.GetComponents<PlayMakerArrayListProxy>())
            {
                string n = (p.referenceName ?? "").ToLowerInvariant();
                if (_cars == null && n.Contains("car")) _cars = p;
                if (_items == null && n.Contains("item")) _items = p;
            }
            return _reg;
        }

        internal static void Name(GameObject go, string prefab)
        {
            int id = -1;
            var v = Counter();
            if (v != null) { v.Value += 1; id = v.Value; }
            go.name = prefab + "(Clone)" + (id >= 0 ? id.ToString() : "");
        }

        // Takes a car and everything under it (parts, cargo) out of the game's NewGO lists before it is destroyed, so the
        // save does not keep references to it.
        // Perf: reused buffers, a reference-identity set and one compaction pass per list (was RemoveAt per hit, new set/arrays per call)
        private sealed class RefEq : IEqualityComparer<GameObject>
        {
            public bool Equals(GameObject a, GameObject b) { return ReferenceEquals(a, b); }
            public int GetHashCode(GameObject o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }
        private static readonly HashSet<GameObject> _treeSet = new HashSet<GameObject>(new RefEq());
        private static readonly List<Transform> _treeNodes = new List<Transform>();
        private static readonly List<PlayMakerArrayListProxy> _proxies = new List<PlayMakerArrayListProxy>();
        internal static void RemoveTree(GameObject root)
        {
            var reg = Registry();
            if (reg == null || root == null) return;
            _treeSet.Clear(); _treeNodes.Clear();
            root.GetComponentsInChildren(true, _treeNodes);
            foreach (var t in _treeNodes) _treeSet.Add(t.gameObject);
            _treeNodes.Clear();
            _proxies.Clear(); reg.GetComponents(_proxies);
            foreach (var p in _proxies)
            {
                var list = p.arrayList;
                if (list == null) continue;
                int w = 0, n = list.Count;
                for (int i = 0; i < n; i++)
                {
                    var item = list[i]; var go = item as GameObject;
                    // the set holds live objects only, so a destroyed entry never matches (as with the old != null test)
                    if (!ReferenceEquals(go, null) && _treeSet.Contains(go)) continue;
                    if (w != i) list[w] = item; w++;
                }
                if (w < n) list.RemoveRange(w, n - w);
            }
            _proxies.Clear(); _treeSet.Clear();
        }

        // owner = the car the object belongs to when it is not parented under it yet (cargo before it is locked in)
        internal static void Add(GameObject go, bool isVehicle, GameObject owner = null)
        {
            var key = owner != null && _pending.ContainsKey(owner) ? owner : PendingOwner(go);
            if (key != null) { _pending[key].Add(new KeyValuePair<GameObject, bool>(go, isVehicle)); return; }
            AddNow(go, isVehicle);
        }

        private static void AddNow(GameObject go, bool isVehicle)
        {
            if (Registry() == null) return;
            var list = isVehicle ? _cars : _items;
            if (list != null && list.arrayList != null) list.arrayList.Add(go);
            else Core.Log.Warn("[Patrols] " + "NewGO_ArrayList has no '" + (isVehicle ? "car" : "item") + "' list");
        }
    }
}
