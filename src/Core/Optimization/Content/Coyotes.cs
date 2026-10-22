using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// CT8 (plan §7, research B §3.7): every hit on a Coyote sends FactionCoyotesEnemy to __GameManager__ (weapon CheckFriendly,
    /// PlayerCameraHolder.md:L2947-L2960); FactionRelation_Coyotes (__GameManager__.md:L525-L551) takes the global transition to
    /// CoyotesAttacked (Timer = 600) -> PlayerEnemy, whose action 4 BroadcastAll's FactionCoyotesEnemy again: a full scan of every FSM
    /// plus an FsmEventData per FSM on every hit (5-10 per second with automatic weapons).
    /// The first transition to hostile (from PlayerFriendly/wait) keeps the vanilla BroadcastAll. On a repeat (the event arrived while
    /// already in PlayerEnemy) the stand-in for PlayerEnemy #4 delivers the event only to FSMs it would move to another state: a global
    /// or active-state transition on FactionCoyotesEnemy whose target is not the state they are already in. Those are the receivers
    /// that matter: coyote NPCs re-activated or spawned after the first broadcast restart friendly (PlayerIsEnemy start
    /// PlayerFriendly, e.g. Gunnar.md:L312-L323) and still turn hostile on the next hit, exactly as with vanilla's re-broadcast.
    /// Skipped: receivers already in PlayerEnemy / disable / enemy (re-entering re-applies the same sensor tag lists / collider off)
    /// and FSMs without a handler. The timer reset, texts, OnOff and the Wait restart are untouched (vanilla flow).
    /// </summary>
    public sealed class CoyoteBroadcastFeature : Feature
    {
        public override string PlanId { get { return "CT8"; } }
        public override string Key { get { return "Coyote broadcast"; } }
        public override string Title { get { return "Coyote faction broadcast"; } }
        public override string Category { get { return Categories.Content; } }
        public override int Order { get { return 8; } }
        public override string Help { get { return "Repeated hits on Coyotes no longer re-broadcast the hostility event to every FSM in the game, only to those still friendly."; } }

        internal static bool On;
        readonly UndoLog _undo = new UndoLog();
        static Fsm _coyote;
        static int _repeatFrame = -1;
        internal static long Repeats, Delivered, Firsts;
        const string EventName = "FactionCoyotesEnemy";

        public override void SubscribeHooks() { Hooks.ProcessEvent.Prefix(Module, BeforeEvent); }

        protected override void OnEnable(FeatureMode mode)
        {
            On = true;
            FsmEdits.Watch(this, "FactionRelation_Coyotes", "__GameManager__", Ready);
        }
        protected override void OnDisable() { On = false; _coyote = null; FsmEdits.Unwatch(this); _undo.RevertAll(); }
        public override void OnSceneLoaded() { _coyote = null; _undo.Prune(); }

        void Ready(PlayMakerFSM f)
        {
            var fsm = f.Fsm;
            var se = FsmEdits.Act<SendEvent>(this, fsm, "PlayerEnemy", 4, "FactionRelation_Coyotes/PlayerEnemy#4");
            if (se == null) return;
            if (se.eventTarget == null || se.eventTarget.target != FsmEventTarget.EventTarget.BroadcastAll || se.sendEvent == null || se.sendEvent.Name != EventName
                || (se.delay != null && se.delay.Value > 0f) || se.everyFrame)
            { FsmEdits.Warn(this, "FactionRelation_Coyotes/PlayerEnemy#4", "is not BroadcastAll " + EventName); return; }
            var ca = FsmEdits.State(fsm, "CoyotesAttacked");
            if (ca == null) { FsmEdits.Warn(this, "FactionRelation_Coyotes", "no CoyotesAttacked state"); return; }
            FsmEdits.Replace(_undo, f, FsmEdits.State(fsm, "PlayerEnemy"), 4, new RepeatBroadcast(se));
            _coyote = fsm;
        }

        // Hooks.ProcessEvent prefix (never skips): remember that FactionCoyotesEnemy reached the faction FSM while already hostile.
        static bool BeforeEvent(Fsm fsm, FsmEvent ev, FsmEventData data)
        {
            if (!On || !ReferenceEquals(fsm, _coyote) || ev == null) return true;
            string n = ev.Name;
            if (n == null || n.Length != EventName.Length || n != EventName) return true;
            if (fsm.ActiveStateName == "PlayerEnemy") _repeatFrame = Time.frameCount;
            return true;
        }

        static readonly List<PlayMakerFSM> _targets = new List<PlayMakerFSM>(32);

        sealed class RepeatBroadcast : ProxyAction
        {
            readonly SendEvent _se;
            public RepeatBroadcast(SendEvent se) : base(se) { _se = se; }
            protected override void Enter()
            {
                var fsm = Fsm;
                bool repeat = On && _repeatFrame == Time.frameCount && fsm.PreviousActiveState != null && fsm.PreviousActiveState.Name == "CoyotesAttacked";
                _repeatFrame = -1;
                if (!repeat) { Firsts++; base.Enter(); return; }
                Repeats++;
                Deliver(fsm, _se.sendEvent);
                FinishInner();
            }
        }

        /// <summary>Fsm.BroadcastEvent (Fsm.cs:L2525-L2539) restricted to receivers that change state; same snapshot-then-deliver
        /// order (FsmList order), same EventData save / per-target copy / restore, excludeSelf.</summary>
        static void Deliver(Fsm self, FsmEvent ev)
        {
            string name = ev.Name;
            _targets.Clear();
            var list = PlayMakerFSM.FsmList;
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (f == null) continue;
                var fsm = f.Fsm;
                if (fsm == null || ReferenceEquals(fsm, self)) continue;
                if (WouldMove(fsm, name)) _targets.Add(f);
            }
            if (_targets.Count == 0) return;
            var saved = new FsmEventData(Fsm.EventData);
            Fsm.SetEventDataSentByInfo();
            var source = new FsmEventData(Fsm.EventData);
            for (int i = 0; i < _targets.Count; i++)
            {
                var f = _targets[i];
                if (f == null || f.Fsm == null) continue;
                Fsm.EventData = new FsmEventData(source);
                f.Fsm.ProcessEvent(ev);
                Delivered++;
            }
            Fsm.EventData = saved;
            _targets.Clear();
        }

        static bool WouldMove(Fsm fsm, string name)
        {
            if (!fsm.Started) return HasHandler(fsm, name);         // vanilla's ProcessEvent would also Start() it
            var active = fsm.ActiveState;
            if (active == null) return false;
            var g = fsm.GlobalTransitions;
            if (g != null) for (int i = 0; i < g.Length; i++) if (g[i].EventName == name && g[i].ToState != active.Name) return true;
            var t = active.Transitions;
            if (t != null) for (int i = 0; i < t.Length; i++) if (t[i].EventName == name) return true;
            return false;
        }

        static bool HasHandler(Fsm fsm, string name)
        {
            var g = fsm.GlobalTransitions;
            if (g != null) for (int i = 0; i < g.Length; i++) if (g[i].EventName == name) return true;
            return false;
        }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("First broadcasts (vanilla) / repeats targeted", Firsts + " / " + Repeats);
            l.Value("Repeat deliveries", Delivered.ToString());
        }
    }
}
