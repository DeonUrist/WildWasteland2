using Patches = WildWasteland.Core.Patches;
using WildWasteland.Core;
using UI = WildWasteland.Core.UI;
using Events = WildWasteland.Core.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using ES3Internal;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Saves
{
    // Adapted from Apocasaver 2.0.4 WorldHistory: capture before terrain pooling, reset templates before restore.
    // Only immutable ES3 records are retained; no unloaded scene objects are pinned by the history ledger.
    internal static class WorldHistory
    {
        const string CaptureFile = "ww-location-history.es3";
        static readonly Dictionary<string, ES3Data> History = new Dictionary<string, ES3Data>(), Delta = new Dictionary<string, ES3Data>();
        sealed class SavedFsm
        {
            internal string Relative, Name;
            internal int Ordinal;
            internal PlayMakerFSM Template;
        }
        sealed class Layout
        {
            internal readonly List<SavedFsm> Fsms = new List<SavedFsm>();
        }
        sealed class InstanceSite
        {
            internal bool Dirty;
            internal GameObject Prefab;
            internal readonly List<PlayMakerFSM> Fsms = new List<PlayMakerFSM>();
            internal readonly List<PlayMakerFSM> Templates = new List<PlayMakerFSM>();
        }
        static ConditionalWeakTable<GameObject, InstanceSite> Instances = new ConditionalWeakTable<GameObject, InstanceSite>();
        /// <summary>Caches action eligibility under a weak native FSM identity.</summary>
        sealed class SaveEligibility { internal bool Value; }
        static readonly ConditionalWeakTable<PlayMakerFSM, SaveEligibility> Eligibility = new ConditionalWeakTable<PlayMakerFSM, SaveEligibility>();
        static readonly Dictionary<GameObject, Layout> Layouts = new Dictionary<GameObject, Layout>();
        static readonly List<PlayMakerFSM> Components = new List<PlayMakerFSM>(), Site = new List<PlayMakerFSM>(), AtPath = new List<PlayMakerFSM>();
        static readonly FsmEvent SaveEvent = FsmEvent.GetFsmEvent("Save"), LoadEvent = FsmEvent.GetFsmEvent("Load");
        static Func<object, Dictionary<string, ES3Data>> _data;
        static Func<object, IList> _instances;
        static Func<object, object> _prototype;
        static Func<object, GameObject> _prefab;
        static Func<object, bool> _dirty;
        static ES3File _restoreCache;
        static string _slot, _error;
        static int _generation = -1;
        static bool _running, _restoring;
        static IModule _owner;
        /// <summary>Installs the terrain snapshot barrier and pooling observers for Saves.</summary>
        internal static void Install(IModule owner)
        {
            _owner = owner;
            Hooks.FsmEnable.Postfix(owner, InvalidateEligibility);
            SaveTerrain.Install(owner);
            _data = Access.Get<Dictionary<string, ES3Data>>(typeof(ES3File), "cache");
            _dirty = Access.Get<bool>(typeof(ES3File), "dirty");
            var tile = typeof(MapMagic.Terrains.TerrainTile); var pool = AccessTools.TypeByName("Den.Tools.ObjectsPool+Pool");
            if (pool == null) throw new MissingMemberException("Terrain object pool unavailable");
            _instances = Access.Get<IList>(pool, "instances"); _prototype = Access.Get<object>(pool, "prototype");
            var prototype = AccessTools.Field(pool, "prototype"); if (prototype == null) throw new MissingFieldException("Terrain pool prototype unavailable");
            _prefab = Access.Get<GameObject>(prototype.FieldType, "prefab");
            foreach (string name in new[] { "Move", "Remove", "ResetTerrain" }) Require(tile, name, "BeforeTile");
            Require(pool, "ClampCount", "BeforeClamp");
            if (!Patches.Patch(owner, AccessTools.Method(pool, "MoveItems"), prefix: AccessTools.Method(typeof(WorldHistory), "BeforePool"), postfix: AccessTools.Method(typeof(WorldHistory), "AfterPool"))) throw new InvalidOperationException("Terrain history placement hook failed");
            SaveBus.StateChanged += StateChanged;
        }
        /// <summary>Detaches resets and releases template/cache references while retaining immutable current-world history.</summary>
        internal static void Shutdown() { SaveBus.StateChanged -= StateChanged; Layouts.Clear(); Instances = new ConditionalWeakTable<GameObject, InstanceSite>(); Components.Clear(); Site.Clear(); AtPath.Clear(); _restoreCache = null; _owner = null; }
        /// <summary>Installs a mandatory module-owned native observer or fails initialization.</summary>
        static void Require(Type type, string name, string before)
        { if (!Patches.Patch(_owner, AccessTools.Method(type, name), prefix: AccessTools.Method(typeof(WorldHistory), before))) throw new InvalidOperationException("Terrain history hook failed: " + name); }
        /// <summary>Clears immutable history and prefab layouts at native world boundaries.</summary>
        static void StateChanged(string previous, string current)
        { if (current == "LoadGame" || current == "setSeed" || current == "Start") { History.Clear(); Delta.Clear(); Layouts.Clear(); Instances = new ConditionalWeakTable<GameObject, InstanceSite>(); _restoreCache = null; _slot = null; _generation = -1; } }
        /// <summary>Opens the active history ledger and turns storage failures into reload-required state.</summary>
        static bool Session()
        {
            try { return OpenSession(); }
            catch (Exception error) { Failed(error); return false; }
        }
        /// <summary>Prevents later saves when streamed native state could not be preserved.</summary>
        static void Failed(Exception error)
        { SaveSystem.ReloadRequired = true; SaveSystem.Status = "Location history unavailable; reload before saving: " + error.Message; Log.Error("[System] " + SaveSystem.Status, error); }
        /// <summary>Loads original ES3 records once per active slot and generation.</summary>
        static bool OpenSession()
        {
            if (!SaveSystem.LocalReady || _running) return false;
            string slot = SaveSystem.ActiveFile;
            if (_slot == slot && _generation == SaveBus.Generation) return true;
            History.Clear(); Delta.Clear(); Layouts.Clear(); Instances = new ConditionalWeakTable<GameObject, InstanceSite>(); _restoreCache = null; _slot = slot; _generation = SaveBus.Generation;
            ES3File file;
            if (ES3File.cachedFiles.TryGetValue(slot, out file)) foreach (var pair in _data(file)) History[pair.Key] = pair.Value;
            else if (File.Exists(SaveFiles.WorldPath(slot))) foreach (var pair in _data(new ES3File(SaveFiles.Disk(slot), true))) History[pair.Key] = pair.Value;
            return true;
        }
        /// <summary>Captures location records before a terrain tile is moved or removed.</summary>
        static void BeforeTile(Component __instance) { try { if (__instance != null) Capture(__instance.gameObject); } catch (Exception error) { Failed(error); } }
        /// <summary>Captures only the native pool range about to move.</summary>
        static void BeforePool(object __instance, int startNum, int endNum)
        {
            try {
            if (!Session() || startNum >= endNum) return;
            var items = _instances(__instance); for (int i = Mathf.Max(0, startNum); i < Mathf.Min(endNum, items.Count); i++) Capture(items[i] as GameObject, PoolPrefab(__instance));
            } catch (Exception error) { Failed(error); }
        }
        /// <summary>Captures pool instances that shrinking is about to discard.</summary>
        static void BeforeClamp(object __instance, int targetCount)
        {
            try {
            if (!Session()) return;
            var items = _instances(__instance); for (int i = Mathf.Max(0, targetCount); i < items.Count; i++) Capture(items[i] as GameObject, PoolPrefab(__instance));
            } catch (Exception error) { Failed(error); }
        }
        /// <summary>Restores placed pool instances from authored defaults and retained records.</summary>
        static void AfterPool(object __instance, int startNum, int endNum)
        {
            try {
            if (!Session() || startNum >= endNum) return;
            var items = _instances(__instance); var prototype = _prototype(__instance); if (prototype == null) return; var prefab = _prefab(prototype);
            for (int i = Mathf.Max(0, startNum); i < Mathf.Min(endNum, items.Count); i++) Restore(items[i] as GameObject, prefab);
            } catch (Exception error) { Failed(error); }
        }
        /// <summary>Reads the pool's stable authored prefab without retaining streamed instance roots.</summary>
        static GameObject PoolPrefab(object pool)
        { var prototype = _prototype(pool); return prototype == null ? null : _prefab(prototype); }

        /// <summary>Checks a cached global event identity without repeatedly comparing event names.</summary>
        static bool Event(PlayMakerFSM fsm, FsmEvent eventType)
        { foreach (var transition in fsm.Fsm.GlobalTransitions) if (ReferenceEquals(transition.FsmEvent, eventType)) return true; return false; }

        /// <summary>Rechecks native save-action eligibility when a pooled or newly enabled FSM rebuilds its actions.</summary>
        static void InvalidateEligibility(PlayMakerFSM fsm)
        {
            if (fsm == null) return;
            Eligibility.Remove(fsm);
            for (var t = fsm.transform; t != null; t = t.parent)
            {
                InstanceSite site;
                if (Instances.TryGetValue(t.gameObject, out site)) site.Dirty = true;
            }
        }

        /// <summary>Caches native save-action eligibility per weak FSM identity; unloaded instances are not retained.</summary>
        static bool Saves(PlayMakerFSM fsm)
        {
            if (fsm == null || !Event(fsm, SaveEvent)) return false;
            SaveEligibility eligibility;
            if (Eligibility.TryGetValue(fsm, out eligibility)) return eligibility.Value;
            bool saves = false;
            foreach (var state in fsm.FsmStates) foreach (var action in state.Actions)
                if (action is ES3PlayMaker.Save || action is ES3PlayMaker.SaveAll) saves = true;
            Eligibility.Add(fsm, new SaveEligibility { Value = saves });
            return saves;
        }

        /// <summary>Captures authored save paths once per prefab; instance additions remain in their separate weak cache.</summary>
        static Layout PrefabLayout(GameObject prefab)
        {
            Layout layout;
            if (Layouts.TryGetValue(prefab, out layout)) return layout;
            layout = new Layout();
            prefab.GetComponentsInChildren(true, Components);
            foreach (var fsm in Components)
            {
                if (!Saves(fsm)) continue;
                string relative = Relative(fsm.transform, prefab.transform);
                fsm.GetComponents(AtPath);
                int ordinal = AtPath.IndexOf(fsm); AtPath.Clear();
                layout.Fsms.Add(new SavedFsm { Relative = relative, Name = fsm.FsmName, Ordinal = ordinal, Template = fsm });
            }
            Components.Clear(); Layouts[prefab] = layout; return layout;
        }

        /// <summary>Builds the native child-name path once for a prefab layout or a changed instance topology.</summary>
        static string Relative(Transform child, Transform root)
        {
            string relative = "";
            for (var t = child; t != null && t != root; t = t.parent) relative = "/" + t.name + relative;
            return relative.TrimStart('/');
        }

        /// <summary>Resolves exact ordinal and name together, preserving independent defaults of duplicate-named FSM components.</summary>
        static PlayMakerFSM Template(GameObject prefab, Layout layout, string relative, string name, int ordinal)
        {
            if (prefab == null) return null;
            foreach (var saved in layout.Fsms)
                if (saved.Relative == relative && saved.Name == name && saved.Ordinal == ordinal) return saved.Template;
            // A non-authored instance shape may still map to a newly changed template node.
            var target = relative.Length == 0 ? prefab.transform : prefab.transform.Find(relative);
            if (target == null) return null;
            target.GetComponents(AtPath);
            var template = ordinal >= 0 && ordinal < AtPath.Count && AtPath[ordinal].FsmName == name ? AtPath[ordinal] : null;
            AtPath.Clear(); return template;
        }

        /// <summary>Scans each live hierarchy once, then rebuilds only that weak instance after an FSM enable/topology change.</summary>
        static InstanceSite Instance(GameObject root, GameObject prefab)
        {
            InstanceSite site;
            if (Instances.TryGetValue(root, out site) && !site.Dirty && site.Prefab == prefab) return site;
            if (site == null) { site = new InstanceSite(); Instances.Add(root, site); }
            site.Fsms.Clear(); site.Templates.Clear(); site.Prefab = prefab;
            Layout layout = prefab == null ? null : PrefabLayout(prefab);
            root.GetComponentsInChildren(true, Components);
            foreach (var fsm in Components)
            {
                if (!Saves(fsm)) continue;
                site.Fsms.Add(fsm);
                string relative = Relative(fsm.transform, root.transform);
                fsm.GetComponents(AtPath);
                int ordinal = AtPath.IndexOf(fsm); AtPath.Clear();
                site.Templates.Add(Template(prefab, layout, relative, fsm.FsmName, ordinal));
            }
            Components.Clear(); site.Dirty = false; return site;
        }

        /// <summary>Reuses live site metadata while retaining instance-only save FSMs and checking native enabled/event state each visit.</summary>
        static List<PlayMakerFSM> SiteFsms(GameObject root, GameObject prefab, bool restore)
        {
            Site.Clear();
            var site = Instance(root, prefab);
            for (int i = 0; i < site.Fsms.Count; i++)
            {
                var fsm = site.Fsms[i];
                if (fsm == null || !fsm.enabled || !fsm.Fsm.Initialized || !Event(fsm, SaveEvent) || restore && !Event(fsm, LoadEvent)) continue;
                var template = site.Templates[i];
                if (restore && template != null)
                {
                    foreach (var v in fsm.FsmVariables.IntVariables) { var original = template.FsmVariables.FindFsmInt(v.Name); if (original != null) v.Value = original.Value; }
                    foreach (var v in fsm.FsmVariables.FloatVariables) { var original = template.FsmVariables.FindFsmFloat(v.Name); if (original != null) v.Value = original.Value; }
                    foreach (var v in fsm.FsmVariables.BoolVariables) { var original = template.FsmVariables.FindFsmBool(v.Name); if (original != null) v.Value = original.Value; }
                }
                Site.Add(fsm);
            }
            return Site;
        }

        /// <summary>Captures a streamed site's save actions before native pooling destroys or relocates it.</summary>
        static void Capture(GameObject root, GameObject prefab = null)
        {
            if (root == null || !Session()) return;
            var fsms = SiteFsms(root, prefab, false); if (fsms.Count == 0) return;
            Run(fsms, false); Site.Clear();
            if (SaveSystem.Busy) SaveBus.ReportFailure("Terrain locations moved during the world save");
        }

        /// <summary>Restores pooled site defaults and retained ES3 data using the prefab's cached save-action layout.</summary>
        static void Restore(GameObject root, GameObject prefab)
        {
            if (root == null || prefab == null || !Session()) return;
            var fsms = SiteFsms(root, prefab, true);
            if (fsms.Count != 0) Run(fsms, true);
            Site.Clear();
        }
        /// <summary>Runs native save/load events against a private cache and restores global ES3 settings afterward.</summary>
        static void Run(List<PlayMakerFSM> fsms, bool restore)
        {
            string path = ES3Settings.defaultSettings.path; var location = ES3Settings.defaultSettings.location;
            try
            {
                _running = true; _restoring = restore; _error = null;
                ES3File file;
                if (restore && _restoreCache != null && !_dirty(_restoreCache)) file = _restoreCache;
                else
                {
                    file = new ES3File(new ES3Settings(CaptureFile) { location = ES3.Location.Cache }, false);
                    if (restore) { foreach (var pair in History) _data(file)[pair.Key] = pair.Value; _restoreCache = file; }
                }
                var records = _data(file);
                ES3File.cachedFiles[CaptureFile] = file;
                ES3Settings.defaultSettings.path = CaptureFile; ES3Settings.defaultSettings.location = ES3.Location.Cache;
                foreach (var fsm in fsms) fsm.Fsm.Event(restore ? LoadEvent : SaveEvent);
                if (_error != null) throw new IOException(_error);
                if (!restore) { foreach (var pair in records) { History[pair.Key] = pair.Value; Delta[pair.Key] = pair.Value; } _restoreCache = null; }
            }
            catch (Exception error) { SaveSystem.ReloadRequired = true; SaveSystem.Status = "Location history could not be preserved. Reload before saving: " + error.Message; Log.Error("[System] " + SaveSystem.Status, error); }
            finally { ES3Settings.defaultSettings.path = path; ES3Settings.defaultSettings.location = location; ES3File.cachedFiles.Remove(CaptureFile); _running = _restoring = false; }
        }
        /// <summary>Handles expected new-site history misses and records other native save errors.</summary>
        internal static bool NativeError(string error)
        {
            if (!_running) return false;
            // Missing records on a newly generated site use the reset prefab defaults.
            if (_restoring && error.IndexOf("KeyNotFoundException", StringComparison.Ordinal) >= 0) return true;
            _error = error; return false;
        }
        /// <summary>Merges streamed location deltas into the native cache before its transaction stamp.</summary>
        internal static void Merge()
        {
            if (SaveSystem.ReloadRequired) throw new IOException("Location history requires a reload");
            ES3File file;
            if (Delta.Count == 0 || !ES3File.cachedFiles.TryGetValue(SaveSystem.ActiveFile, out file)) return;
            var records = _data(file); foreach (var pair in Delta) records[pair.Key] = pair.Value;
            // SaveSystem.Stamp follows this call and marks the same cache dirty.
        }
        /// <summary>Refreshes immutable location history only after verified save publication.</summary>
        internal static void Accepted()
        {
            ES3File file; if (!ES3File.cachedFiles.TryGetValue(SaveSystem.ActiveFile, out file)) return;
            History.Clear(); foreach (var pair in _data(file)) History[pair.Key] = pair.Value; Delta.Clear(); _restoreCache = null;
        }
    }
}
