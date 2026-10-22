using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Defers native save-list admission until a composite spawn is ready and removes destroyed spawn trees.</summary>
    public static partial class ItemSpawner
    {
        private static readonly Dictionary<GameObject, List<KeyValuePair<GameObject, bool>>> PendingRegistrations = new Dictionary<GameObject, List<KeyValuePair<GameObject, bool>>>();
        private static readonly HashSet<GameObject> RemovalSet = new HashSet<GameObject>(new ObjectReferenceComparer());
        private static readonly List<Transform> RemovalNodes = new List<Transform>();
        private static readonly List<PlayMakerArrayListProxy> RemovalProxies = new List<PlayMakerArrayListProxy>();

        /// <summary>Uses managed identity so destroyed Unity references retain their original transaction keys.</summary>
        private sealed class ObjectReferenceComparer : IEqualityComparer<GameObject>
        {
            /// <summary>Compares the exact managed object references.</summary>
            public bool Equals(GameObject a, GameObject b) { return ReferenceEquals(a, b); }
            /// <summary>Hashes managed identity without invoking Unity's destroyed-object equality.</summary>
            public int GetHashCode(GameObject value) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value); }
        }

        /// <summary>Starts deferred registration of a composite root and any children or explicitly associated objects.</summary>
        public static void Defer(GameObject root)
        {
            if (root != null && !PendingRegistrations.ContainsKey(root)) PendingRegistrations[root] = new List<KeyValuePair<GameObject, bool>>();
        }

        /// <summary>True while the exact root has an uncommitted native save-list transaction.</summary>
        public static bool Pending(GameObject root) { return !ReferenceEquals(root, null) && PendingRegistrations.ContainsKey(root); }

        /// <summary>Discards the pending list without publishing objects from a failed composite spawn.</summary>
        public static void Discard(GameObject root) { if (!ReferenceEquals(root, null)) PendingRegistrations.Remove(root); }

        /// <summary>Publishes surviving objects in their original order and returns their admitted count.</summary>
        public static int Commit(GameObject root)
        {
            List<KeyValuePair<GameObject, bool>> entries;
            if (ReferenceEquals(root, null) || !PendingRegistrations.TryGetValue(root, out entries)) return 0;
            PendingRegistrations.Remove(root);
            int count = 0;
            foreach (var entry in entries) if (entry.Key != null) { AddNow(entry.Key, entry.Value); count++; }
            return count;
        }

        /// <summary>Names are assigned separately; adds an existing object now or defers it under the supplied/parent root.</summary>
        public static void AddExisting(GameObject value, bool vehicle, GameObject owner = null)
        {
            var root = owner != null && PendingRegistrations.ContainsKey(owner) ? owner : PendingOwner(value);
            if (root != null) { PendingRegistrations[root].Add(new KeyValuePair<GameObject, bool>(value, vehicle)); return; }
            AddNow(value, vehicle);
        }

        /// <summary>Finds the nearest ancestor with an open composite transaction.</summary>
        private static GameObject PendingOwner(GameObject value)
        {
            if (PendingRegistrations.Count == 0 || value == null) return null;
            for (var t = value.transform; t != null; t = t.parent) if (PendingRegistrations.ContainsKey(t.gameObject)) return t.gameObject;
            return null;
        }

        /// <summary>Adds an already named object to the cached native item/car list, preserving admission order.</summary>
        private static void AddNow(GameObject value, bool vehicle)
        {
            if (GameRefs.NewGOArrayList == null) return;
            var list = List(vehicle ? SaveList.Cars : SaveList.Items);
            if (list != null && list.arrayList != null) list.arrayList.Add(value);
            else Log.Warn(null, "NewGO_ArrayList has no '" + (vehicle ? "car" : "item") + "' list");
        }

        /// <summary>Removes a root and its live descendants from every native saved list with one stable compaction per list.</summary>
        public static void RemoveTree(GameObject root)
        {
            var registry = GameRefs.NewGOArrayList;
            if (registry == null || root == null) return;
            RemovalSet.Clear(); RemovalNodes.Clear();
            root.GetComponentsInChildren(true, RemovalNodes);
            foreach (var node in RemovalNodes) RemovalSet.Add(node.gameObject);
            RemovalNodes.Clear(); RemovalProxies.Clear(); registry.GetComponents(RemovalProxies);
            foreach (var proxy in RemovalProxies)
            {
                var list = proxy.arrayList;
                if (list == null) continue;
                int write = 0, count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    var item = list[i]; var go = item as GameObject;
                    if (!ReferenceEquals(go, null) && RemovalSet.Contains(go)) continue;
                    if (write != i) list[write] = item;
                    write++;
                }
                if (write < count) list.RemoveRange(write, count - write);
            }
            RemovalProxies.Clear(); RemovalSet.Clear();
        }

        /// <summary>Drops scene-bound lookup references and pending transactions when the world is reset.</summary>
        public static void ResetRegistrations()
        {
            _counterGo = _registry = null; _counter = null; _items = _cars = null;
            PendingRegistrations.Clear(); RemovalSet.Clear(); RemovalNodes.Clear(); RemovalProxies.Clear();
        }
    }
}
