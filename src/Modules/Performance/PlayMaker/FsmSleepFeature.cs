using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// FS1 FSM sleep (plan §4, research A §4.2). ~70% of the enabled FSMs sit in a state where Fsm.Update does nothing
    /// observable, but PlayMakerFSM.Update (PMF.cs:426) still runs Fsm.Update every frame (Fsm.cs:2184-2231: push, delayed
    /// events, FsmState.OnUpdate, UpdateStateChanges over all states, pop). Such an FSM is put to sleep by setting ITS OWN
    /// Fsm.ManualUpdate flag (PMF.Update then skips Fsm.Update; Unity's Update order is untouched) and woken by every path
    /// that can make its next Update do something.
    ///
    /// Sleep condition (checked in a postfix of Fsm.Update, i.e. right after an Update ran, so ResetLoopCount/EventTarget = null
    /// of UpdateStateChanges already happened): owner is a PlayMakerFSM whose own Fsm this is (no sub-FSM), started, not
    /// finished, no breakpoint, active state entered (else Update would Continue()), not switching, no pending delayed events,
    /// active state set, and FsmState.OnUpdate (FsmState.cs) is a no-op: either (state finished and no active actions) or no
    /// active action overrides OnUpdate (static per type; FsmStateActionAdvanced counts as overriding) and
    /// CheckAllActionsFinished returns without effect (state finished, or not active, or no finished actions pending and at
    /// least one active action BlocksFinish). Never applied to an FSM whose ManualUpdate somebody else set (only FSMs in our
    /// own set are ever cleared). Sleeps only while the Core.Hooks Fsm.ProcessEvent dispatcher is live.
    ///
    /// Wake (clear the flag): Fsm.ProcessEvent (Core.Hooks prefix, every event from any source incl. other mods, UI proxies,
    /// delayed events of other FSMs), Fsm.Event(FsmEventTarget, FsmEvent) (all Event overloads end there; wakes the sender),
    /// Fsm.SwitchState (SetState, GotoPreviousState, ReEnterState, transitions), Fsm.Start, Fsm.OnEnable, Fsm.Stop, Fsm.Init,
    /// Fsm.Reinitialize, Fsm.Clear, Fsm.OnDestroy, Fsm.DelayedEvent (both overloads; the list lives on the SENDER),
    /// FsmStateAction.Finish (callbacks/coroutines finishing an action; FINISHED fires on the next Update), and every
    /// physics/animator/GUI callback (Fsm.OnTrigger*/OnCollision* incl. 2D, OnParticleCollision, OnControllerColliderHit,
    /// OnJointBreak/2D, OnAnimatorMove/IK, OnGUI); PlayMakerFSM.OnBeforeSerialize (Instantiate copies the serialized flag);
    /// PlayMakerFSM.Awake clears a copied flag; scene load, save and switch-off wake everything; <see cref="Wake"/> /
    /// <see cref="WakeAll"/> for other mods that edit actions or delayed events directly.
    /// Not wake sources (nothing in an idle state reads them): variable changes, coroutines, Fixed/LateUpdate proxies
    /// (they call Fsm.FixedUpdate/LateUpdate directly; any Finish/Event they cause wakes). BlocksFinish is only set in action
    /// Awake() in this game (SetEventTarget, IgnoreEvents, debug draw actions).
    /// Known semantic difference: FsmState.StateTime does not advance while asleep (read only by GetTimeInfo/BlockEvents, 0
    /// uses in the game).
    /// Shadow: nothing sleeps; FSMs that would be asleep (no wake since the decision) are snapshotted before their Update and
    /// compared after it; any change = a missing wake source (mismatch).
    /// </summary>
    public sealed class FsmSleepFeature : Feature
    {
        public override string PlanId { get { return "FS1"; } }
        public override string Key { get { return "FSM sleep"; } }
        public override string Title { get { return "Idle FSMs skip their update"; } }
        public override string Category { get { return Categories.PlayMaker; } }
        public override int Order { get { return 11; } }
        public override bool SupportsShadow { get { return true; } }
        public override string Help { get { return "FSMs waiting for an event (doors, items, switches, parked cars) stop running their per-frame update until something happens to them."; } }

        internal static bool On, Shadow;
        static bool Any { get { return On || Shadow; } }
        static FsmSleepFeature _inst;
        public static long ClonesCleared, Snapshots;

        static readonly HashSet<Fsm> _asleep = new HashSet<Fsm>(RefComparer<Fsm>.Instance);
        static readonly Func<Fsm, bool> ActiveStateEntered = FastReflect.Getter<Fsm, bool>(typeof(Fsm), "activeStateEntered");
        static readonly Func<FsmState, bool> StateFinished = FastReflect.Getter<FsmState, bool>(typeof(FsmState), "finished");
        static readonly Func<FsmState, List<FsmStateAction>> FinishedActions = FastReflect.Getter<FsmState, List<FsmStateAction>>(typeof(FsmState), "_finishedActions");
        static readonly Func<PlayMakerFSM, Fsm> PmfFsm = FastReflect.Getter<PlayMakerFSM, Fsm>(typeof(PlayMakerFSM), "fsm");

        static readonly Dictionary<Type, bool> _updates = new Dictionary<Type, bool>();
        static Type _lastType; static bool _lastUpdates;

        protected override void OnEnable(FeatureMode mode)
        {
            if (!EnsurePatched(typeof(Patch))) throw new Exception("FSM sleep patches failed");
            _inst = this;
            Shadow = mode == FeatureMode.Shadow; On = mode == FeatureMode.On;
        }

        protected override void OnDisable()
        {
            WakeAll();
            On = Shadow = false;
            _snapFsm = null;
        }

        public override void SubscribeHooks()
        {
            Hooks.ProcessEvent.Prefix(Module, BeforeProcessEvent);
        }

        public override void OnSceneLoaded() { WakeAll(); }
        public override void OnSaveBegin() { WakeAll(); }

        public override void DrawStats(ToolLayout l)
        {
            l.Value(IsShadow ? "Would be asleep now" : "Asleep now", _asleep.Count + " of " + PlayMakerFSM.FsmList.Count + " enabled FSMs");
            l.Value("Shadow checks / copied flags cleared", Snapshots + " / " + ClonesCleared);
            if (On && !Hooks.ProcessEvent.Applied) l.Text("Waiting for the Fsm.ProcessEvent dispatcher: nothing sleeps.");
        }

        // ---------------------------------------------------------------- wake

        /// <summary>Wakes one FSM put to sleep by this feature (no-op otherwise). Safe from any code on the main thread.</summary>
        public static void Wake(Fsm fsm)
        {
            if (Shadow) { if (_asleep.Remove(fsm)) Telemetry.Wakes++; return; }
            if (!fsm.ManualUpdate) return;          // awake, or set by somebody else? (then not in our set either)
            if (_asleep.Remove(fsm)) { fsm.ManualUpdate = false; Telemetry.Wakes++; }
        }

        /// <summary>Wakes every FSM this feature put to sleep.</summary>
        public static void WakeAll()
        {
            if (!Shadow) foreach (var f in _asleep) if (f != null) f.ManualUpdate = false;
            Telemetry.Wakes += _asleep.Count;
            _asleep.Clear();
        }

        static bool BeforeProcessEvent(Fsm fsm, FsmEvent ev, FsmEventData data)
        {
            if (Any && fsm != null) Wake(fsm);
            return true;
        }

        // ---------------------------------------------------------------- sleep decision

        static bool UpdatesEveryFrame(FsmStateAction a)
        {
            var t = a.GetType();
            if (ReferenceEquals(t, _lastType)) return _lastUpdates;
            bool v;
            if (!_updates.TryGetValue(t, out v))
            {
                try
                {
                    var m = t.GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                    v = m == null || m.DeclaringType != typeof(FsmStateAction);
                }
                catch (Exception) { v = true; }
                _updates[t] = v;
            }
            _lastType = t; _lastUpdates = v;
            return v;
        }

        /// <summary>FsmState.OnUpdate would do nothing observable (besides StateTime).</summary>
        static bool StateIdle(Fsm fsm, FsmState st)
        {
            var acts = st.ActiveActions;
            int n = acts.Count;
            bool finished = StateFinished(st);
            if (finished && n == 0) return true;                  // OnUpdate body skipped entirely
            for (int i = 0; i < n; i++)
            {
                var a = acts[i];
                if (a == null || UpdatesEveryFrame(a)) return false;
            }
            // body runs: base OnUpdate (empty) on each, then CheckAllActionsFinished
            if (finished || !st.Active) return true;              // CheckAllActionsFinished returns at once
            var fin = FinishedActions(st);
            if (fin != null && fin.Count != 0) return false;      // would remove them and maybe finish
            if (n == 0) return false;                             // would finish the state
            for (int i = 0; i < n; i++) if (acts[i].BlocksFinish) return true;
            return false;                                         // nothing blocks: would finish
        }

        static bool CanSleep(Fsm fsm)
        {
            if (fsm.ManualUpdate || !fsm.Started || fsm.Finished || fsm.IsSwitchingState || Fsm.HitBreakpoint) return false;
            if (!ActiveStateEntered(fsm)) return false;
            if (fsm.DelayedEvents.Count != 0) return false;
            var st = fsm.ActiveState;
            if (st == null || !StateIdle(fsm, st)) return false;
            if (fsm.Host != null) return false;
            var pmf = fsm.Owner as PlayMakerFSM;
            if (ReferenceEquals(pmf, null) || !ReferenceEquals(PmfFsm(pmf), fsm)) return false;
            return true;
        }

        // ---------------------------------------------------------------- shadow snapshot

        static Fsm _snapFsm;
        static FsmState _sState; static bool _sEntered, _sStateFinished, _sSwitching, _sFinished, _sStarted;
        static int _sActive, _sFinishedActs, _sDelayed, _sLoop; static FsmEventTarget _sTarget;

        static void Snap(Fsm fsm)
        {
            Snapshots++;
            _snapFsm = fsm;
            _sState = fsm.ActiveState; _sEntered = ActiveStateEntered(fsm); _sSwitching = fsm.IsSwitchingState;
            _sFinished = fsm.Finished; _sStarted = fsm.Started; _sDelayed = fsm.DelayedEvents.Count; _sTarget = fsm.EventTarget;
            if (_sState != null)
            {
                _sStateFinished = StateFinished(_sState); _sActive = _sState.ActiveActions.Count;
                var fin = FinishedActions(_sState); _sFinishedActs = fin != null ? fin.Count : 0; _sLoop = _sState.loopCount;
            }
        }

        static void Compare(Fsm fsm)
        {
            _snapFsm = null;
            var st = fsm.ActiveState;
            bool same = ReferenceEquals(st, _sState) && ActiveStateEntered(fsm) == _sEntered && fsm.IsSwitchingState == _sSwitching
                && fsm.Finished == _sFinished && fsm.Started == _sStarted && fsm.DelayedEvents.Count == _sDelayed && ReferenceEquals(fsm.EventTarget, _sTarget);
            if (same && st != null)
            {
                var fin = FinishedActions(st);
                same = StateFinished(st) == _sStateFinished && st.ActiveActions.Count == _sActive && (fin != null ? fin.Count : 0) == _sFinishedActs && st.loopCount == _sLoop;
            }
            if (!same && _inst != null)
                _inst.Mismatch("FSM '" + fsm.Name + "' on " + fsm.OwnerName + " state '" + (_sState != null ? _sState.Name : "-") + "' -> '" + (st != null ? st.Name : "-") + "' changed during an Update it would have slept through");
        }

        // ---------------------------------------------------------------- patches

        static void W(Fsm f) { if (Any && f != null) Wake(f); }

        [HarmonyPatch]
        static class Patch
        {
            [HarmonyPatch(typeof(Fsm), "Update"), HarmonyPrefix]
            static void UpdatePre(Fsm __instance)
            {
                if (Shadow && _asleep.Contains(__instance)) Snap(__instance);
            }

            [HarmonyPatch(typeof(Fsm), "Update"), HarmonyPostfix]
            static void UpdatePost(Fsm __instance)
            {
                if (!Any) return;
                if (Shadow)
                {
                    if (ReferenceEquals(_snapFsm, __instance)) Compare(__instance);
                    if (!_asleep.Contains(__instance) && CanSleep(__instance)) { _asleep.Add(__instance); Telemetry.Sleeps++; }
                    return;
                }
                if (!Hooks.ProcessEvent.Applied || !CanSleep(__instance)) return;
                _asleep.Add(__instance);
                __instance.ManualUpdate = true;
                Telemetry.Sleeps++;
            }

            // ---- event / state paths ----
            [HarmonyPatch(typeof(Fsm), "Event", typeof(FsmEventTarget), typeof(FsmEvent)), HarmonyPrefix]
            static void EventPre(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "SwitchState"), HarmonyPrefix] static void SwitchStatePre(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "Start"), HarmonyPrefix] static void StartPre(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnEnable"), HarmonyPrefix] static void OnEnablePre(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "Stop"), HarmonyPrefix] static void StopPre(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "Init"), HarmonyPrefix] static void InitPre(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "Reinitialize"), HarmonyPrefix] static void ReinitPre(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "Clear"), HarmonyPrefix] static void ClearPre(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnDestroy"), HarmonyPrefix] static void OnDestroyPre(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "DelayedEvent", typeof(FsmEvent), typeof(float)), HarmonyPrefix]
            static void Delayed1Pre(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "DelayedEvent", typeof(FsmEventTarget), typeof(FsmEvent), typeof(float)), HarmonyPrefix]
            static void Delayed2Pre(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(FsmStateAction), "Finish"), HarmonyPrefix]
            static void FinishPre(FsmStateAction __instance) { if (Any) { var f = __instance.Fsm; if (f != null) Wake(f); } }

            // ---- physics / animator / GUI callbacks ----
            [HarmonyPatch(typeof(Fsm), "OnTriggerEnter"), HarmonyPrefix] static void P1(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnTriggerStay"), HarmonyPrefix] static void P2(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnTriggerExit"), HarmonyPrefix] static void P3(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnCollisionEnter"), HarmonyPrefix] static void P4(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnCollisionStay"), HarmonyPrefix] static void P5(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnCollisionExit"), HarmonyPrefix] static void P6(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnTriggerEnter2D"), HarmonyPrefix] static void P7(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnTriggerStay2D"), HarmonyPrefix] static void P8(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnTriggerExit2D"), HarmonyPrefix] static void P9(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnCollisionEnter2D"), HarmonyPrefix] static void P10(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnCollisionStay2D"), HarmonyPrefix] static void P11(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnCollisionExit2D"), HarmonyPrefix] static void P12(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnParticleCollision"), HarmonyPrefix] static void P13(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnControllerColliderHit"), HarmonyPrefix] static void P14(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnJointBreak"), HarmonyPrefix] static void P15(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnJointBreak2D"), HarmonyPrefix] static void P16(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnAnimatorMove"), HarmonyPrefix] static void P17(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnAnimatorIK"), HarmonyPrefix] static void P18(Fsm __instance) { W(__instance); }
            [HarmonyPatch(typeof(Fsm), "OnGUI"), HarmonyPrefix] static void P19(Fsm __instance) { W(__instance); }

            // ---- serialized flag ----
            [HarmonyPatch(typeof(PlayMakerFSM), "OnBeforeSerialize"), HarmonyPrefix]
            static void BeforeSerializePre(PlayMakerFSM __instance)
            {
                if (!Any) return;
                var f = PmfFsm(__instance);
                if (f == null || !f.ManualUpdate) return;
                if (PlayMakerFSM.IsMainThread) Wake(f);
                else
                {
                    // off the main thread (loading): never mutate the set; the FSM re-sleeps consistently later
                    try { if (_asleep.Contains(f)) f.ManualUpdate = false; } catch (Exception) { f.ManualUpdate = false; }
                }
            }

            [HarmonyPatch(typeof(PlayMakerFSM), "Awake"), HarmonyPostfix]
            static void AwakePost(PlayMakerFSM __instance)
            {
                if (!On) return;
                var f = PmfFsm(__instance);
                // vanilla never sets ManualUpdate (research A); a fresh FSM carrying it was copied from a sleeping one
                if (f != null && f.ManualUpdate && !_asleep.Contains(f))
                {
                    f.ManualUpdate = false; ClonesCleared++;
                    if (ClonesCleared <= 3 && Log.Verbose) Log.Info("[Performance] FS1: cleared a copied sleep flag on " + __instance.name + " / " + f.Name);
                }
            }
        }
    }
}
