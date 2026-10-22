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
    /// Quests module entry (Wave A: stays the registered entry; B18). Per-target lifecycle parts live in partial files:
    /// HUD (HUD/_fromQuests/QuestsModule.Hud.cs: the IMGUI windows job) and NPCAI (NPCAI/Behaviour/_fromQuests/QuestsModule.Npcai.cs:
    /// the follower body driver ticks).
    /// </summary>
    public sealed partial class QuestsModule : ModuleBase
    {
        Plugin _engine;
        readonly HireSettings _hire = new HireSettings(); readonly VoiceSettings _voice = new VoiceSettings();
        internal static ConfigEntry<Key> InteractKey;
        bool _checked;
        public override string Id { get { return "Quests"; } }
        public override string DisplayName { get { return "Quests & hiring"; } }
        public override string[] ConflictsWith { get { return new[] { QuestContent.LegacyGuid }; } }
        public override void BindConfig(ModuleConfig cfg)
        {
            _engine = new Plugin(); _engine.Bind(cfg); _hire.Bind(cfg); _voice.Bind(cfg);
            InteractKey = cfg.Hotkey("Quests: talk", Key.F, "Talk to the friendly NPC under your aim. Apocatalker owns its own interaction when installed.");
        }
        public override void Init()
        {
            _checked = false;
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
            _engine.Frame(); _engine.InteractionFrame(); FrameNpcai();
        }
        void StartEngine()
        {
            _engine.Start(this, _hire, _voice); Services.Quests = _engine;
            SaveBus.StateChanged += StateChanged; Services.CrewBailedOut += CrewHistory.Bailed;
            QuestWorld.Begin(this); if (SaveBus.State != null) StateChanged(null, SaveBus.State);
        }
        void Late() { if (Plugin.I == _engine) { _engine.LateFrame(); LateNpcai(); } }
        bool Live { get { return Plugin.I == _engine && _engine.Ready && QuestWorld.Ready && GameRefs.InGame; } }
        void Progress() { if (Live) _engine.Tick(); }
        void Camps() { if (Live && GameRefs.Player != null) Takeover.NeutralizeTick(GameRefs.Player.transform.position); }
        void Givers() { if (Live && GameRefs.Player != null) Takeover.GiverTick(_engine.book, GameRefs.Player.transform.position); }
        void Map() { if (Live) _engine.SyncMapMarkers(); MapLink.Reconcile(); }
        void Reputation() { if (Live) _engine.ReputationTick(); }
        void Interaction() { if (Live) _engine.FindInteraction(); }
        bool WorldStep() { return Plugin.I == _engine && QuestWorld.Step(); }
        void StateChanged(string previous, string state) { _engine.StateChanged(previous, state); }
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        { if (Plugin.I == _engine) { _engine.SceneReset(); QuestWorld.Begin(this); } }
        public override void Shutdown()
        {
            SaveBus.StateChanged -= StateChanged; Services.CrewBailedOut -= CrewHistory.Bailed;
            if (ReferenceEquals(Services.Quests, _engine)) Services.Quests = null;
            if (Plugin.I == _engine) _engine.Stop();
            QuestWorld.Clear(); MapLink.Reset(); ShutdownNpcai();
        }
        /// <summary>NPCAI part: per-frame follower body driver tick (after the engine frame).</summary>
        partial void FrameNpcai();
        /// <summary>NPCAI part: late follower pose (after the seat lock).</summary>
        partial void LateNpcai();
        /// <summary>NPCAI part: releases the follower body driver.</summary>
        partial void ShutdownNpcai();
        public override IEnumerable<ToolPage> ToolPages()
        {
            return new ToolPage[] { new ConfigPage(this, ToolGroups.General, "Quests & hiring", Config.Tools, "Quests / *")
                { Intro = "Quest, faction, hire and voice tuning. Player options and J/F/Delete bindings are in Game settings. Custom quests/factions and voices live in the Quests plugin folder." } };
        }
    }
}
