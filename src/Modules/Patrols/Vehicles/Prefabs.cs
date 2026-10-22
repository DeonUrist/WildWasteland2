using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrol-specific prefab kind/display matching over the shared Core asset index.</summary>
    internal static class Prefabs
    {
        /// <summary>Derived patrol matching metadata for one shared asset root.</summary>
        private class Entry { public GameObject Go; public string Name; public string Display; public string Kind; }
        private static List<Entry> _all;
        private static List<GameObject> _roots;                                   // every asset root with a PlayMaker FSM (crew, carcasses, loot...)
        private static Dictionary<string, GameObject> _byName;
        private static int _classifiedGeneration = -1;
        /// <summary>The shared prefab generation used to invalidate availability caches.</summary>
        internal static int Generation { get { return Core.PrefabCatalog.Generation; } }

        /// <summary>Invalidates shared asset names and the patrol-specific classification.</summary>
        internal static void Invalidate() { Core.PrefabCatalog.Invalidate(); ClearClassification(); }
        /// <summary>Enables Core registration invalidation without installing module-specific copies of the hooks.</summary>
        internal static void Install(Core.IModule owner) { Core.PrefabCatalog.TrackRegistrations(); }
        /// <summary>Refreshes factory contributions and shares Core's ES3-list size checks and asset scan.</summary>
        internal static void Refresh()
        {
            if (!PatrolsModule.Running) return;
            ModVehicles.Refresh(); Core.PrefabCatalog.Refresh(); EnsureClassification();
        }
        /// <summary>Releases only the derived display/kind index; Core owns the source asset snapshots.</summary>
        private static void ClearClassification() { _all = null; _roots = null; _byName = null; _classifiedGeneration = -1; }
        /// <summary>Rebuilds kind/display rows once for each shared asset generation.</summary>
        private static void EnsureClassification()
        {
            if (_all != null && _classifiedGeneration == Generation) return;
            Scan(); _classifiedGeneration = Generation;
        }
        /// <summary>Resolves an authored exact name or registered vehicle factory without fuzzy substitution.</summary>
        internal static GameObject Exact(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;
            string name = query.Trim();
            if (Core.PrefabCatalog.Ready)
            {
                var exact = Core.PrefabCatalog.Exact(name); if (exact != null) return exact;
            }
            if (MotorcycleIntegration.IsBody(name)) return MotorcycleIntegration.Template();
            var mod = ModVehicles.Template(name);
            return mod != null ? mod : Core.PrefabCatalog.Find(name);
        }

        /// <summary>Resolves vehicle/item names and display labels with the existing kind-first fallback order.</summary>
        internal static GameObject Find(string query, string kind)
        {
            if (string.IsNullOrEmpty(query)) return null;
            if (MotorcycleIntegration.IsBody(query)) return MotorcycleIntegration.Template();
            { var mod = ModVehicles.Template(query); if (mod != null) return mod; }
            EnsureClassification();
            string q = query.Trim();
            Entry e = _all.FirstOrDefault(x => x.Kind == kind && string.Equals(x.Name, q, StringComparison.OrdinalIgnoreCase))
                   ?? _all.FirstOrDefault(x => string.Equals(x.Name, q, StringComparison.OrdinalIgnoreCase))
                   ?? _all.FirstOrDefault(x => x.Kind == kind && string.Equals(x.Display, q, StringComparison.OrdinalIgnoreCase))
                   ?? _all.FirstOrDefault(x => string.Equals(x.Display, q, StringComparison.OrdinalIgnoreCase))
                   ?? _all.FirstOrDefault(x => x.Kind == kind && x.Display != null && x.Display.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                   ?? _all.FirstOrDefault(x => x.Kind == kind && x.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            if (e == null)
            {
                var near = _all.Where(x => x.Kind == kind).Select(x => x.Name + (x.Display != null ? " \"" + x.Display + "\"" : "")).Take(40);
                Core.Log.Warn("[Patrols] " + "No " + kind + " prefab matches \"" + q + "\". Known: " + string.Join(", ", near.ToArray()));
                return null;
            }
            PatrolsModule.Verbose("Prefab \"" + q + "\" -> " + e.Name + (e.Display != null ? " (\"" + e.Display + "\")" : "") + " [" + e.Kind + "]");
            return e.Go;
        }

        // Any asset root prefab by name (carcasses, enemies ... anything with a PlayMaker FSM), exact then contains — from the
        // cached scan. Only scene / prefab registration changes invalidate it; misses never trigger another scan.
        /// <summary>Resolves a root with a PlayMaker FSM by exact or partial name, including registered vehicle factories.</summary>
        internal static GameObject FindAny(string query)
        {
            if (string.IsNullOrEmpty(query)) return null;
            if (MotorcycleIntegration.IsBody(query)) return MotorcycleIntegration.Template();
            { var mod = ModVehicles.Template(query); if (mod != null) return mod; }
            string q = CrewAppearance.BasePrefab(query.Trim());
            EnsureClassification();
            return Lookup(q);
        }

        /// <summary>Matches the cached root name before applying the original case-insensitive contains fallback.</summary>
        private static GameObject Lookup(string q)
        {
            GameObject go;
            if (_byName.TryGetValue(q, out go)) return go;
            foreach (var r in _roots)
                if (r != null && r.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    PatrolsModule.Verbose("Prefab \"" + q + "\" -> " + r.name + " (partial match)");
                    return r;
                }
            return null;
        }

        /// <summary>Classifies Core-cached asset FSM roots into patrol vehicle/item display rows without a second scene scan.</summary>
        private static void Scan()
        {
            _all = new List<Entry>();
            _roots = new List<GameObject>();
            _byName = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            var byGo = new Dictionary<GameObject, List<PlayMakerFSM>>();
            foreach (var f in Core.PrefabCatalog.AssetFsms)
            {
                if (f == null || f.gameObject == null) continue;
                var go = f.gameObject;
                if (go.scene.IsValid() || go.transform.parent != null) continue;   // assets only, roots only
                List<PlayMakerFSM> l;
                if (!byGo.TryGetValue(go, out l))
                {
                    byGo[go] = l = new List<PlayMakerFSM>();
                    _roots.Add(go);
                    if (!_byName.ContainsKey(go.name)) _byName[go.name] = go;
                }
                l.Add(f);
            }
            foreach (var kv in byGo)
            {
                var names = new HashSet<string>(kv.Value.Select(f => f.FsmName));
                string kind;
                if (names.Contains("RpmGear") || names.Contains("getFuel") || names.Contains("CrashDamage")) kind = "vehicle";
                else if (names.Contains("ID")) kind = IdOf(kv.Value) ?? "item";
                else continue;
                _all.Add(new Entry { Go = kv.Key, Name = kv.Key.name, Display = DisplayOf(kv.Value), Kind = kind });
            }
            if (Core.Log.Verbose) PatrolsModule.Verbose("Prefab scan: " + _all.Count + " vehicles/items, " + _roots.Count + " prefabs with FSMs");
        }

        /// <summary>Reads the native ID category used by existing patrol item matching.</summary>
        private static string IdOf(List<PlayMakerFSM> fsms)
        {
            var id = fsms.FirstOrDefault(f => f.FsmName == "ID");
            var v = id != null ? id.FsmVariables.GetFsmString("ID") : null;
            return v != null && !string.IsNullOrEmpty(v.Value) ? v.Value : null;
        }

        /// <summary>Reads ItemName or its literal native UI action text as the display fallback.</summary>
        private static string DisplayOf(List<PlayMakerFSM> fsms)
        {
            var f = fsms.FirstOrDefault(x => x.FsmName == "ItemName");
            if (f == null) return null;
            var v = f.FsmVariables.GetFsmString("ItemName");
            if (v != null && !string.IsNullOrEmpty(v.Value)) return v.Value;
            try
            {
                // wheels etc. carry the name only as the text of a UiTextSetText action
                foreach (var st in f.FsmStates)
                    foreach (var a in st.Actions)
                    {
                        if (a == null || a.GetType().Name != "UiTextSetText") continue;
                        var fld = a.GetType().GetField("text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var fs = fld != null ? fld.GetValue(a) as FsmString : null;
                        if (fs != null && !fs.UseVariable && !string.IsNullOrEmpty(fs.Value)) return fs.Value;
                    }
            }
            catch (Exception) { }
            return null;
        }
    }
}
