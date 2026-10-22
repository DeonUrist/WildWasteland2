using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace WildWasteland.Core
{
    public static partial class Access
    {
        /// <summary>
        /// Background accessor compilation (port of Performance CompileQueue): requests queue up; a LateUpdate frame job hands
        /// everything queued so far to ONE thread-pool work item that emits all of them into one <see cref="Batch"/> (one
        /// assembly load), then publishes the delegates and runs the "done" callbacks. Until then callers use reflection, so
        /// results never differ. The first batch is built on the main thread once (warms MonoMod's IL proxy).
        /// Differences from the Performance copy: the job is named "&lt;owner.Id&gt;.AccessCompile" (Scheduler naming rule) and
        /// the Telemetry "compile" mark is raised through <see cref="OnFirstBatch"/> (Debug/Optimization subscribe).
        /// </summary>
        public static class Queue
        {
            /// <summary>One queued request: adds its builders to the batch, then Done runs after the build.</summary>
            struct Item { public Action<Batch> Add; public Action Done; }

            static readonly List<Item> _q = new List<Item>();
            static Job _job;
            static volatile bool _busy;
            static bool _warmed;
            /// <summary>Bumped after every published batch: cached "not ready yet" results re-check when it changes.</summary>
            public static int Version;
            /// <summary>Accessors built and batches run so far.</summary>
            public static long Built, Batches;
            /// <summary>Duration of the last and the worst batch (ms).</summary>
            public static long LastMs, WorstMs;
            /// <summary>Last build failure (null when none).</summary>
            public static string LastError;
            /// <summary>Raised once on the main thread before the first (warm-up) batch; Performance used it for Telemetry.Mark("compile").</summary>
            public static Action OnFirstBatch;

            /// <summary>Requests queued and not yet handed to a batch.</summary>
            public static int Pending { get { lock (_q) return _q.Count; } }

            /// <summary>
            /// Queues builders (add) and a completion callback (done, may be null; runs on the worker thread or the main thread
            /// for the first batch). owner registers the pump job if none is running (none: the request waits for an owner).
            /// </summary>
            public static void Enqueue(IModule owner, Action<Batch> add, Action done)
            {
                lock (_q) _q.Add(new Item { Add = add, Done = done });
                if (owner != null && (_job == null || _job.Removed)) _job = Scheduler.EveryFrame(owner, owner.Id + ".AccessCompile", Pump, Phase.LateUpdate);
            }

            /// <summary>Frame job: hands the queued requests to one batch (worker thread, except the very first one).</summary>
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
                    var first = OnFirstBatch;
                    if (first != null) { try { first(); } catch (Exception) { } }
                    Run(items);
                    return;
                }
                _busy = true;
                if (!ThreadPool.QueueUserWorkItem(Work, items)) { _busy = false; Run(items); }
            }

            /// <summary>Thread-pool entry: builds the batch and clears the busy flag.</summary>
            static void Work(object state)
            {
                try { Run((Item[])state); } finally { _busy = false; }
            }

            /// <summary>Builds one batch from items, publishes the delegates, runs the callbacks and updates the stats.</summary>
            static void Run(Item[] items)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var b = new Batch();
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

            /// <summary>One-line status for the tool window / Debug page.</summary>
            public static string Stats()
            {
                return Built + " accessors in " + Batches + " batches (last " + LastMs + " ms, worst " + WorstMs + " ms, background), " + Pending + " queued"
                    + (LastError != null ? ", error: " + LastError : "");
            }
        }
    }
}
