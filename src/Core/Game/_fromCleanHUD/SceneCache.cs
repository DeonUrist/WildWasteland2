using System;
using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.CleanHUD
{
    // One bounded scene walk supplies HUD bindings and the controls catalog. No global resource scans during play.
    internal static class SceneCache
    {
        private static readonly Dictionary<string, Transform> Roots = new Dictionary<string, Transform>();
        private static readonly Dictionary<string, Transform> Paths = new Dictionary<string, Transform>();
        private static readonly Dictionary<string, float> Missing = new Dictionary<string, float>();
        private static readonly Dictionary<string, PlayMakerFSM> FsMs = new Dictionary<string, PlayMakerFSM>();
        private static readonly List<GameObject> RootBuffer = new List<GameObject>();
        private static readonly List<PlayMakerFSM> FsmBuffer = new List<PlayMakerFSM>();
        private static readonly Stack<Transform> Walk = new Stack<Transform>();
        private static readonly HashSet<Transform> QueuedRoots = new HashSet<Transform>();
        private static IEnumerator _scan;
        internal static bool KeysReady;

        internal static void Clear()
        {
            var disposable = _scan as IDisposable; if (disposable != null) disposable.Dispose();
            _scan = null; Walk.Clear(); QueuedRoots.Clear(); Roots.Clear(); Paths.Clear(); Missing.Clear(); FsMs.Clear(); RootBuffer.Clear(); FsmBuffer.Clear(); KeysReady = false;
        }
        internal static void Reset()
        {
            Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i); if (!scene.isLoaded) continue;
                RootBuffer.Clear(); scene.GetRootGameObjects(RootBuffer);
                foreach (var go in RootBuffer) { Roots[go.name] = go.transform; Walk.Push(go.transform); }
            }
            _scan = Discover(false);
            KeyCatalog.SceneReset();
        }
        internal static void BeginKeys()
        {
            var disposable = _scan as IDisposable; if (disposable != null) disposable.Dispose();
            Walk.Clear(); foreach (var root in Roots.Values) if (root != null) Walk.Push(root);
            KeyCatalog.BeginFsms(); KeysReady = false; _scan = Discover(true);
        }
        internal static bool Step()
        {
            if (_scan == null) return false;
            if (_scan.MoveNext()) return true;
            var disposable = _scan as IDisposable; if (disposable != null) disposable.Dispose();
            _scan = null; QueuedRoots.Clear(); return false;
        }
        internal static void QueueRoot(Transform root)
        {
            if (root == null || !QueuedRoots.Add(root)) return;
            Walk.Push(root);
            if (_scan == null) _scan = Discover(false);
        }
        private static IEnumerator Discover(bool keys)
        {
            while (Walk.Count > 0)
            {
                var t = Walk.Pop(); if (t == null) continue;
                string n = t.name;   // one name read per node
                if (n == "mapIcon") LocationMarkers.Observe(t);
                for (int i = 0; i < t.childCount; i++) Walk.Push(t.GetChild(i));
                bool named = n == "Weapons" || n == "Bandage_Item" || n == "FirstAid_Item" || n == "PlayerCamera" || n == "__GameManager__";
                // Tile walks (keys == false) only need the FSMs of the five named objects.
                if (!keys && !named) { yield return null; continue; }
                FsmBuffer.Clear(); t.GetComponents<PlayMakerFSM>(FsmBuffer);
                for (int i = 0; i < FsmBuffer.Count; i++)
                {
                    var f = FsmBuffer[i]; if (f == null) continue;
                    if (named) FsMs[n + "\n" + f.FsmName] = f;
                    if (!keys || f.Fsm == null || !f.Fsm.Initialized || f.FsmStates == null) continue;
                    foreach (var state in f.FsmStates)
                    {
                        KeyCatalog.ObserveState(f, state);
                        yield return null;
                    }
                }
                yield return null;
            }
            if (keys) { KeyCatalog.FinishFsms(); KeysReady = true; }
        }
        internal static PlayMakerFSM Fsm(string name, string fsm)
        {
            PlayMakerFSM value;
            return FsMs.TryGetValue(name + "\n" + fsm, out value) ? value : null;
        }
        internal static Transform FindPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Transform found;
            if (Paths.TryGetValue(path, out found) && found != null) return found;
            float retry;
            if (Missing.TryGetValue(path, out retry) && Time.unscaledTime < retry) return null;
            int split = path.IndexOf('/');
            string root = split < 0 ? path : path.Substring(0, split);
            if (Roots.TryGetValue(root, out found) && found != null)
            {
                found = split < 0 ? found : found.Find(path.Substring(split + 1));
                if (found != null) { Paths[path] = found; Missing.Remove(path); return found; }
            }
            Missing[path] = Time.unscaledTime + 1f;
            return null;
        }
    }
}
