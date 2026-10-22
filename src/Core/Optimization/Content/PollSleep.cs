using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// Exact sleep for FSMs that wait in a "poll" state (research B §2: almost all polling is an everyFrame compare used as "wait
    /// until"). A watched FSM is paused (Fsm.ManualUpdate, see <see cref="FsmPause"/>) while every unfinished action of its active
    /// state is one of:
    ///  - a getter whose per-frame work only writes FSM variables (GetSpeed, GetDistance, GetFsmFloat/Int/Bool, GetChildCountAdvanced,
    ///    FloatClamp, IntClamp, GetChildCount): the sleep job calls the action's own OnUpdate each frame, so the variables it writes
    ///    are exactly what vanilla would write;
    ///  - a tester that fires an event (BoolTest, IntCompare, FloatCompare, GameObjectHasChildren, GameObjectCompareTag): the job
    ///    evaluates the same condition read-only; if the event it would fire leads to a transition the FSM is woken and its own
    ///    Update (later this frame: the job runs in EarlyUpdate, before any FSM) fires it, identical to vanilla;
    ///  - a callback-only action (TriggerEvent, TriggerEventByLayer, CollisionEvent, CollisionEventPlus): callbacks reach a paused FSM.
    /// Any state entry (Core Hooks.EnterState postfix), a pending delayed event, a disabled/inactive component or an unsupported
    /// active action wakes it / keeps it awake. Testers whose storeResult is used never sleep (the stored bool would go stale).
    /// What changes: the Unity Update call of a paused FSM is a single flag test instead of Fsm.Update (push/pop, delayed events,
    /// UpdateState, UpdateStateChanges over all states); FsmState.StateTime does not advance while asleep (nothing in the game reads it).
    /// Shared by CT3 (hinge child-count polls), CT5 (item/vehicle wait-until polls) and CT11 (option toggles); each feature
    /// registers its FSM names and owns the sleepers it registered.
    /// </summary>
    internal static class PollSleep
    {
        internal sealed class Sleeper
        {
            internal PlayMakerFSM Comp; internal Fsm Fsm; internal Feature Owner;
            internal bool Asleep;
            internal FsmState PlanState; internal int PlanCount = -1; internal FsmStateAction[] Plan; internal int PlanLen;
            internal int Index; internal bool Failed;
        }

        static readonly List<Sleeper> _all = new List<Sleeper>(256);
        static readonly Dictionary<Fsm, Sleeper> _byFsm = new Dictionary<Fsm, Sleeper>(RefComparer<Fsm>.Instance);
        static Job _job;
        static int _users;
        internal static long WakeCount, SleepCount, GuardCalls;
        internal static int Asleep;

        internal static void Acquire(Feature f)
        {
            if (_users++ == 0)
            {
                if (_job == null || !IsAlive(_job)) _job = Scheduler.EveryFrame(f.Module, "Performance.PollSleep", Tick, Phase.EarlyUpdate);
                _job.Enabled = true;
            }
        }

        static bool IsAlive(Job j) { foreach (var x in Scheduler.AllJobs()) if (x == j) return true; return false; }

        /// <summary>Releases everything <paramref name="owner"/> registered (wakes its sleepers).</summary>
        internal static void Release(Feature owner)
        {
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var s = _all[i];
                if (s.Owner != owner) continue;
                Wake(s);
                RemoveAt(i);
            }
            if (_users > 0 && --_users == 0 && _job != null) _job.Enabled = false;
        }

        /// <summary>Called from SubscribeHooks of each user feature (Core removes the subscriptions when the module stops). Several
        /// subscriptions of the same handler are harmless: the first call wakes, later ones find nothing asleep.</summary>
        internal static void Hook(PerformanceModule m) { Hooks.EnterState.Postfix(m, AfterEnterState); }

        internal static void Register(Feature owner, PlayMakerFSM comp)
        {
            var fsm = comp.Fsm;
            if (fsm == null || _byFsm.ContainsKey(fsm)) return;
            var s = new Sleeper { Comp = comp, Fsm = fsm, Owner = owner, Index = _all.Count };
            _all.Add(s); _byFsm[fsm] = s;
        }

        static void RemoveAt(int i)
        {
            var s = _all[i];
            _byFsm.Remove(s.Fsm);
            int last = _all.Count - 1;
            if (i != last) { _all[i] = _all[last]; _all[i].Index = i; }
            _all.RemoveAt(last);
        }

        static void Wake(Sleeper s)
        {
            if (!s.Asleep) return;
            s.Asleep = false; Asleep--; WakeCount++;
            FsmPause.Clear(s.Fsm, FsmPause.PollSleep);
        }

        // Hooks.EnterState postfix: any state entry of a sleeping FSM (event, restart on enable, SetState) wakes it.
        static void AfterEnterState(Fsm fsm, FsmState st)
        {
            if (Asleep == 0) return;
            Sleeper s;
            if (_byFsm.TryGetValue(fsm, out s) && s.Asleep) { Wake(s); s.PlanCount = -1; }
        }

        internal static void OnSceneLoaded()
        {
            for (int i = _all.Count - 1; i >= 0; i--) if (_all[i].Comp == null) { if (_all[i].Asleep) { _all[i].Asleep = false; Asleep--; } RemoveAt(i); }
        }

        static int _cursorPrune;
        static void Tick()
        {
            if (_all.Count == 0) return;
            // prune destroyed sleepers a few per frame
            for (int k = 0; k < 4 && _all.Count > 0; k++)
            {
                if (_cursorPrune >= _all.Count) _cursorPrune = 0;
                var p = _all[_cursorPrune];
                if (p.Comp == null) { if (p.Asleep) { p.Asleep = false; Asleep--; } RemoveAt(_cursorPrune); }
                else _cursorPrune++;
            }
            for (int i = 0; i < _all.Count; i++)
            {
                var s = _all[i];
                try { Step(s); }
                catch (Exception e) { Wake(s); s.Failed = true; Log.Error("[Performance] poll sleep " + s.Fsm.Name, e); }
            }
        }

        static void Step(Sleeper s)
        {
            var fsm = s.Fsm;
            if (s.Asleep)
            {
                if (!FsmPause.Held(fsm, FsmPause.PollSleep)) { s.Asleep = false; Asleep--; s.PlanCount = -1; return; }   // woken by someone else
                if (fsm.Finished || fsm.ActiveState != s.PlanState || s.PlanState.ActiveActions.Count != s.PlanCount || fsm.DelayedEvents.Count != 0)
                { Wake(s); s.PlanCount = -1; return; }
                if (!s.Comp.isActiveAndEnabled) return;          // Unity does not update it either; restart on enable enters a state -> hook wakes
                GuardCalls++;
                if (Guard(s)) { Wake(s); }
                return;
            }
            if (s.Failed) return;                                // threw once: stays vanilla
            if (fsm.Finished || !fsm.Started || fsm.IsSwitchingState || fsm.ManualUpdate) return;
            var st = fsm.ActiveState;
            if (st == null) return;
            var active = st.ActiveActions;
            if (st != s.PlanState || active.Count != s.PlanCount) { BuildPlan(s, st); }
            if (s.Plan == null || fsm.DelayedEvents.Count != 0 || !s.Comp.isActiveAndEnabled) return;
            // this frame's update is evaluated here; only if nothing would fire does the FSM skip it
            GuardCalls++;
            if (Guard(s)) return;
            if (!FsmPause.Set(fsm, FsmPause.PollSleep)) return;
            s.Asleep = true; Asleep++; SleepCount++;
        }

        static void BuildPlan(Sleeper s, FsmState st)
        {
            s.PlanState = st;
            var active = st.ActiveActions;
            s.PlanCount = active.Count;
            if (active.Count == 0) { s.Plan = null; return; }      // nothing per frame: FS1's business, not a poll
            if (s.Plan == null || s.Plan.Length < active.Count) s.Plan = new FsmStateAction[Math.Max(4, active.Count)];
            for (int i = 0; i < active.Count; i++)
            {
                var a = active[i];
                if (a == null || a.Finished || !Supported(a)) { s.Plan = null; return; }
                s.Plan[i] = a;
            }
            s.PlanLen = active.Count;
        }

        static bool Supported(FsmStateAction a)
        {
            // callbacks only
            if (a is TriggerEvent || a is TriggerEventByLayer || a is CollisionEvent || a is CollisionEventPlus) return true;
            // getters (their OnUpdate only writes variables)
            if (a is GetSpeed || a is GetDistance || a is GetFsmFloat || a is GetFsmInt || a is GetFsmBool || a is FloatClamp || a is IntClamp) return true;
            var gc = a as GetChildCountAdvanced;
            if (gc != null) return gc.updateType == FsmStateActionAdvanced.FrameUpdateSelector.OnUpdate;
            // testers (fire events; storeResult must be unused)
            if (a is BoolTest || a is IntCompare || a is FloatCompare) return true;
            var hc = a as GameObjectHasChildren;
            if (hc != null) return hc.storeResult == null || hc.storeResult.IsNone;
            var ct = a as GameObjectCompareTag;
            if (ct != null) return ct.storeResult == null || ct.storeResult.IsNone;
            return false;
        }

        /// <summary>One frame of the sleeping state: run getters (same writes as vanilla), evaluate testers read-only. True = wake.</summary>
        static bool Guard(Sleeper s)
        {
            var fsm = s.Fsm;
            var plan = s.Plan;
            for (int i = 0; i < s.PlanLen; i++)
            {
                var a = plan[i];
                FsmEvent ev = null;
                var bt = a as BoolTest;
                if (bt != null) { ev = bt.boolVariable.Value ? bt.isTrue : bt.isFalse; if (Handled(fsm, ev)) return true; continue; }
                var ic = a as IntCompare;
                if (ic != null)
                {
                    int x = ic.integer1.Value, y = ic.integer2.Value;
                    ev = x == y ? ic.equal : x < y ? ic.lessThan : ic.greaterThan;
                    if (Handled(fsm, ev)) return true; continue;
                }
                var fc = a as FloatCompare;
                if (fc != null)
                {
                    float x = fc.float1.Value, y = fc.float2.Value;
                    if (Mathf.Abs(x - y) <= fc.tolerance.Value) ev = fc.equal;
                    else if (x < y) ev = fc.lessThan;
                    else if (x > y) ev = fc.greaterThan;
                    if (Handled(fsm, ev)) return true; continue;
                }
                var hc = a as GameObjectHasChildren;
                if (hc != null)
                {
                    var go = fsm.GetOwnerDefaultTarget(hc.gameObject);
                    if (go == null) continue;                     // vanilla: no event
                    ev = go.transform.childCount > 0 ? hc.trueEvent : hc.falseEvent;
                    if (Handled(fsm, ev)) return true; continue;
                }
                var ct = a as GameObjectCompareTag;
                if (ct != null)
                {
                    var go = ct.gameObject.Value;
                    bool flag = go != null && go.CompareTag(ct.tag.Value);
                    ev = flag ? ct.trueEvent : ct.falseEvent;
                    if (Handled(fsm, ev)) return true; continue;
                }
                if (a is TriggerEvent || a is TriggerEventByLayer || a is CollisionEvent || a is CollisionEventPlus) continue;
                // getter: the action's own per-frame work (writes its variables exactly like vanilla)
                a.OnUpdate();
                if (a.Finished) return true;                      // should not happen for everyFrame getters; let vanilla continue
            }
            return false;
        }

        /// <summary>Would firing <paramref name="ev"/> change state (global transition or one of the active state's)? Unhandled events
        /// are no-ops in vanilla too (none of the supported action types overrides Event()).</summary>
        static bool Handled(Fsm fsm, FsmEvent ev)
        {
            if (ev == null) return false;
            string name = ev.Name;
            if (string.IsNullOrEmpty(name)) return false;
            var g = fsm.GlobalTransitions;
            if (g != null) for (int i = 0; i < g.Length; i++) if (g[i].EventName == name) return true;
            var t = fsm.ActiveState.Transitions;
            if (t != null) for (int i = 0; i < t.Length; i++) if (t[i].EventName == name) return true;
            return false;
        }

        internal static int CountFor(Feature owner, out int asleep)
        {
            int n = 0; asleep = 0;
            for (int i = 0; i < _all.Count; i++) if (_all[i].Owner == owner) { n++; if (_all[i].Asleep) asleep++; }
            return n;
        }
    }

    /// <summary>Base for the features that only feed FSM names to <see cref="PollSleep"/>.</summary>
    public abstract class PollSleepFeature : Feature
    {
        public override string Category { get { return Categories.Content; } }
        protected abstract string[] FsmNames { get; }
        protected virtual string RootPattern { get { return null; } }

        public override void SubscribeHooks() { PollSleep.Hook(Module); }
        protected override void OnEnable(FeatureMode mode)
        {
            PollSleep.Acquire(this);
            var names = FsmNames;
            for (int i = 0; i < names.Length; i++) FsmEdits.Watch(this, names[i], RootPattern, Ready);
        }
        void Ready(PlayMakerFSM f) { PollSleep.Register(this, f); }
        protected override void OnDisable() { FsmEdits.Unwatch(this); PollSleep.Release(this); }
        public override void OnSceneLoaded() { PollSleep.OnSceneLoaded(); FsmPause.Prune(); }
        public override void DrawStats(ToolLayout l)
        {
            int asleep, n = PollSleep.CountFor(this, out asleep);
            l.Value("Watched FSMs / asleep now", n + " / " + asleep);
            l.Value("All poll sleepers: sleeps / wakes", PollSleep.SleepCount + " / " + PollSleep.WakeCount);
        }
    }
}
