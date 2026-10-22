using System;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>Read-only presentation surface for an optimization's counters and state.</summary>
    public interface IStatsSource
    {
        /// <summary>Stable optimization identifier used by captures and the Debug page.</summary>
        string PlanId { get; }
        /// <summary>Draws the source's current counters without running optimization work.</summary>
        void DrawStats(ToolLayout layout);
    }

    /// <summary>Allocation-free counters owned by Core optimization; Debug observes them without being called by Core.</summary>
    public static class OptimizationMetrics
    {
        public static long SendGo, SendGoChildren, SendGoFsm, Broadcasts, FastRemovals;
        public static long Sleeps, Wakes, PickQueries, PickCasts;

        /// <summary>Lifecycle/cost tag raised for measurement consumers; no listener means no work.</summary>
        public static event Action<string> Marked;

        /// <summary>Publishes an attribution tag, isolating measurement failures from gameplay.</summary>
        public static void Mark(string tag)
        {
            var handlers = Marked;
            if (handlers == null) return;
            foreach (Action<string> handler in handlers.GetInvocationList())
            {
                try { handler(tag); }
                catch (Exception ex) { Log.Error(null, "Optimization measurement tag", ex); }
            }
        }

        /// <summary>Clears cumulative optimization counters at the start of a new measurement.</summary>
        public static void Reset()
        {
            SendGo = SendGoChildren = SendGoFsm = Broadcasts = FastRemovals = 0;
            Sleeps = Wakes = PickQueries = PickCasts = 0;
        }
    }
}
