using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using WildWasteland.Core;

namespace WildWastelandDeveloper
{
    internal sealed class JobRow
    {
        internal string System, Name, Phase; internal double Calls, Milliseconds, Average, Maximum; internal int Errors; internal bool Enabled;
    }
    internal sealed class EngineCounter : IDisposable
    {
        internal string Name, Unit, Display = "n/a"; private ProfilerRecorder _recorder; private double _scale;
        internal EngineCounter(ProfilerCategory category, string name, string unit, double scale = 1)
        {
            Name = name; Unit = unit; _scale = scale;
            try { _recorder = ProfilerRecorder.StartNew(category, name); } catch { }
        }
        internal void Update() { Display = _recorder.Valid ? (_recorder.LastValue * _scale).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " " + Unit : "n/a (not published by player)"; }
        public void Dispose() { if (_recorder.Valid) _recorder.Dispose(); }
    }
    internal static class Measurements
    {
        internal const int Capacity = 8192;
        internal static double RecordedSeconds;
        internal static int PausedFrames;
        internal static int DashboardFrames;
        internal static readonly float[] Frames = new float[Capacity];
        private static readonly float[] Sorted = new float[Capacity];
        internal static int Head, Count, Samples;
        internal static float Average, P95, P99, Worst;
        internal static long ManagedBytes; internal static int Gc0, Gc1, Gc2;
        internal static double SchedulerMsPerSecond, OwnMilliseconds;
        internal static readonly List<EngineCounter> Engine = new List<EngineCounter>();
        internal static List<JobRow> Jobs = new List<JobRow>();
        internal static List<MethodRow> Methods = new List<MethodRow>();
        internal static readonly Dictionary<string, double> SystemCosts = new Dictionary<string, double>();
        private sealed class Previous { internal long Calls; internal double Ms; }
        private static readonly Dictionary<Job, Previous> Old = new Dictionary<Job, Previous>();
        private static readonly List<Job> Dead = new List<Job>();
        private static readonly HashSet<Job> Live = new HashSet<Job>();
        private static int _gc0, _gc1, _gc2;
        internal static string Context = "", Coverage = "";
        internal static readonly List<string> ModuleStates = new List<string>();
        internal static void Initialize()
        {
            Engine.Add(new EngineCounter(ProfilerCategory.Internal, "Main Thread", "ms", .000001));
            Engine.Add(new EngineCounter(ProfilerCategory.Internal, "Render Thread", "ms", .000001));
            Engine.Add(new EngineCounter(ProfilerCategory.Render, "GPU Frame Time", "ms", .000001));
            Engine.Add(new EngineCounter(ProfilerCategory.Memory, "GC Allocated In Frame", "KiB/frame", 1.0 / 1024));
            Engine.Add(new EngineCounter(ProfilerCategory.Memory, "GC Used Memory", "MiB", 1.0 / 1048576));
            Engine.Add(new EngineCounter(ProfilerCategory.Render, "Draw Calls Count", "draws/frame"));
            Engine.Add(new EngineCounter(ProfilerCategory.Render, "SetPass Calls Count", "passes/frame"));
            Engine.Add(new EngineCounter(ProfilerCategory.Render, "Triangles Count", "triangles/frame"));
            Engine.Add(new EngineCounter(ProfilerCategory.Render, "Shadow Casters Count", "casters/frame"));
            Reset();
        }
        internal static void Frame()
        {
            Instrumentation.Frame = Time.frameCount;
            float ms = Time.unscaledDeltaTime * 1000;
            if (ms <= 0 || float.IsNaN(ms) || float.IsInfinity(ms)) return;
            Frames[Head] = ms; Head = (Head + 1) % Capacity; Count = Math.Min(Count + 1, Capacity); Samples++;
            RecordedSeconds += ms / 1000.0; if (Time.timeScale == 0) PausedFrames++;
            if (Runtime.Instance != null && Runtime.Instance.Visible) DashboardFrames++;
        }
        internal static void Snapshot(double seconds)
        {
            double total = 0; Worst = 0;
            for (int i = 0; i < Count; i++) { Sorted[i] = Frames[i]; total += Frames[i]; Worst = Mathf.Max(Worst, Frames[i]); }
            Array.Sort(Sorted, 0, Count);
            Average = Count == 0 ? 0 : (float)(total / Count);
            P95 = Count == 0 ? 0 : Sorted[Math.Min(Count - 1, (int)(Count * .95))]; P99 = Count == 0 ? 0 : Sorted[Math.Min(Count - 1, (int)(Count * .99))];
            ManagedBytes = GC.GetTotalMemory(false); Gc0 = GC.CollectionCount(0) - _gc0; Gc1 = GC.CollectionCount(1) - _gc1; Gc2 = GC.CollectionCount(2) - _gc2;
            foreach (var counter in Engine) counter.Update();
            var rows = new List<JobRow>(); Live.Clear(); SystemCosts.Clear(); SchedulerMsPerSecond = 0;
            foreach (var job in Scheduler.AllJobs())
            {
                Live.Add(job); Previous old; if (!Old.TryGetValue(job, out old)) { old = new Previous { Calls = job.Calls, Ms = job.TotalMs }; Old.Add(job, old); }
                long calls = job.Calls - old.Calls; double ms = job.TotalMs - old.Ms;
                if (calls < 0 || ms < 0) { calls = 0; ms = 0; }
                rows.Add(new JobRow { System = job.Owner == null ? "Core" : job.Owner.Id, Name = job.Name, Phase = job.Phase.ToString(), Calls = calls / seconds,
                    Milliseconds = ms / seconds, Average = calls == 0 ? 0 : ms / calls, Maximum = job.MaxMs, Errors = job.Exceptions, Enabled = job.Enabled });
                SchedulerMsPerSecond += ms / seconds; old.Calls = job.Calls; old.Ms = job.TotalMs;
                string system = job.Owner == null ? "Core" : job.Owner.Id; double sum; SystemCosts.TryGetValue(system, out sum); SystemCosts[system] = sum + ms / seconds;
            }
            Dead.Clear(); foreach (var kv in Old) if (!Live.Contains(kv.Key)) Dead.Add(kv.Key); foreach (var job in Dead) Old.Remove(job);
            rows.Sort((a, b) => b.Milliseconds.CompareTo(a.Milliseconds)); Jobs = rows;
            Methods = Instrumentation.Snapshot(seconds);
            Diagnosis.Observe();
            ModuleStates.Clear(); foreach (var entry in ModuleHost.Entries) ModuleStates.Add(entry.Module.Id + " | " + entry.State + " | errors " + entry.Errors + (string.IsNullOrEmpty(entry.LastError) ? "" : " | " + entry.LastError));
            var player = GameRefs.Player;
            Context = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " | " + GameRefs.SaveLoadState + " | " + (player == null ? "no player" : "player " + player.transform.position.ToString("F1")) + " | time scale " + Time.timeScale.ToString("0.##");
            int patched = 0, failed = 0, eligible = 0; foreach (var scope in Instrumentation.Scopes) { patched += scope.Patched; failed += scope.Failed; eligible += scope.Eligible; }
            Coverage = patched + " detailed methods installed; " + failed + " patch failures; " + Instrumentation.PendingCount + " waiting; " + eligible + " discovered entry points across " + Instrumentation.Scopes.Count + " systems";
        }
        internal static void Reset()
        {
            Count = Head = Samples = 0; Average = P95 = P99 = Worst = 0;
            RecordedSeconds = 0; PausedFrames = 0;
            DashboardFrames = 0;
            _gc0 = GC.CollectionCount(0); _gc1 = GC.CollectionCount(1); _gc2 = GC.CollectionCount(2);
            foreach (var probe in Instrumentation.Probes) {
                System.Threading.Interlocked.Exchange(ref probe.Maximum, 0);
                probe.EpochCalls = System.Threading.Interlocked.Read(ref probe.Calls); probe.EpochInclusive = System.Threading.Interlocked.Read(ref probe.Inclusive);
                probe.EpochSelf = System.Threading.Interlocked.Read(ref probe.Self); probe.EpochErrors = System.Threading.Interlocked.Read(ref probe.Errors);
                probe.EpochMain = System.Threading.Interlocked.Read(ref probe.Main); probe.EpochWorker = System.Threading.Interlocked.Read(ref probe.Worker); probe.EpochAllocated = System.Threading.Interlocked.Read(ref probe.Allocated);
            }
            Instrumentation.Snapshot(1); Old.Clear();
            Diagnosis.ResetWindow();
        }
        internal static void Dispose() { foreach (var counter in Engine) counter.Dispose(); Engine.Clear(); }
    }
}
