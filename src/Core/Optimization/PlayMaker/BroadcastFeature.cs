using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>PM4: BroadcastAll without the throw-away copy of FsmList (pooled snapshot; per-receiver EventData copies kept).</summary>
    public sealed class BroadcastFeature : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "PM4"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Broadcast events"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Broadcast without list copies"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.PlayMaker; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 4; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Game-wide events (Save/Load settings, day change, faction changes) reuse one buffer instead of copying every FSM."; } }
        static bool On;
        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode) { if (!EnsurePatched(typeof(Patch))) throw new Exception("broadcast patch failed"); On = true; }
        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable() { On = false; }

        static readonly Stack<PlayMakerFSM[]> _pool = new Stack<PlayMakerFSM[]>();
        /// <summary>Rents a component array and captures the current native FsmList order for reentrant broadcast.</summary>
        static PlayMakerFSM[] Snapshot(out int n)
        {
            var src = PlayMakerFSM.FsmList; n = src.Count;
            var a = _pool.Count > 0 ? _pool.Pop() : null;
            if (a == null || a.Length < n) a = new PlayMakerFSM[Mathf.NextPowerOfTwo(Mathf.Max(256, n))];
            src.CopyTo(a, 0);
            return a;
        }
        /// <summary>Clears captured native references before returning a broadcast snapshot array to the pool.</summary>
        static void Release(PlayMakerFSM[] a, int n) { Array.Clear(a, 0, n); _pool.Push(a); }

        [HarmonyPatch]
        static class Patch
        {
            /// <summary>Fsm broadcast prefix: preserves exclude-self and per-receiver event data while reusing the ordered snapshot.</summary>
            [HarmonyPatch(typeof(Fsm), "BroadcastEvent", typeof(FsmEvent), typeof(bool)), HarmonyPrefix]
            static bool FsmBroadcast(Fsm __instance, FsmEvent fsmEvent, bool excludeSelf)
            {
                if (!On) return true;
                OptimizationMetrics.Broadcasts++;
                var eventData = new FsmEventData(Fsm.EventData);
                Fsm.SetEventDataSentByInfo();
                var source = new FsmEventData(Fsm.EventData);
                int n; var snap = Snapshot(out n);
                try
                {
                    for (int i = 0; i < n; i++)
                    {
                        var item = snap[i];
                        if (!(item == null) && item.Fsm != null && (!excludeSelf || item.Fsm != __instance))
                        {
                            Fsm.EventData = new FsmEventData(source);
                            item.Fsm.ProcessEvent(fsmEvent);
                        }
                    }
                }
                finally { Release(snap, n); }
                Fsm.EventData = eventData;
                return false;
            }

            /// <summary>PlayMaker broadcast prefix: delivers to the same ordered component snapshot without native list-copy allocations.</summary>
            [HarmonyPatch(typeof(PlayMakerFSM), "BroadcastEvent", typeof(FsmEvent)), HarmonyPrefix]
            static bool PmBroadcast(FsmEvent fsmEvent)
            {
                if (!On) return true;
                OptimizationMetrics.Broadcasts++;
                int n; var snap = Snapshot(out n);
                try
                {
                    for (int i = 0; i < n; i++) { var item = snap[i]; if (!(item == null) && item.Fsm != null) item.Fsm.ProcessEvent(fsmEvent); }
                }
                finally { Release(snap, n); }
                return false;
            }
        }
    }
}
