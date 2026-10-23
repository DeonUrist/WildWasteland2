using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Bootstrap;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    internal sealed partial class Plugin : IQuestApi
    {
        internal string DataDir;
        QuestsModule _saveOwner;
        int _generation;
        string _sessionSlot;
        bool _newWorld;
        int _aimFrame = -1, _aimComputed;
        string _resumeText, _resumeSlot; int _resumeWorld;
        string[] _resumeCrew;
        readonly List<QuestRow> _rows = new List<QuestRow>();
        readonly Dictionary<string, int> _emptyScans = new Dictionary<string, int>();
        /// <summary>Whether the quest engine has restored its current world.</summary>
        public bool Ready { get { return ready; } }
        /// <summary>Whether tracker presentation is enabled.</summary>
        public bool TrackerEnabled { get { return showTracker.Value; } }
        /// <summary>Whether the journal is currently open.</summary>
        public bool JournalOpen { get { return showLog; } }
        /// <summary>Current cached journal text.</summary>
        public string JournalText { get { return logText; } }
        /// <summary>Current journal hotkey name.</summary>
        public string JournalKey { get { return logKey.Value.ToString(); } }
        /// <summary>Total completed quests in the current journal.</summary>
        public int Completed { get { return book.TotalDone; } }
        /// <summary>Typed tracker rows for HUD consumers.</summary>
        public IList<QuestRow> Rows { get { return _rows; } }
        /// <summary>Current conversation state provider.</summary>
        public IQuestConversation Conversation { get { return bridge; } }
        bool TrackerStyled { get { return ModApi.Get<IQuestPresentation>() != null && ModApi.Get<IQuestPresentation>().Tracker; } }
        bool JournalStyled { get { return ModApi.Get<IQuestPresentation>() != null && ModApi.Get<IQuestPresentation>().Journal; } }
        /// <summary>Whether HUD owns the tracker presentation.</summary>
        internal bool DialogueStyled { get { return ModApi.Get<IQuestPresentation>() != null && ModApi.Get<IQuestPresentation>().Conversation; } }
        /// <summary>Whether any fallback quest presentation is currently visible.</summary>
        internal bool WantsGui { get { return ready && (bridge.NativeOpen || bridge.Prompt.Length > 0 || showLog || toasts.Count > 0 || (!TrackerStyled && showTracker.Value && trackerLines.Count > 0)); } }

        /// <summary>Loads content and registers typed state, input and native observers.</summary>
        internal void Start(QuestsModule owner, HireSettings hiring, VoiceSettings voices)
        {
            ModApi.Provide<IQuestDiagnosticSource>(owner, new QuestDiagnosticSource());
            _saveOwner = owner;
            ModApi.Provide<IQuestFallbackData>(owner, this);
            I = this; logStarted = true; logged.Clear(); DataDir = owner.Dir; _generation++;
            foreach (var file in QuestContent.Import(DataDir)) Core.Log.Always(null,"[Quests] imported missing content: " + file);
            ApplyTakeoverSettings(); ApplyFactionSettings(); ParseGenTypes();
            Game.MinLiquidFraction = minLiquid.Value;
            factionsPath = Path.Combine(DataDir, "factions.txt");
            Factions.Refresh(factionsPath, Warn); defs = QuestFile.Load(Path.Combine(DataDir, "quests.txt"), Warn);
            Hire.Init(hiring, book, AddToast, ChangeRep); Voice.Init(voices, DataDir);
            Takeover.Log = Info;
            Core.Hooks.CreateObjectEnter.Postfix(owner, Death);
            Core.Hooks.GetButtonDownUpdate.Prefix(owner, BlockButtonDown);
            Core.Hooks.GetButtonDo.Prefix(owner, BlockButton);
            Core.Hooks.GetButtonUpUpdate.Prefix(owner, BlockButtonUp);
            Core.Hooks.GetMouseButtonDownUpdate.Prefix(owner, BlockMouse);
            Core.Hooks.MouseLookDo.Prefix(owner, BlockLook);
            Core.Log.Always(null,"[Quests] Conversation mouse-look guard installed.");
            Hotkeys.Register(owner, logKey, ToggleJournal, CanJournal);
            Hotkeys.Register(owner, hiring.dismissKey, DismissFollower, CanDismiss);
            Hotkeys.Register(owner, QuestsModule.InteractKey, OpenInteraction, CanInteract);
            bridge.InstallOptional(owner);
            Core.Log.Always(null,"[Quests] Native Quests 1.8.2 ready: " + defs.Count + " authored quests; AQ1 / ApocalypterQuests retained; Talk optional.");
        }
        /// <summary>Quests warning (always written, rate-limited); replaces QuestLogger.LogWarning.</summary>
        internal static void Warn(string text) { Core.Log.Warn(null,"[Quests] " + text); }
        /// <summary>Quests level-1 line (written while the verbose level is 1 or more); replaces QuestLogger.LogInfo.</summary>
        internal static void Info(string text) { Core.Log.Main(null,"[Quests] " + text); }
        /// <summary>Quests error (always written, rate-limited); replaces QuestLogger.LogError.</summary>
        internal static void Error(string text) { Core.Log.Error(null,"[Quests] " + text); }
        /// <summary>Applies current faction, takeover and generation tuning.</summary>
        internal void ApplyConfig() { ApplyTakeoverSettings(); ApplyFactionSettings(); ParseGenTypes(); Game.MinLiquidFraction = minLiquid.Value; }
        /// <summary>Observes a native death action for quest objective accounting.</summary>
        static void Death(CreateObject action)
        {
            if (I == null || !I.ready || action.Fsm == null || action.Fsm.Name != "Health" || action.Fsm.ActiveStateName != "spawn") return;
            var go = action.Owner; if (go == null || go == GameRefs.Player) return;
            string dead = action.storeObject != null && action.storeObject.Value != null ? Core.Names.Base(action.storeObject.Value.name) : "";
            I.OnEnemyKilled(Core.Names.Base(go.name), dead, go.transform.position, go.tag, go);
        }
        /// <summary>Blocks native button-down actions while the quest conversation owns input.</summary>
        static bool BlockButtonDown(GetButtonDown action) { return I == null || !I.bridge.NativeOpen; }
        /// <summary>Blocks held native buttons while the quest conversation owns input.</summary>
        static bool BlockButton(GetButton action) { return I == null || !I.bridge.NativeOpen; }
        /// <summary>Blocks native button-up actions while the quest conversation owns input.</summary>
        static bool BlockButtonUp(GetButtonUp action) { return I == null || !I.bridge.NativeOpen; }
        /// <summary>Blocks native mouse actions while the quest conversation owns input.</summary>
        static bool BlockMouse(GetMouseButtonDown action) { return I == null || !I.bridge.NativeOpen; }
        /// <summary>Blocks player mouse-look during a native quest conversation.</summary>
        static bool BlockLook(MouseLook __instance) { return I == null || !I.bridge.NativeOpen || __instance.Owner != GameRefs.Player; }
        /// <summary>Tests whether the journal can open in current gameplay.</summary>
        bool CanJournal() { return ready && GameRefs.InGame && !bridge.TalkingNow; }
        /// <summary>Tests whether the current companion can be dismissed.</summary>
        bool CanDismiss() { return ready && Hire.Active && GameRefs.InGame; }
        /// <summary>Toggles the journal only while gameplay is unpaused.</summary>
        void ToggleJournal() { if (!GamePaused()) showLog = !showLog; }

        /// <summary>Releases transient quest session state before replacing the world.</summary>
        internal void SceneReset()
        {
            _generation++; ready = false; _sessionSlot = null; _newWorld = false;
            _resumeText = _resumeSlot = null; _resumeCrew = null;
            bridge.Close(); ResetSession(); deferred.Clear(); showLog = false; toasts.Clear(); _rows.Clear();
            Takeover.Shutdown(); Factions.Overrides.Clear(); CrewHistory.Clear(); if (ModApi.Get<IQuestFollowerBody>() != null) ModApi.Get<IQuestFollowerBody>().Release(); Game.ResetWorld();
        }
        /// <summary>Captures or restores quest state at canonical native lifecycle transitions.</summary>
        internal void StateChanged(string previous, string state)
        {
            if (state == "LoadGame" || state == "setSeed" || state == "Start")
            {
                bool fresh = state != "LoadGame";
                SceneReset(); _newWorld = fresh; CrewHistory.Begin(fresh); book.Clear();
            }
            if (state == "isPlay" && !ready)
            {
                _sessionSlot = SaveBus.Slot; if (_sessionSlot == null) return;
                ready = true; CrewHistory.Load(_newWorld);
                bool resume = !_newWorld && _resumeText != null && _resumeSlot == _sessionSlot && _resumeWorld == SaveBus.Generation;
                if (resume && book.Deserialize(_resumeText)) { RestoreBook(); CrewHistory.Restore(_resumeCrew); saveBlocked = false; }
                else if (!_newWorld) LoadBook(); else { saveBlocked = false; book.Clear(); }
                _resumeText = _resumeSlot = null; _resumeCrew = null;
                _newWorld = false; RefreshLines(Core.PlayerState.Transform); SnapshotRows();
                Core.Log.Always(null,"[Quests] World ready: " + _sessionSlot + ", " + book.Active.Count + " active quests" + (saveBlocked ? "; unreadable data protected" : ""));
            }
            // Capture before native serialization; the shared transaction verifies both keys before publication.
            if (state == "SaveGame" && ready && _sessionSlot == SaveBus.Slot)
            {
                SaveBook(); CrewHistory.Save(_saveOwner, _sessionSlot);
            }
        }
        /// <summary>Copies current faction overrides and takeover walks into the journal.</summary>
        void CaptureBook()
        {
            book.Overrides.Clear(); foreach (var item in Factions.Overrides) book.Overrides[item.Key] = item.Value;
            book.Walks.Clear(); book.Walks.AddRange(Takeover.ActiveWalks());
        }
        /// <summary>Restores current journal overrides and companion contract state.</summary>
        void RestoreBook()
        {
            Factions.Overrides.Clear(); foreach (var item in book.Overrides) Factions.Overrides[item.Key] = item.Value;
            Takeover.RestoreWalks(book.Walks); Hire.Reset();
        }
        /// <summary>Copies cached tracker text and stable quest colors into typed rows.</summary>
        void SnapshotRows()
        {
            _rows.Clear();
            for (int i = 0; i < trackerLines.Count && i < book.Active.Count; i++)
                _rows.Add(new QuestRow { Id = book.Active[i].Id, Text = trackerLines[i], Color = MapLink.QuestColor(book.Active[i].Id) });
        }
        /// <summary>Tests the explicit return-goods delivery state.</summary>
        internal static bool Returning(Quest q) { return q.Type == QType.Deliver && q.Place == "WW:return-empty-delivery"; }
        /// <summary>Finds a live friendly delivery recipient near the requested position.</summary>
        bool DeliveryRecipient(Quest q, Vector3 position, float radius, bool returning)
        {
            foreach (var f in QuestWorld.People)
            {
                if (f == null || f.FsmName != "Health" || f.ActiveStateName != "health" || !f.gameObject.activeInHierarchy) continue;
                var go = f.gameObject;
                if (returning && go.name != q.GiverId) continue;
                var hp = f.FsmVariables.GetFsmFloat("Health"); if (hp == null || hp.Value <= 0) continue;
                if (Core.WorldGeometry.HorizontalDistance(f.transform.position, position) > radius || Game.IsPlayerEnemy(go) || !Person(Core.Names.Base(go.name))) continue;
                var collider = go.GetComponent<Collider>(); if (collider == null || !collider.enabled) continue;
                var vehicles = ModApi.Get<IFollowerVehicles>(); if (vehicles != null && vehicles.IsSeated(go)) continue;
                if (!Factions.Friendly(Factions.ByKey(q.Home), Factions.OfObject(go.tag, Core.Names.Base(go.name), go.transform.position))) continue;
                if (returning) q.GiverPos = Core.Origin.World(f.transform.position);
                return true;
            }
            return false;
        }
        /// <summary>Closes conversations and releases quest-owned runtime resources.</summary>
        internal void Stop()
        {
            if (ready && !saveBlocked && _sessionSlot != null && _sessionSlot == SaveBus.Slot)
            { CaptureBook(); _resumeText = book.Serialize(); _resumeSlot = _sessionSlot; _resumeWorld = SaveBus.Generation; _resumeCrew = CrewHistory.Snapshot(); }
            _generation++; ready = false; bridge.Close(); bridge.Uninstall(); ResetSession();
            Hire.Shutdown(); Voice.Shutdown(); if (ModApi.Get<IQuestFollowerBody>() != null) ModApi.Get<IQuestFollowerBody>().Release(); Takeover.Shutdown(); Factions.Reset();
            Game.ResetWorld(); CrewHistory.Clear(); _rows.Clear(); toasts.Clear(); deferred.Clear(); I = null;
        }
    }
}
