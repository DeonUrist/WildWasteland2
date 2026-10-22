using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Quests
{
    /// <summary>
    /// Owns quest content, hire contracts and journal persistence; presentation and companion body work use typed services.
    /// </summary>
    public sealed partial class QuestsModule : ModuleBase
    {
        Plugin _engine;
        readonly HireSettings _hire = new HireSettings(); readonly VoiceSettings _voice = new VoiceSettings();
        internal static ConfigEntry<Key> InteractKey;
        bool _checked;
        /// <summary>Canonical quest module identity.</summary>
        public override string Id { get { return "Quests"; } }
        /// <summary>Name shown in the module shell.</summary>
        public override string DisplayName { get { return "Quests & hiring"; } }
        /// <summary>Standalone quest plugin conflict identity.</summary>
        public override string[] ConflictsWith { get { return new[] { QuestContent.LegacyGuid }; } }
        /// <summary>Binds engine, companion and voice configuration plus interaction input.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            _engine = new Plugin(); _engine.Bind(cfg); _hire.Bind(cfg); _voice.Bind(cfg);
            InteractKey = cfg.Hotkey("Quests: talk", Key.F, "Talk to the friendly NPC under your aim. Apocatalker owns its own interaction when installed.");
        }
        /// <summary>Schedules quest progress, world projections and presentation updates.</summary>
        public override void Init()
        {
            _checked = false;
            ModApi.Provide<IFollowers>(this,new Hire.FollowersApi());
            Scheduler.EveryFrame(this, "Quests.Frame", Frame);
            Scheduler.EveryFrame(this, "Quests.Poses", Late, Phase.LateUpdateLast);
            Scheduler.Interval(this, "Quests.Progress", .5f, Progress);
            Scheduler.Interval(this, "Quests.Camps", .5f, Camps);
            Scheduler.Interval(this, "Quests.Givers", .5f, Givers);
            Scheduler.Interval(this, "Quests.Map", .5f, Map);
            Scheduler.Interval(this, "Quests.Reputation", .5f, Reputation);
            Scheduler.Interval(this, "Quests.Interaction", .2f, Interaction);
            Scheduler.Sliced(this, "Quests.World cache", 500, WorldStep);
        }
        /// <summary>Starts the engine after chainloading and advances active quest work.</summary>
        void Frame()
        {
            if (!_checked)
            {
                _checked = true;
                // No standalone load-order dependency: inspect after the chainloader has completed.
                if (!ModuleHost.DeferredInit(this, StartEngine)) return;
            }
            if (Plugin.I != _engine) return;
            if (_engine.Ready && !QuestWorld.Ready) return;
            _engine.Frame(); _engine.InteractionFrame(); if (ModApi.Get<IQuestFollowerBody>() != null) ModApi.Get<IQuestFollowerBody>().Tick();
        }
        /// <summary>Starts quest content and publishes real typed state providers.</summary>
        void StartEngine()
        {
            _engine.Start(this, _hire, _voice); ModApi.Provide<IQuestApi>(this, _engine); ModApi.Provide<IQuestHireState>(this, new Hire.StateApi());
            SaveBus.StateChanged += StateChanged; ModEvents.Subscribe<Core.Events.CrewBailedOut>(this, CrewBailed);
            QuestWorld.Begin(this); if (SaveBus.State != null) StateChanged(null, SaveBus.State);
        }
        /// <summary>Runs companion pose finalization after the quest late frame.</summary>
        void Late() { if (Plugin.I == _engine) { _engine.LateFrame(); if (ModApi.Get<IQuestFollowerBody>() != null) ModApi.Get<IQuestFollowerBody>().Late(); } }
        bool Live { get { return Plugin.I == _engine && _engine.Ready && QuestWorld.Ready && GameRefs.InGame; } }
        /// <summary>Advances objective completion on the quest cadence.</summary>
        void Progress() { if (Live) _engine.Tick(); }
        /// <summary>Advances current camp takeover outcomes near the player.</summary>
        void Camps() { if (Live && GameRefs.Player != null) Takeover.NeutralizeTick(GameRefs.Player.transform.position); }
        /// <summary>Restores eligible quest givers near the player.</summary>
        void Givers() { if (Live && GameRefs.Player != null) Takeover.GiverTick(_engine.book, GameRefs.Player.transform.position); }
        /// <summary>Synchronizes quest map and compass presentation.</summary>
        void Map() { if (Live) _engine.SyncMapMarkers(); MapLink.Reconcile(); }
        /// <summary>Advances faction reputation recovery.</summary>
        void Reputation() { if (Live) _engine.ReputationTick(); }
        /// <summary>Refreshes the current aimed quest interaction.</summary>
        void Interaction() { if (Live) _engine.FindInteraction(); }
        /// <summary>Processes the next quest projection slice over shared indexes.</summary>
        bool WorldStep() { return Plugin.I == _engine && QuestWorld.Step(); }
        /// <summary>Forwards the canonical native save/load state transition.</summary>
        void StateChanged(string previous, string state) { _engine.StateChanged(previous, state); }
        /// <summary>Clears quest world projections when the primary scene changes.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        { if (mode != LoadSceneMode.Additive && Plugin.I == _engine) { _engine.SceneReset(); QuestWorld.Begin(this); } }
        /// <summary>Releases engine state, subscriptions, markers and companion body ownership.</summary>
        public override void Shutdown()
        {
            SaveBus.StateChanged -= StateChanged; ModEvents.Unsubscribe<Core.Events.CrewBailedOut>(this, CrewBailed);
            ModApi.Withdraw<IQuestApi>(this); ModApi.Withdraw<IQuestHireState>(this);
            if (Plugin.I == _engine) _engine.Stop();
            QuestWorld.Clear(); MapLink.Reset(); if (ModApi.Get<IQuestFollowerBody>() != null) ModApi.Get<IQuestFollowerBody>().Release();
        }
        /// <summary>Remembers the actual on-foot actor emitted by the crew producer without depending on Patrols types.</summary>
        static void CrewBailed(ref Core.Events.CrewBailedOut change)
        { CrewHistory.Bailed(Entities.Find(change.Vehicle), Entities.Find(change.Passenger), Entities.Find(change.Npc)); }
        /// <summary>Contributes quest authoring and tuning pages to Tools.</summary>
        public override IEnumerable<ToolPage> ToolPages()
        {
            return new ToolPage[] { new ConfigPage(this, ToolGroups.General, "Quests & hiring", Config.Tools, "Quests / *")
                { Intro = "Quest, faction, hire and voice tuning. Player options and J/F/Delete bindings are in Game settings. Custom quests/factions and voices live in the Quests plugin folder." } };
        }
    }
}
