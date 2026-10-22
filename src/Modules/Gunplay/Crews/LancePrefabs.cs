using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>
    /// Blast-lance prefab lookup for the rider lance (item prop and thrown projectile) over the shared Core asset index, with the same rule
    /// the Patrols Prefabs.FindAny used for these names: asset roots carrying a PlayMaker FSM, exact name (case-insensitive, first root
    /// wins) then the first root whose name contains the query. Rebuilt when <see cref="PrefabCatalog.Generation"/> changes.
    /// </summary>
    internal static class LancePrefabs
    {
        static readonly List<GameObject> _roots = new List<GameObject>();
        static readonly Dictionary<string, GameObject> _byName = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        static int _generation = -1;

        /// <summary>Asset root with an FSM named (or, failing that, containing) <paramref name="query"/>; null when none.</summary>
        internal static GameObject FindAny(string query)
        {
            if (string.IsNullOrEmpty(query)) return null;
            string q = query.Trim();
            Ensure();
            GameObject go;
            if (_byName.TryGetValue(q, out go)) return go;
            foreach (var r in _roots)
                if (r != null && r.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (Log.L2) GunplayLog.Deep("Lance prefab \"" + q + "\" -> " + r.name + " (partial match)");
                    return r;
                }
            return null;
        }

        /// <summary>Indexes the FSM-carrying asset roots of the current Core prefab generation (once per generation).</summary>
        static void Ensure()
        {
            var fsms = PrefabCatalog.AssetFsms;
            if (_generation == PrefabCatalog.Generation && _roots.Count > 0) return;
            _generation = PrefabCatalog.Generation;
            _roots.Clear(); _byName.Clear();
            var seen = new HashSet<GameObject>();
            foreach (var f in fsms)
            {
                if (f == null || f.gameObject == null) continue;
                var go = f.gameObject;
                if (go.scene.IsValid() || go.transform.parent != null) continue;   // assets only, roots only
                if (!seen.Add(go)) continue;
                _roots.Add(go);
                if (!_byName.ContainsKey(go.name)) _byName[go.name] = go;
            }
        }
    }
}
