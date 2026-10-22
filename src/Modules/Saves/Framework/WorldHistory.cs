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
        internal static void Shutdown() { SaveBus.StateChanged -= StateChanged; Layouts.Clear(); Components.Clear(); Site.Clear(); AtPath.Clear(); _restoreCache = null; _owner = null; }
        static void Require(Type type, string name, string before)
        { if (!Patches.Patch(_owner, AccessTools.Method(type, name), prefix: AccessTools.Method(typeof(WorldHistory), before))) throw new InvalidOperationException("Terrain history hook failed: " + name); }
        static void StateChanged(string previous, string current)
        { if (current == "LoadGame" || current == "setSeed" || current == "Start") { History.Clear(); Delta.Clear(); Layouts.Clear(); _restoreCache = null; _slot = null; _generation = -1; } }
        static bool Session()
        {
            try { return OpenSession(); }
            catch (Exception error) { Failed(error); return false; }
        }
        static void Failed(Exception error)
        { SavesSystem.ReloadRequired = true; SavesSystem.Status = "Location history unavailable; reload before saving: " + error.Message; Log.Error("[System] " + SavesSystem.Status, error); }
        static bool OpenSession()
        {
            if (!SavesSystem.LocalReady || _running) return false;
            string slot = SavesSystem.ActiveFile;
            if (_slot == slot && _generation == SaveBus.Generation) return true;
            History.Clear(); Delta.Clear(); Layouts.Clear(); _restoreCache = null; _slot = slot; _generation = SaveBus.Generation;
            ES3File file;
            if (ES3File.cachedFiles.TryGetValue(slot, out file)) foreach (var pair in _data(file)) History[pair.Key] = pair.Value;
            else if (File.Exists(SaveFiles.WorldPath(slot))) foreach (var pair in _data(new ES3File(SaveFiles.Disk(slot), true))) History[pair.Key] = pair.Value;
            return true;
        }
        static void BeforeTile(Component __instance) { try { if (__instance != null) Capture(__instance.gameObject); } catch (Exception error) { Failed(error); } }
        static void BeforePool(object __instance, int startNum, int endNum)
        {
            try {
            if (!Session() || startNum >= endNum) return;
            var items = _instances(__instance); for (int i = Mathf.Max(0, startNum); i < Mathf.Min(endNum, items.Count); i++) Capture(items[i] as GameObject, PoolPrefab(__instance));
            } catch (Exception error) { Failed(error); }
        }
        static void BeforeClamp(object __instance, int targetCount)
        {
            try {
            if (!Session()) return;
            var items = _instances(__instance); for (int i = Mathf.Max(0, targetCount); i < items.Count; i++) Capture(items[i] as GameObject, PoolPrefab(__instance));
            } catch (Exception error) { Failed(error); }
        }
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
        static void InvalidateEligibility(PlayMakerFSM fsm) { if (fsm != null) Eligibility.Remove(fsm); }

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

        /// <summary>Captures relative native FSM paths once per prefab, using the first real instance's initialized topology.</summary>
        static Layout PrefabLayout(GameObject root, GameObject prefab)
        {
            Layout layout;
            if (Layouts.TryGetValue(prefab, out layout)) return layout;
            layout = new Layout();
            root.GetComponentsInChildren(true, Components);
            foreach (var fsm in Components)
            {
                if (!Saves(fsm)) continue;
                string relative = "";
                for (var t = fsm.transform; t != null && t != root.transform; t = t.parent) relative = "/" + t.name + relative;
                var target = relative.Length == 0 ? prefab.transform : prefab.transform.Find(relative.TrimStart('/'));
                fsm.GetComponents(AtPath);
                int ordinal = AtPath.IndexOf(fsm); AtPath.Clear();
                layout.Fsms.Add(new SavedFsm { Relative = relative.TrimStart('/'), Name = fsm.FsmName, Ordinal = ordinal, Template = target == null ? null : FsmUtil.Find(target.gameObject, fsm.FsmName) });
            }
            Components.Clear(); Layouts[prefab] = layout; return layout;
        }

        /// <summary>Reuses component buffers for unknown tile roots; pooled sites resolve only cached prefab save paths.</summary>
        static List<PlayMakerFSM> SiteFsms(GameObject root, GameObject prefab, bool restore)
        {
            Site.Clear();
            if (prefab == null)
            {
                root.GetComponentsInChildren(true, Components);
                foreach (var fsm in Components)
                    if (fsm != null && fsm.enabled && fsm.Fsm.Initialized && Saves(fsm)) Site.Add(fsm);
                Components.Clear(); return Site;
            }
            foreach (var saved in PrefabLayout(root, prefab).Fsms)
            {
                var target = saved.Relative.Length == 0 ? root.transform : root.transform.Find(saved.Relative);
                if (target == null) continue;
                target.GetComponents(AtPath);
                if (saved.Ordinal >= 0 && saved.Ordinal < AtPath.Count)
                {
                    var fsm = AtPath[saved.Ordinal];
                    if (fsm == null || fsm.FsmName != saved.Name || !fsm.enabled || !fsm.Fsm.Initialized || !Event(fsm, SaveEvent) || restore && !Event(fsm, LoadEvent)) { AtPath.Clear(); continue; }
                    if (restore && saved.Template != null)
                    {
                        foreach (var v in fsm.FsmVariables.IntVariables) { var original = saved.Template.FsmVariables.FindFsmInt(v.Name); if (original != null) v.Value = original.Value; }
                        foreach (var v in fsm.FsmVariables.FloatVariables) { var original = saved.Template.FsmVariables.FindFsmFloat(v.Name); if (original != null) v.Value = original.Value; }
                        foreach (var v in fsm.FsmVariables.BoolVariables) { var original = saved.Template.FsmVariables.FindFsmBool(v.Name); if (original != null) v.Value = original.Value; }
                    }
                    Site.Add(fsm);
                }
                AtPath.Clear();
            }
            return Site;
        }

        /// <summary>Captures a streamed site's save actions before native pooling destroys or relocates it.</summary>
        static void Capture(GameObject root, GameObject prefab = null)
        {
            if (root == null || !Session()) return;
            var fsms = SiteFsms(root, prefab, false); if (fsms.Count == 0) return;
            Run(fsms, false); Site.Clear();
            if (SavesSystem.Busy) SaveBus.ReportFailure("Terrain locations moved during the world save");
        }

        /// <summary>Restores pooled site defaults and retained ES3 data using the prefab's cached save-action layout.</summary>
        static void Restore(GameObject root, GameObject prefab)
        {
            if (root == null || prefab == null || !Session()) return;
            var fsms = SiteFsms(root, prefab, true);
            if (fsms.Count != 0) Run(fsms, true);
            Site.Clear();
        }
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
            catch (Exception error) { SavesSystem.ReloadRequired = true; SavesSystem.Status = "Location history could not be preserved. Reload before saving: " + error.Message; Log.Error("[System] " + SavesSystem.Status, error); }
            finally { ES3Settings.defaultSettings.path = path; ES3Settings.defaultSettings.location = location; ES3File.cachedFiles.Remove(CaptureFile); _running = _restoring = false; }
        }
        internal static bool NativeError(string error)
        {
            if (!_running) return false;
            // Missing records on a newly generated site use the reset prefab defaults.
            if (_restoring && error.IndexOf("KeyNotFoundException", StringComparison.Ordinal) >= 0) return true;
            _error = error; return false;
        }
        internal static void Merge()
        {
            if (SavesSystem.ReloadRequired) throw new IOException("Location history requires a reload");
            ES3File file;
            if (Delta.Count == 0 || !ES3File.cachedFiles.TryGetValue(SavesSystem.ActiveFile, out file)) return;
            var records = _data(file); foreach (var pair in Delta) records[pair.Key] = pair.Value;
            // SavesSystem.Stamp follows this call and marks the same cache dirty.
        }
        internal static void Accepted()
        {
            ES3File file; if (!ES3File.cachedFiles.TryGetValue(SavesSystem.ActiveFile, out file)) return;
            History.Clear(); foreach (var pair in _data(file)) History[pair.Key] = pair.Value; Delta.Clear(); _restoreCache = null;
        }
    }
}
