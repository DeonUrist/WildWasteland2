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
    // =============================================================== prefab lookup
    // Asset prefabs = GameObjects without a scene. Matched by prefab name first, then by the in-game name
    // (ItemName FSM: variable ItemName, or the text of its UiTextSetText actions), exact then contains.
    internal static class Prefabs
    {
        private class Entry { public GameObject Go; public string Name; public string Display; public string Kind; }
        private static List<Entry> _all;
        private static List<GameObject> _roots;                                   // every asset root with a PlayMaker FSM (crew, carcasses, loot...)
        private static Dictionary<string, GameObject> _byName;
        private static Dictionary<string, GameObject> _exact;
        private static Dictionary<string, GameObject> _folded;
        private static ES3ReferenceMgrBase[] _managers;
        private static int[] _managerCounts;
        internal static int Generation;

        internal static void Invalidate() { _all = null; _roots = null; _byName = null; _exact = null; _folded = null; _managers = null; _managerCounts = null; Generation++; }
        internal static void Install(Core.IModule owner)
        {
            if (!Core.Patches.Patch(owner, Core.Patches.Method(typeof(ES3ReferenceMgrBase), "AddPrefab", new[] { typeof(ES3Prefab) }),
                postfix: Core.Patches.Method(typeof(Prefabs), "AfterAddPrefab"))) throw new InvalidOperationException("Prefab registration hook failed");
            if (!Core.Patches.Patch(owner, Core.Patches.Method(typeof(ES3ReferenceMgrBase), "Awake"),
                postfix: Core.Patches.Method(typeof(Prefabs), "AfterManagerAwake"))) throw new InvalidOperationException("Prefab manager hook failed");
        }
        private static void AfterAddPrefab(bool __result) { if (PatrolsModule.Running && __result) Invalidate(); }
        private static void AfterManagerAwake() { if (PatrolsModule.Running) Invalidate(); }

        // Warm after scene loading, never in Plugin.Awake. Counts also catch mods that append directly to ES3's public list.
        internal static void Refresh()
        {
            if (!PatrolsModule.Running) return;
            ModVehicles.Refresh();
            if (_managers != null)
                for (int i = 0; i < _managers.Length; i++)
                    if ((_managers[i] != null && _managers[i].prefabs != null ? _managers[i].prefabs.Count : -1) != _managerCounts[i])
                    { Invalidate(); break; }
            if (_exact == null) ScanExact();
            if (_all == null) Scan();
        }

        private static void ScanExact()
        {
            _exact = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && !go.scene.IsValid() && go.transform.parent == null && !_exact.ContainsKey(go.name)) _exact[go.name] = go;
            _managers = Resources.FindObjectsOfTypeAll<ES3ReferenceMgrBase>();
            _managerCounts = new int[_managers.Length];
            for (int i = 0; i < _managers.Length; i++)
            {
                var manager = _managers[i];
                _managerCounts[i] = manager != null && manager.prefabs != null ? manager.prefabs.Count : -1;
                if (_managerCounts[i] < 0) continue;
                foreach (var prefab in manager.prefabs) if (prefab != null) _exact[prefab.name] = prefab.gameObject;
            }
            // Keep the old stable preference for ES3-backed roots when asset names differ only by case.
            _folded = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in _exact)
            {
                GameObject previous;
                if (!_folded.TryGetValue(pair.Key, out previous) || previous == null
                    || (pair.Value.GetComponent<ES3Prefab>() != null && previous.GetComponent<ES3Prefab>() == null)) _folded[pair.Key] = pair.Value;
            }
            if (Core.Log.Verbose) PatrolsModule.Verbose("Prefab name index: " + _exact.Count + " names (generation " + Generation + ")");
        }

        // Exported names must resolve exactly: a missing mod must never spawn a similarly named vanilla frame.
        // Runtime factories are shared with the exporter, availability checks and the builder.
        internal static GameObject Exact(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;
            string name = query.Trim();
            GameObject hit;
            if (_exact != null && _exact.TryGetValue(name, out hit) && hit != null) return hit;
            if (MotorcycleIntegration.IsBody(name)) return MotorcycleIntegration.Template();
            var mod = ModVehicles.Template(name);
            if (mod != null) return mod;
            if (_exact == null) ScanExact();
            if (_exact.TryGetValue(name, out hit)) return hit;
            return _folded.TryGetValue(name, out hit) ? hit : null;
        }

        internal static GameObject Find(string query, string kind)
        {
            if (string.IsNullOrEmpty(query)) return null;
            if (MotorcycleIntegration.IsBody(query)) return MotorcycleIntegration.Template();
            { var mod = ModVehicles.Template(query); if (mod != null) return mod; }
            if (_all == null) Scan();
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
        internal static GameObject FindAny(string query)
        {
            if (string.IsNullOrEmpty(query)) return null;
            if (MotorcycleIntegration.IsBody(query)) return MotorcycleIntegration.Template();
            { var mod = ModVehicles.Template(query); if (mod != null) return mod; }
            string q = CrewAppearance.BasePrefab(query.Trim());
            if (_all == null) Scan();
            return Lookup(q);
        }

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

        private static void Scan()
        {
            _all = new List<Entry>();
            _roots = new List<GameObject>();
            _byName = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            var byGo = new Dictionary<GameObject, List<PlayMakerFSM>>();
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
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

        private static string IdOf(List<PlayMakerFSM> fsms)
        {
            var id = fsms.FirstOrDefault(f => f.FsmName == "ID");
            var v = id != null ? id.FsmVariables.GetFsmString("ID") : null;
            return v != null && !string.IsNullOrEmpty(v.Value) ? v.Value : null;
        }

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
