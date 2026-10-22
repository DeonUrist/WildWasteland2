using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using WildWasteland.Core;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// Builds FS2/FS3 accessors off the main thread, many per assembly.
    /// History: one MonoMod DynamicMethodDefinition per member loads one in-memory assembly (Player.log: "Fallback handler
    /// could not load library ...data-XXXX.dll" per load); a patrol car spawn requested 65 at once -> 0.1.26d combat spikes
    /// (worst 213 ms). 0.1.26e spread them over frames (1 ms/frame). Now: requests queue up; a frame job hands everything
    /// queued so far to ONE thread-pool work item that emits all of them into one <see cref="FastReflect.Batch"/> (Cecil,
    /// one assembly load), then publishes the delegates and runs the "done" callbacks. Until then callers use reflection
    /// (vanilla), so results never differ. The first batch is built on the main thread once (warms MonoMod's IL proxy).
    /// </summary>
    internal static class CompileQueue
    {
        struct Item { public Action<FastReflect.Batch> Add; public Action Done; }

        static readonly List<Item> _q = new List<Item>();
        static Job _job;
        static volatile bool _busy;
        static bool _warmed;
        /// <summary>Bumped after every published batch: cached "not ready yet" results re-check when it changes.</summary>
        internal static int Version;
        internal static long Built, Batches;
        internal static long LastMs, WorstMs;
        internal static string LastError;
        internal static int Pending { get { lock (_q) return _q.Count; } }

        internal static void Enqueue(IModule owner, Action<FastReflect.Batch> add, Action done)
        {
            lock (_q) _q.Add(new Item { Add = add, Done = done });
            if (owner != null && (_job == null || _job.Removed)) _job = Scheduler.EveryFrame(owner, "Performance.Compile", Pump, Phase.LateUpdate);
        }

        static void Pump()
        {
            if (_busy) return;
            Item[] items;
            lock (_q)
            {
                if (_q.Count == 0) return;
                items = _q.ToArray();
                _q.Clear();
            }
            if (!_warmed)
            {
                _warmed = true;
                Telemetry.Mark("compile");
                Run(items);   // first batch on the main thread: MonoMod builds its ILGenerator proxy type here, not on a worker
                return;
            }
            _busy = true;
            if (!ThreadPool.QueueUserWorkItem(_ => { try { Run(items); } finally { _busy = false; } })) { _busy = false; Run(items); }
        }

        static void Run(Item[] items)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var b = new FastReflect.Batch();
                for (int i = 0; i < items.Length; i++) { try { items[i].Add(b); } catch (Exception) { } }
                b.Build();
                Interlocked.Add(ref Built, b.Count);
                Interlocked.Increment(ref Batches);
            }
            catch (Exception e) { LastError = e.GetType().Name + ": " + e.Message; }
            for (int i = 0; i < items.Length; i++) { try { if (items[i].Done != null) items[i].Done(); } catch (Exception) { } }
            LastMs = sw.ElapsedMilliseconds;
            if (LastMs > WorstMs) WorstMs = LastMs;
            Interlocked.Increment(ref Version);
        }

        internal static string Stats()
        {
            return Built + " accessors in " + Batches + " batches (last " + LastMs + " ms, worst " + WorstMs + " ms, background), " + Pending + " queued"
                + (LastError != null ? ", error: " + LastError : "");
        }
    }
}
