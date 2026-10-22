using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace WildWasteland.Core
{
    /// <summary>
    /// Cheap named timing scopes for code that is not a scheduler job, hook handler or event handler (e.g. a bake step), plus the
    /// global <see cref="On"/> switch that also enables per-handler timing in <see cref="Hooks"/> and <see cref="ModEvents"/>.
    /// Ids are registered once at Init; Begin/End cost one bool check while tracing is off. The Debug module owns the switch and
    /// the pages that read <see cref="Scopes"/> (README 5.2). Main thread only.
    /// </summary>
    public static class Trace
    {
        /// <summary>Tracing master switch (set by the Debug module: "Trace hooks" or log level 2).</summary>
        public static bool On;

        /// <summary>Local lifecycle markers consumed by Debug captures; the subscribing module owns its subscription lifetime.</summary>
        public static event Action<string> Marked;

        /// <summary>Publishes a lifecycle marker without creating a timing scope or enabling tracing.</summary>
        public static void Mark(string tag)
        {
            var marked = Marked;
            if (marked == null || string.IsNullOrEmpty(tag)) return;
            try { marked(tag); }
            catch (Exception e) { Log.Error(null, "Trace marker subscriber failed", e); }
        }

        /// <summary>Accumulated timing of one registered scope.</summary>
        public sealed class Scope
        {
            /// <summary>Module that registered the scope (null = Core).</summary>
            public IModule Owner;
            /// <summary>Scope name as registered ("Bake step").</summary>
            public string Name;
            /// <summary>Completed Begin/End pairs while tracing was on.</summary>
            public long Calls;
            /// <summary>Total Stopwatch ticks spent inside the scope.</summary>
            public long Ticks;
            /// <summary>Longest single call, ticks.</summary>
            public long MaxTicks;
            /// <summary>Total time in milliseconds.</summary>
            public double TotalMs { get { return Ticks * TicksToMs; } }
        }

        /// <summary>Stopwatch ticks to milliseconds.</summary>
        public static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        static readonly List<Scope> _scopes = new List<Scope>();

        /// <summary>Every registered scope, in registration order (index = id - 1).</summary>
        public static IList<Scope> Scopes { get { return _scopes; } }

        /// <summary>Registers a named scope for <paramref name="owner"/> and returns its id (call once, at Init). Re-registering the
        /// same owner + name returns the existing id, so Init stays re-runnable.</summary>
        public static int Register(IModule owner, string name)
        {
            for (int i = 0; i < _scopes.Count; i++)
                if (_scopes[i].Owner == owner && _scopes[i].Name == name) return i + 1;
            _scopes.Add(new Scope { Owner = owner, Name = name });
            return _scopes.Count;
        }

        /// <summary>Start timestamp for <see cref="End"/>; 0 while tracing is off (then End does nothing).</summary>
        public static long Begin(int id) { return On && id > 0 ? Stopwatch.GetTimestamp() : 0L; }

        /// <summary>Adds the time since <paramref name="started"/> to scope <paramref name="id"/>. No-op when started is 0.</summary>
        public static void End(int id, long started)
        {
            if (started == 0L || id <= 0 || id > _scopes.Count) return;
            long t = Stopwatch.GetTimestamp() - started;
            var s = _scopes[id - 1];
            s.Calls++; s.Ticks += t; if (t > s.MaxTicks) s.MaxTicks = t;
        }

        /// <summary>Clears every scope's counters (Debug "reset" button).</summary>
        public static void ResetStats()
        {
            for (int i = 0; i < _scopes.Count; i++) { var s = _scopes[i]; s.Calls = 0; s.Ticks = 0; s.MaxTicks = 0; }
        }
    }
}
