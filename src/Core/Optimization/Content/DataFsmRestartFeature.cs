using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// CT9b (research B §1.3 "Item data FSMs", §0 RestartOnEnable): ~6 of every item's 7.5 FSMs are pure data (ID, Value, imageUI,
    /// weaponType, BumperDamage...: one state, no actions), yet RestartOnEnable (default true, Fsm.cs:L2115/L2280) makes every
    /// SetActive(true) (culling, save re-activation) stop/reset and re-start them. Provably exact case only: an FSM with exactly one
    /// state that has zero actions - restarting it re-enters the same empty state (no action, no variable change; only an unhandled
    /// FINISHED and the EnterState notification), so RestartOnEnable=false gives the same state and variables. Applied to every such FSM
    /// in the game at Awake (and to live ones when switched on). Only FSMs this feature switched are restored, and only if still
    /// false (Apocapocket / WW Patrols set it false on their own FSMs; those are left alone when they were already false).
    /// Side effect: other mods' Fsm.EnterState hooks no longer see these empty re-entries.
    /// </summary>
    public sealed class DataFsmRestartFeature : Feature
    {
        /// <summary>Plan id CT9 (shared with DuplicateRigidbodyFeature).</summary>
        public override string PlanId { get { return "CT9"; } }
        /// <summary>tools.cfg key under [Performance / Game logic].</summary>
        public override string Key { get { return "Data FSM restart"; } }
        /// <summary>Developer page title.</summary>
        public override string Title { get { return "No restart for empty data FSMs"; } }
        /// <summary>Game logic category.</summary>
        public override string Category { get { return Categories.Content; } }
        /// <summary>Order inside the category.</summary>
        public override int Order { get { return 10; } }
        /// <summary>Developer page help text.</summary>
        public override string Help { get { return "Item data FSMs (one empty state) are not restarted every time an item is re-activated (exact)."; } }

        readonly UndoLog _undo = new UndoLog();
        internal static long Switched;

        /// <summary>Watches every FSM at Awake (and live ones) through FsmEdits.</summary>
        protected override void OnEnable(FeatureMode mode) { FsmEdits.Watch(this, "*", null, Ready); }
        /// <summary>Stops watching and restores RestartOnEnable on the FSMs this feature switched.</summary>
        protected override void OnDisable() { FsmEdits.Unwatch(this); _undo.RevertAll(); }
        /// <summary>Prunes undo entries of destroyed FSMs.</summary>
        public override void OnSceneLoaded() { _undo.Prune(); }

        /// <summary>FSM with exactly one state and zero actions: RestartOnEnable = false (recorded for undo).</summary>
        void Ready(PlayMakerFSM f)
        {
            var fsm = f.Fsm;
            if (!fsm.RestartOnEnable) return;
            var st = fsm.States;
            if (st == null || st.Length != 1 || st[0] == null) return;
            var a = st[0].Actions;
            if (a == null || a.Length != 0) return;
            fsm.RestartOnEnable = false;
            _undo.Add(new RestartUndo(f));
            Switched++;
        }

        sealed class RestartUndo : Undo
        {
            readonly PlayMakerFSM _c;
            /// <summary>Undo record for one switched FSM.</summary>
            public RestartUndo(PlayMakerFSM c) { _c = c; }
            internal override bool Alive { get { return _c != null; } }
            /// <summary>Restores RestartOnEnable = true if it is still false.</summary>
            internal override void Revert() { var f = _c.Fsm; if (f != null && !f.RestartOnEnable) f.RestartOnEnable = true; }
        }

        /// <summary>Switched FSMs (live / total).</summary>
        public override void DrawStats(ToolLayout l) { l.Value("FSMs switched (live / total)", _undo.Count + " / " + Switched); }
    }
}
