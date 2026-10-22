using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Shared native scene-root/path lookups populated by the single bounded WorldIndex walker.</summary>
    public static class SceneLookup
    {
        private static readonly Dictionary<string, Transform> Roots = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Transform> Paths = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> Missing = new Dictionary<string, float>(StringComparer.Ordinal);
        private static WorldWatch _roots;
        /// <summary>Installs the scene-root observer lazily without enumerating scene objects during Plugin.Awake.</summary>
        public static void Ensure()
        {
            if (_roots == null) _roots = WorldIndex.Watch(null, IsRoot, FoundRoot);
        }
        /// <summary>Matches scene roots only; name is supplied by the shared walker.</summary>
        private static bool IsRoot(Transform transform, string name) { return transform.parent == null; }
        /// <summary>Caches a root observed by the world index and reopens failed path lookups.</summary>
        private static void FoundRoot(Transform transform, string name, Component component) { Roots[name] = transform; Missing.Clear(); }
        /// <summary>Resolves root/child paths with one-second negative caching and no global scene scan.</summary>
        public static Transform FindPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Ensure(); Transform found;
            if (Paths.TryGetValue(path, out found) && found != null) return found;
            float retry;
            if (Missing.TryGetValue(path, out retry) && Time.unscaledTime < retry) return null;
            int split = path.IndexOf('/'); string root = split < 0 ? path : path.Substring(0, split);
            if (Roots.TryGetValue(root, out found) && found != null)
            {
                found = split < 0 ? found : found.Find(path.Substring(split + 1));
                if (found != null) { Paths[path] = found; Missing.Remove(path); return found; }
            }
            Missing[path] = Time.unscaledTime + 1f; return null;
        }
        /// <summary>Forgets cached native references at a scene transition; WorldIndex requeues the new scene roots.</summary>
        internal static void Reset() { Roots.Clear(); Paths.Clear(); Missing.Clear(); }
    }
}
