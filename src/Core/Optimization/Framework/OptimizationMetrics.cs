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
        static Action<string>[] _marks = new Action<string>[0];
        public static event Action<string> Marked
        {
            add { AddMark(value); }
            remove { RemoveMark(value); }
        }

        /// <summary>Copies the observer array only when measurement ownership changes, never in a frame/slice tag.</summary>
        static void AddMark(Action<string> handler)
        {
            if (handler == null) return;
            var current = _marks;
            var next = new Action<string>[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = handler;
            _marks = next;
        }

        /// <summary>Drops the most recently registered matching observer without retaining a stopped Debug instance.</summary>
        static void RemoveMark(Action<string> handler)
        {
            var current = _marks;
            int index = Array.LastIndexOf(current, handler);
            if (index < 0) return;
            var next = new Action<string>[current.Length - 1];
            Array.Copy(current, 0, next, 0, index);
            Array.Copy(current, index + 1, next, index, current.Length - index - 1);
            _marks = next;
        }

        /// <summary>Publishes an attribution tag, isolating measurement failures from gameplay.</summary>
        public static void Mark(string tag)
        {
            var handlers = _marks;
            for (int i = 0; i < handlers.Length; i++)
            {
                try { handlers[i](tag); }
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
