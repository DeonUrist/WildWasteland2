using System.Collections.Concurrent;
using System.Diagnostics;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    // Log timings only. TownRuntime holds the existing native handoff until loaded towns are ready.
    internal static class TownLoading
    {
        private static readonly ConcurrentDictionary<string, Work> WorkItems = new ConcurrentDictionary<string, Work>();
        internal sealed class Work
        {
            private readonly string _key;
            private readonly Stopwatch _time = Stopwatch.StartNew();
            private bool _done;
            /// <summary>Initializes work with the supplied owning state and immutable inputs.</summary>
            internal Work(string key) { _key = key; if (Log.L1) Log.Main(null, "[POI] generation started " + key); }
            /// <summary>Completes the loading record with its final status.</summary>
            internal void Finish(string detail)
            {
                lock (_time) { if (_done) return; _done = true; }
                if (Log.L1) Log.Main(null, "[POI] generation ended " + _key + ": " + detail + "; " + _time.ElapsedMilliseconds + " ms");
                Work ignored; WorkItems.TryRemove(_key, out ignored);
            }
        }
        /// <summary>Starts a tracked generation or loading record.</summary>
        internal static Work Begin(string key) { var work = new Work(key); WorkItems[key] = work; return work; }
        /// <summary>Releases current resources and resets the associated world or catalog state.</summary>
        internal static void Clear(string reason) { foreach (var work in WorkItems.Values) work.Finish(reason); WorkItems.Clear(); }
        /// <summary>Completes pending loading records with the failure reason.</summary>
        internal static void Failed(string reason) { Clear("Stopped: " + reason); }
    }
}
