using System.Collections.Concurrent;
using System.Diagnostics;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
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
            internal Work(string key) { _key = key; if (Log.L1) Log.Main(null, "[Towns] generation started " + key); }
            internal void Finish(string detail)
            {
                lock (_time) { if (_done) return; _done = true; }
                if (Log.L1) Log.Main(null, "[Towns] generation ended " + _key + ": " + detail + "; " + _time.ElapsedMilliseconds + " ms");
                Work ignored; WorkItems.TryRemove(_key, out ignored);
            }
        }
        internal static Work Begin(string key) { var work = new Work(key); WorkItems[key] = work; return work; }
        internal static void Clear(string reason) { foreach (var work in WorkItems.Values) work.Finish(reason); WorkItems.Clear(); }
        internal static void Failed(string reason) { Clear("Stopped: " + reason); }
    }
}
