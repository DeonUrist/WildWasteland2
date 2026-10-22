using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using WildWasteland.Core.Events;

namespace WildWasteland.Core
{
    /// <summary>
    /// The only watcher of the game's SaveLoadGame FSM (AGENTS.md 5.5). Reads ActiveStateName once per frame and raises:
    ///  - <see cref="StateChanged"/>(previous, current) for modules that used to poll the FSM themselves (ported watchers),
    ///  - IModule.OnSaveBegin ("SaveGame"), OnSaveEnd (back to isPlay after a save), OnLoadBegin ("LoadGame"),
    ///    OnWorldReady (first isPlay after LoadGame / setSeed / a new scene).
    /// <see cref="AfterSave"/> runs a callback ~1 s after the save returned to isPlay (ES3 cache rule: writes done earlier are overwritten
    /// by the game's cache flush). Slot = ES3Settings.defaultSettings.path, valid when it matches SaveGameN.es3.
    /// Module save data formats are kept verbatim from the source mods (save compatibility); see docs/decisions.md.
    /// </summary>
    public static class SaveBus
    {
        public delegate void StateChange(string previous, string current);
        /// <summary>Raised on every SaveLoadGame state change (also when the FSM is first found: previous = null).</summary>
        public static event StateChange StateChanged;
        /// <summary>Raised for every reported save failure (owner, message); the Saves module subscribes in Wave B.</summary>
        public static event Action<IModule, string> FailureReported;

        /// <summary>Explicit save-failure channel (replaces the old Log.Error side effect): a save participant reports a swallowed
        /// IO/serialization failure so the running save transaction is failed instead of publishing a partial snapshot. The caller
        /// logs the cause itself; this only forwards it to the save framework.</summary>
        public static void ReportFailure(IModule owner, string message)
        {
            var h = FailureReported;
            if (h == null) return;
            foreach (Action<IModule, string> handler in h.GetInvocationList())
                try { handler(owner, message); } catch (Exception e) { Log.Error(null, "SaveBus.FailureReported handler", e); }
        }

        /// <summary>Old owner-less form of <see cref="ReportFailure(IModule,string)"/>.</summary>
        [Obsolete("refactor: use SaveBus.ReportFailure(owner, message)")]
        public static void ReportFailure(string message) { ReportFailure(null, message); }
        internal static event Action<UI.ToolLayout> CleanupSettings;
        /// <summary>Draws the cleanup settings contributed by modules (Saves page).</summary>
        internal static void DrawCleanupSettings(UI.ToolLayout layout) { var draw = CleanupSettings; if (draw != null) draw(layout); }
        internal static event Action SaveCancelled;
        /// <summary>Aborts the running native save and returns SaveLoadGame to isPlay.</summary>
        internal static void CancelSave()
        {
            _after.Clear(); Saving = false; _saveSlot = null;
            var cancel = SaveCancelled; if (cancel != null) cancel();
            var registry = GameRefs.NewGOArrayList != null ? FsmUtil.Find(GameRefs.NewGOArrayList, "Save_NewGO_ArrayList") : null;
            if (registry != null) registry.Fsm.SetState("off");
            if (GameRefs.SaveLoadGame != null) GameRefs.SaveLoadGame.Fsm.SetState("isPlay");
        }

        public static string State { get; private set; }
        public static bool Saving { get; private set; }
        public static bool Loading { get; private set; }
        public static int Generation { get; private set; }
        /// <summary>SaveLoadGame not in isPlay (a save or load is running). Spawns should wait.</summary>
        public static bool Busy { get { return State != null && State != "isPlay"; } }
        internal static bool PendingAfterSave { get { return _after.Count != 0; } }

        static bool _pendingWorldReady = true;
        static string _saveSlot;
        static readonly List<KeyValuePair<float, Action>> _after = new List<KeyValuePair<float, Action>>();
        static readonly Regex WorldSlotRx = new Regex(@"^(SaveGame([1-9]|10)|WW_(live|save|auto)-[a-fA-F0-9]{32})\.es3$", RegexOptions.IgnoreCase);

        /// <summary>Current validated native or WW save identity, or null.</summary>
        public static string Slot
        {
            get
            {
                try
                {
                    var p = ES3Settings.defaultSettings.path;
                    var n = string.IsNullOrEmpty(p) ? null : Path.GetFileName(p);
                    return n != null && WorldSlotRx.IsMatch(n) ? n : null;
                }
                catch { return null; }
            }
        }



        static readonly Dictionary<string, SaveKey> _keys = new Dictionary<string, SaveKey>();

        /// <summary>Helper for one ES3 key in the slot file (cache rule + StoreCachedFile + verify). One instance per key name.</summary>
        public static SaveKey Key(string es3Key)
        {
            SaveKey k;
            if (!_keys.TryGetValue(es3Key, out k)) { k = new SaveKey(es3Key); _keys[es3Key] = k; }
            return k;
        }

        /// <summary>Slot of the save that is running (set on SaveGame, cleared on scene load/cancel), else null.</summary>
        public static string SaveSlot { get { return Saving ? _saveSlot : null; } }

        /// <summary>Run <paramref name="a"/> once, <paramref name="delay"/> unscaled seconds after the next isPlay following a save.</summary>
        public static void AfterSave(Action a, float delay = 1f) { _after.Add(new KeyValuePair<float, Action>(-delay, a)); }

        /// <summary>Resets the watcher on scene load (new generation, world ready pending).</summary>
        internal static void OnSceneLoaded() { Generation++; State = null; Saving = Loading = false; _pendingWorldReady = true; _after.Clear(); }

        /// <summary>Called once per frame by Core (EarlyUpdate).</summary>
        internal static void Tick()
        {
            var fsm = GameRefs.SaveLoadGame;
            string now = fsm != null && fsm.Fsm.Initialized ? fsm.ActiveStateName : null;
            if (now != State)
            {
                var prev = State;
                State = now;
                if (now != null) Changed(prev, now);
            }
            if (_after.Count > 0 && State == "isPlay")
            {
                float t = Time.unscaledTime;
                for (int i = _after.Count - 1; i >= 0; i--)
                {
                    var kv = _after[i];
                    if (kv.Key < 0) { _after[i] = new KeyValuePair<float, Action>(t - kv.Key, kv.Value); continue; }   // arm
                    if (t < kv.Key) continue;
                    _after.RemoveAt(i);
                    try { kv.Value(); } catch (Exception e) { Log.Error("SaveBus AfterSave callback", e); }
                }
            }
        }

        /// <summary>Handles a SaveLoadGame state change: StateChanged handlers, module hooks and typed save events.</summary>
        static void Changed(string prev, string now)
        {
            Trace.Mark("SaveLoadGame." + now);
            if (Log.L1) Log.Main(null, "SaveLoadGame: " + (prev ?? "-") + " -> " + now);
            var h = StateChanged;
            if (h != null) foreach (StateChange d in h.GetInvocationList()) { try { d(prev, now); } catch (Exception e) { Log.Error("SaveBus.StateChanged handler " + d.Method.DeclaringType + "." + d.Method.Name, e); } }

            switch (now)
            {
                case "SaveGame":
                    Saving = true; _saveSlot = Slot;
                    ModuleHost.Broadcast(ModuleHost.Event.SaveBegin, _saveSlot);
                    if (ModEvents.HasListeners<SaveBegin>()) { var e = new SaveBegin { Slot = _saveSlot, Generation = Generation, Kind = SaveKind.Manual }; ModEvents.Publish(ref e); }
                    break;
                case "LoadGame":
                    Generation++;
                    Loading = true; _pendingWorldReady = true; _after.Clear();
                    ModuleHost.Broadcast(ModuleHost.Event.LoadBegin, Slot);
                    if (ModEvents.HasListeners<LoadBegin>()) { var e = new LoadBegin { Slot = Slot, Generation = Generation }; ModEvents.Publish(ref e); }
                    break;
                case "setSeed":
                case "Start":
                    Generation++;
                    _pendingWorldReady = true;
                    break;
                case "isPlay":
                    if (Saving)
                    {
                        Saving = false; ModuleHost.Broadcast(ModuleHost.Event.SaveEnd, _saveSlot);
                        if (ModEvents.HasListeners<SaveEnd>()) { var e = new SaveEnd { Slot = _saveSlot, Generation = Generation, Kind = SaveKind.Manual, Ok = true }; ModEvents.Publish(ref e); }
                    }
                    if (_pendingWorldReady)
                    {
                        bool fromLoad = Loading;
                        _pendingWorldReady = false; Loading = false; ModuleHost.Broadcast(ModuleHost.Event.WorldReady, Slot);
                        if (ModEvents.HasListeners<WorldReady>()) { var e = new WorldReady { Slot = Slot, Generation = Generation, FromLoad = fromLoad }; ModEvents.Publish(ref e); }
                    }
                    break;
            }
        }
    }
}
