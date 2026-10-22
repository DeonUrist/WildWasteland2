using HutongGames.PlayMaker;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// Shared by PM5 (BecameVisible/Invisible) and PM6 (trigger/collision proxies): decides whether a SYSTEM event that the
    /// game sends to an FSM through <c>Fsm.Event(FsmEvent)</c> would change nothing, so the expensive dispatch
    /// (Event -> ProcessEvent -> Core.Hooks dispatcher + other mods' ProcessEvent patches -> transition scan) can be replaced by
    /// its only remaining side effect, <c>Fsm.SetEventDataSentByInfo()</c>.
    ///
    /// Vanilla (PlayMaker Fsm.cs): Event(FsmEvent e) -> Event(EventTarget, e) (Fsm.cs:2495) -> SetEventDataSentByInfo();
    /// target null -> Self -> ProcessEvent(e) (2429-2485) -> [Active? Started? (else Start()) switching? state OnEvent?
    /// global transitions, state transitions, OnOutputEvent] (2322-2383); then, only when the sender is not the executing
    /// FSM, UpdateStateChanges() (2662: pending switch, ResetLoopCount on every state, EventTarget = null).
    /// ProcessEvent changes nothing when: the FSM is not Active; or it is switching (returns before the transition scan);
    /// or it is started, its state does not handle OnEvent, no global/state transition uses the event and no output-event
    /// listener exists. A not-started active FSM would be STARTED by ProcessEvent -> never skipped.
    /// Not covered by the skip (documented in the report): Harmony prefixes on Fsm.ProcessEvent (WW Core.Hooks subscribers,
    /// Apocasaver) do not see these unhandled system events any more.
    /// </summary>
    internal static class FsmEventSkip
    {
        /// <summary>True when Event(e) on this FSM would only update EventData's sender info (and, outside the FSM's own
        /// execution, reset loop counts / EventTarget). Never true when anything else could happen.</summary>
        internal static bool Unhandled(Fsm fsm, FsmEvent e)
        {
            if (fsm == null || e == null) return false;
            var target = fsm.EventTarget;
            if (target != null && target.target != FsmEventTarget.EventTarget.Self) return false;   // event goes elsewhere
            if (fsm.Active)
            {
                if (!fsm.Started) return false;                       // ProcessEvent would Start() it
                if (!fsm.IsSwitchingState)
                {
                    var st = fsm.ActiveState;
                    if (st == null || st.HandlesOnEvent) return false;
                    var g = fsm.GlobalTransitions;
                    if (g != null) for (int i = 0; i < g.Length; i++) if (g[i].FsmEvent == e) return false;
                    var t = st.Transitions;
                    if (t != null) for (int i = 0; i < t.Length; i++) if (t[i].FsmEvent == e) return false;
                    if (fsm.OnOutputEvent != null) return false;
                }
            }
            // the trailing UpdateStateChanges (only when the sender is not executing) must not have a switch to perform
            if (!ReferenceEquals(FsmExecutionStack.ExecutingFsm, fsm) && fsm.IsSwitchingState && !Fsm.HitBreakpoint) return false;
            return true;
        }

        /// <summary>Exactly what Event(e) does when <see cref="Unhandled"/> said true.</summary>
        internal static void Apply(Fsm fsm)
        {
            Fsm.SetEventDataSentByInfo();
            if (!ReferenceEquals(FsmExecutionStack.ExecutingFsm, fsm))
            {
                // UpdateStateChanges() without a pending switch: reset loop counts, clear EventTarget
                var states = fsm.States;
                for (int i = 0; i < states.Length; i++) states[i].ResetLoopCount();
                fsm.EventTarget = null;
            }
        }
    }
}
