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
    // Perf (README 6.14/8#10): no List/array allocation per tile move (shared buffers), template and save-action answers are
    // cached under weak FSM identities, global-event checks compare the cached FsmEvent reference before the name.
    internal static class WorldHistory
    {
        const string CaptureFile = "ww-location-history.es3";
        static readonly Dictionary<string, ES3Data> History = new Dictionary<string, ES3Data>(), Delta = new Dictionary<string, ES3Data>();
        /// <summary>Resolved authored template of one live save FSM, cached under the weak FSM identity.</summary>
        sealed class TemplateRef { internal GameObject Prefab; internal PlayMakerFSM Template; }
        /// <summary>Cached "has ES3 Save/SaveAll actions" answer for an initialized FSM.</summary>
        sealed class Eligible { internal bool Value; }
        static readonly ConditionalWeakTable<PlayMakerFSM, TemplateRef> Templates = new ConditionalWeakTable<PlayMakerFSM, TemplateRef>();
        static readonly ConditionalWeakTable<PlayMakerFSM, Eligible> Eligibility = new ConditionalWeakTable<PlayMakerFSM, Eligible>();
        static readonly List<PlayMakerFSM> Components = new List<PlayMakerFSM>(), Site = new List<PlayMakerFSM>();
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
        internal static void Shutdown() { SaveBus.StateChanged -= StateChanged; Components.Clear(); Site.Clear(); _restoreCache = null; _owner = null; }
        /// <summary>Installs a mandatory module-owned native observer or fails initialization.</summary>
        static void Require(Type type, string name, string before)
        { if (!Patches.Patch(_owner, AccessTools.Method(type, name), prefix: AccessTools.Method(typeof(WorldHistory), before))) throw new InvalidOperationException("Terrain history hook failed: " + name); }
        /// <summary>Clears immutable history and prefab layouts at native world boundaries.</summary>
        static void StateChanged(string previous, string current)
        { if (current == "LoadGame" || current == "setSeed" || current == "Start") { History.Clear(); Delta.Clear(); _restoreCache = null; _slot = null; _generation = -1; } }
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
            History.Clear(); Delta.Clear(); _restoreCache = null; _slot = slot; _generation = SaveBus.Generation;
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
            var items = _instances(__instance); for (int i = Mathf.Max(0, startNum); i < Mathf.Min(endNum, items.Count); i++) Capture(items[i] as GameObject);
            } catch (Exception error) { Failed(error); }
        }
        /// <summary>Captures pool instances that shrinking is about to discard.</summary>
        static void BeforeClamp(object __instance, int targetCount)
        {
            try {
            if (!Session()) return;
            var items = _instances(__instance); for (int i = Mathf.Max(0, targetCount); i < items.Count; i++) Capture(items[i] as GameObject);
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
        /// <summary>True when the FSM has a global transition on the event (cached reference first, then the original name compare).</summary>
        static bool Event(PlayMakerFSM fsm, FsmEvent cached, string name)
        {
            foreach (var transition in fsm.Fsm.GlobalTransitions)
            {
                var ev = transition.FsmEvent;
                if (ev != null && (ReferenceEquals(ev, cached) || ev.Name == name)) return true;
            }
            return false;
        }

        /// <summary>True when an initialized FSM owns an ES3 Save/SaveAll action; the answer is cached per weak FSM identity.</summary>
        static bool HasSaveActions(PlayMakerFSM fsm)
        {
            Eligible cached;
            if (Eligibility.TryGetValue(fsm, out cached)) return cached.Value;
            bool saves = false;
            foreach (var state in fsm.FsmStates) foreach (var action in state.Actions) if (action is ES3PlayMaker.Save || action is ES3PlayMaker.SaveAll) saves = true;
            Eligibility.Add(fsm, new Eligible { Value = saves });
            return saves;
        }

        /// <summary>Fills the shared <see cref="Site"/> buffer with the enabled, initialized save FSMs under a streamed root (no allocation per tile move).</summary>
        static List<PlayMakerFSM> SiteFsms(GameObject root)
        {
            Site.Clear();
            root.GetComponentsInChildren(true, Components);
            for (int i = 0; i < Components.Count; i++)
            {
                var fsm = Components[i];
                if (fsm == null || !fsm.enabled || !fsm.Fsm.Initialized || !Event(fsm, SaveEvent, "Save") || !HasSaveActions(fsm)) continue;
                Site.Add(fsm);
            }
            Components.Clear();
            return Site;
        }

        /// <summary>Captures a streamed site's save actions before native pooling destroys or relocates it.</summary>
        static void Capture(GameObject root)
        {
            if (root == null || !Session()) return;
            var fsms = SiteFsms(root); if (fsms.Count == 0) return;
            Run(fsms, false); Site.Clear();
            if (SaveSystem.Busy) SaveBus.ReportFailure("Terrain locations moved during the world save");
        }

        /// <summary>Authored template FSM for a live save FSM (same child path and FSM name under the pool prefab), cached per instance.</summary>
        static PlayMakerFSM Template(PlayMakerFSM fsm, GameObject root, GameObject prefab)
        {
            TemplateRef cached;
            if (Templates.TryGetValue(fsm, out cached))
            {
                // Reuse while the pool prefab is the same and a resolved template still exists (re-resolve destroyed ones, as before).
                if (ReferenceEquals(cached.Prefab, prefab) && (ReferenceEquals(cached.Template, null) || cached.Template != null)) return cached.Template;
                Templates.Remove(fsm);
            }
            string relative = "";
            for (var t = fsm.transform; t != null && t != root.transform; t = t.parent) relative = "/" + t.name + relative;
            var target = relative.Length == 0 ? prefab.transform : prefab.transform.Find(relative.TrimStart('/'));
            var template = target == null ? null : FsmUtil.Find(target.gameObject, fsm.FsmName);
            Templates.Add(fsm, new TemplateRef { Prefab = prefab, Template = template });
            return template;
        }

        /// <summary>Resets pooled site variables to the prefab defaults, then replays the retained ES3 records into them.</summary>
        static void Restore(GameObject root, GameObject prefab)
        {
            if (root == null || prefab == null || !Session()) return;
            var fsms = SiteFsms(root);
            for (int i = fsms.Count - 1; i >= 0; i--)
            {
                var fsm = fsms[i]; if (!Event(fsm, LoadEvent, "Load")) { fsms.RemoveAt(i); continue; }
                var template = Template(fsm, root, prefab);
                if (template == null) continue;
                foreach (var v in fsm.FsmVariables.IntVariables) { var original = template.FsmVariables.FindFsmInt(v.Name); if (original != null) v.Value = original.Value; }
                foreach (var v in fsm.FsmVariables.FloatVariables) { var original = template.FsmVariables.FindFsmFloat(v.Name); if (original != null) v.Value = original.Value; }
                foreach (var v in fsm.FsmVariables.BoolVariables) { var original = template.FsmVariables.FindFsmBool(v.Name); if (original != null) v.Value = original.Value; }
            }
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
                foreach (var fsm in fsms) fsm.SendEvent(restore ? "Load" : "Save");
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
