using System;
using System.Collections.Generic;
using System.Diagnostics;
using WildWasteland.Core;

namespace WildWasteland.Modules.Debug
{
    /// <summary>One completed snapshot of a Core hook, event handler or named scope; rates exclude time before the sample window.</summary>
    internal sealed class HandlerRow
    {
        internal string Kind, Owner, Name;
        internal double Calls, Milliseconds;
        internal int Errors;
    }

    /// <summary>Combines Core-owned handler counters without patching dispatchers a second time or retaining removed owners.</summary>
    internal static class HandlerMeasurements
    {
        private sealed class Previous
        {
            internal long Calls, Ticks;
            internal int Errors;
        }

        private static readonly Dictionary<object, Previous> Old = new Dictionary<object, Previous>();
        private static readonly HashSet<object> Live = new HashSet<object>();
        private static readonly List<object> Dead = new List<object>();
        private static readonly List<Hooks.HandlerStat> HooksBuffer = new List<Hooks.HandlerStat>();
        private static readonly List<ModEvents.HandlerStat> EventsBuffer = new List<ModEvents.HandlerStat>();
        internal static readonly List<HandlerRow> Rows = new List<HandlerRow>();

        /// <summary>Reads each live Core counter once per measurement snapshot and computes non-overlapping per-handler rates.</summary>
        internal static void Snapshot(double seconds)
        {
            Rows.Clear(); Live.Clear();
            seconds = Math.Max(.001, seconds);
            HooksBuffer.Clear(); Hooks.Stats(HooksBuffer);
            foreach (var row in HooksBuffer) Add(row, "Hook", row.Owner, row.Target + " " + row.Kind + " " + row.Method, row.Calls, row.Ticks, row.Errors, seconds);
            EventsBuffer.Clear(); ModEvents.Stats(EventsBuffer);
            foreach (var row in EventsBuffer) Add(row, "Event", row.Owner, row.Event + " " + row.Method, row.Calls, row.Ticks, row.Errors, seconds);
            foreach (var row in Core.Trace.Scopes) Add(row, "Scope", row.Owner, row.Name, row.Calls, row.Ticks, 0, seconds);
            Dead.Clear(); foreach (var pair in Old) if (!Live.Contains(pair.Key)) Dead.Add(pair.Key);
            foreach (var key in Dead) Old.Remove(key);
            Rows.Sort((a, b) => b.Milliseconds.CompareTo(a.Milliseconds));
        }

        /// <summary>Computes one handler's delta, treating reset counters and newly registered handlers as a fresh baseline.</summary>
        private static void Add(object key, string kind, IModule owner, string name, long calls, long ticks, int errors, double seconds)
        {
            Live.Add(key);
            Previous old;
            if (!Old.TryGetValue(key, out old))
            {
                old = new Previous { Calls = calls, Ticks = ticks, Errors = errors };
                Old.Add(key, old);
            }
            long deltaCalls = Math.Max(0, calls - old.Calls), deltaTicks = Math.Max(0, ticks - old.Ticks);
            Rows.Add(new HandlerRow { Kind = kind, Owner = owner != null ? owner.Id : "Core", Name = name,
                Calls = deltaCalls / seconds, Milliseconds = deltaTicks * 1000.0 / Stopwatch.Frequency / seconds, Errors = Math.Max(0, errors - old.Errors) });
            old.Calls = calls; old.Ticks = ticks; old.Errors = errors;
        }

        /// <summary>Forgets prior sample deltas when the user resets the measurement window.</summary>
        internal static void Reset() { Old.Clear(); Rows.Clear(); Live.Clear(); Dead.Clear(); HooksBuffer.Clear(); EventsBuffer.Clear(); }
    }
}
