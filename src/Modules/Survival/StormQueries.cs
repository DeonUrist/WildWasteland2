using ES3Internal;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Survival
{
    /// <summary>Storm identity and exposure shared with navigation and senses without consulting native visual enable flags.</summary>
    internal sealed class StormQueries : IStormQueries
    {
        internal const long Prefab = 5697600806714206537L;
        readonly Dictionary<int, GameObject> _roots = new Dictionary<int, GameObject>();
        readonly List<int> _dead = new List<int>();
        readonly WeatherRuntime _weather;
        /// <summary>Observes native anchors through Core's shared world discovery.</summary>
        internal StormQueries(SurvivalModule owner, WeatherRuntime weather)
        { _weather = weather; WorldIndex.WatchComponent<ES3Prefab>(owner, Found); Scheduler.Interval(owner, "Survival.StormQueries.Prune", 1f, Prune); }
        /// <summary>Retains exact native storm prefab instances only.</summary>
        void Found(Transform node, string name, Component component)
        { var prefab = component as ES3Prefab; if (prefab != null && prefab.prefabId == Prefab) _roots[node.GetInstanceID()] = node.gameObject; }
        /// <summary>Counts live native and manual preview anchors once.</summary>
        public int ActiveStorms { get { int count = 0; foreach (var pair in _roots) if (Live(pair.Value) && !_weather.Presents(pair.Value)) count++; return count + _weather.Count; } }
        /// <summary>Checks native prefab identity on a root or ancestor, independent of presentation state.</summary>
        public bool IsStormRoot(GameObject go)
        {
            for (var node = go != null ? go.transform : null; node != null; node = node.parent)
            { var prefab = node.GetComponent<ES3Prefab>(); if (prefab != null && prefab.prefabId == Prefab) return true; }
            return false;
        }
        /// <summary>Tests horizontal distance against retained active anchors.</summary>
        public bool AnyNear(Vector3 world, float radius)
        {
            float square = radius * radius;
            for (int i = 0; i < _weather.Count; i++) { WeatherRegion region; if (!_weather.TryGet(i, out region)) continue; var d = region.WorldCenter - world; d.y = 0; if (d.sqrMagnitude <= square) return true; }
            foreach (var pair in _roots) if (Live(pair.Value)) { var d = Origin.World(pair.Value.transform.position) - world; d.y = 0; if (d.sqrMagnitude <= square) return true; }
            return false;
        }
        /// <summary>Uses the rendered rounded front footprint, with the native radius only for unreplaced encounters.</summary>
        public float StormExposure(Vector3 world)
        {
            float result = 0;
            for (int i = 0; i < _weather.Count; i++)
            {
                WeatherRegion region; if (!_weather.TryGet(i, out region)) continue;
                var local = Quaternion.Inverse(Quaternion.Euler(0, region.Heading, 0)) * (world - region.WorldCenter);
                result = Mathf.Max(result, Mathf.Clamp01(WeatherRegion.EdgeDistance(local.x / region.Radii.x, local.z / region.Radii.y) * 5));
            }
            foreach (var pair in _roots)
            {
                var root = pair.Value; if (!Live(root) || _weather.Presents(root)) continue;
                var d = Origin.World(root.transform.position) - world; d.y = 0;
                if (d.sqrMagnitude <= 2500f * 2500f) result = 1;
            }
            return result;
        }
        /// <summary>Native disabled storm anchors do not contribute exposure.</summary>
        static bool Live(GameObject root) { return root != null && root.activeInHierarchy; }
        /// <summary>Forgets the unloaded world's native anchors.</summary>
        internal void Clear() { _roots.Clear(); _dead.Clear(); }
        /// <summary>Releases Unity-destroyed storm anchors without a scene scan.</summary>
        void Prune() { _dead.Clear(); foreach (var pair in _roots) if (pair.Value == null) _dead.Add(pair.Key); foreach (int id in _dead) _roots.Remove(id); _dead.Clear(); }
    }
}
