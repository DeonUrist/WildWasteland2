using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Bootstrap;

namespace WildWastelandDeveloper
{
    internal static class Reports
    {
        private static Task<string> _writer;
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        private static string Csv(string text) { return "\"" + (text ?? "").Replace("\"", "\"\"") + "\""; }
        private static string F(double value) { return value.ToString("R", C); }
        internal static void Export(Runtime runtime)
        {
            if (_writer != null) { runtime.Status = "A capture is already being written."; return; }
            var files = new Dictionary<string, string>();
            Diagnosis.AddFiles(files, runtime);
            var metadata = new StringBuilder();
            metadata.AppendLine("WildWastelandDeveloper 0.1.0 capture UTC " + DateTime.UtcNow.ToString("O", C));
            metadata.AppendLine("Context: " + Measurements.Context).AppendLine("Coverage: " + Measurements.Coverage);
            metadata.AppendLine("Capture active: " + runtime.Recording + "; timing active: " + Instrumentation.Timing + "; hooks still installing: " + Instrumentation.PendingCount);
            metadata.AppendLine("Frame window: last " + Measurements.Count + " samples; rates: most recent approximately one-second snapshot.");
            metadata.AppendLine("Frame average/p95/p99/worst ms: " + F(Measurements.Average) + "/" + F(Measurements.P95) + "/" + F(Measurements.P99) + "/" + F(Measurements.Worst));
            metadata.AppendLine("GC collections since reset: " + Measurements.Gc0 + "/" + Measurements.Gc1 + "/" + Measurements.Gc2);
            metadata.AppendLine("Developer update+GUI average ms/frame: " + F(Measurements.OwnMilliseconds) + "; hook overhead additional.");
            metadata.AppendLine("Inclusive method/job times overlap. Self subtracts only instrumented children. Worker time is not main-thread frame time.");
            metadata.AppendLine("Cumulative methods are since hook installation; worst calls since sample reset. Job maxima/errors are WW lifetime values.");
            metadata.AppendLine("Native engine internals, third-party code, inlined calls and uninstalled/unpatchable methods are not fully attributed. Missing engine counters are n/a.");
            metadata.AppendLine("Periodic logging: " + Plugin.Summaries.Value + "; spikes: " + Plugin.SpikeLog.Value + "; lifecycle: " + Plugin.LifecycleLog.Value + "; thread allocations: " + Plugin.AllocationTiming.Value + "; API available: " + Instrumentation.AllocationSupported);
            metadata.AppendLine("Trace queue dropped: " + Instrumentation.Dropped + "; reports retain at most512 recent trace lines.");
            foreach (var counter in Measurements.Engine) metadata.AppendLine(counter.Name + ": " + counter.Display);
            foreach (var entry in Chainloader.PluginInfos) metadata.AppendLine("Plugin " + entry.Key + " " + entry.Value.Metadata.Version + " " + entry.Value.Location);
            foreach (var state in Measurements.ModuleStates) metadata.AppendLine("Module " + state);
            files.Add("metadata.txt", metadata.ToString());
            var frames = new StringBuilder("sample_oldest_first,frame_ms\n");
            for (int i = 0; i < Measurements.Count; i++) { int index = (Measurements.Head - Measurements.Count + i + Measurements.Capacity) % Measurements.Capacity; frames.Append(i).Append(',').Append(F(Measurements.Frames[index])).AppendLine(); }
            files.Add("frames.csv", frames.ToString());
            var jobs = new StringBuilder("system,job,phase,enabled,calls_per_second,ms_per_second,average_call_ms,lifetime_max_ms,lifetime_errors\n");
            foreach (var row in Measurements.Jobs) jobs.Append(Csv(row.System)).Append(',').Append(Csv(row.Name)).Append(',').Append(Csv(row.Phase)).Append(',').Append(row.Enabled).Append(',').Append(F(row.Calls)).Append(',').Append(F(row.Milliseconds)).Append(',').Append(F(row.Average)).Append(',').Append(F(row.Maximum)).Append(',').Append(row.Errors).AppendLine();
            files.Add("jobs.csv", jobs.ToString());
            var methods = new StringBuilder("system,method,calls_per_second,inclusive_ms_per_second,self_ms_per_second,main_self_ms_per_second,worker_self_ms_per_second,max_call_ms,errors_in_snapshot,inclusive_alloc_bytes_per_second\n");
            foreach (var row in Measurements.Methods) methods.Append(Csv(row.Scope)).Append(',').Append(Csv(row.Name)).Append(',').Append(F(row.Calls)).Append(',').Append(F(row.Inclusive)).Append(',').Append(F(row.Self)).Append(',').Append(F(row.Main)).Append(',').Append(F(row.Worker)).Append(',').Append(F(row.Maximum)).Append(',').Append(row.Errors).Append(',').Append(Instrumentation.AllocationSupported && Plugin.AllocationTiming.Value ? F(row.Allocated) : "n/a").AppendLine();
            files.Add("methods.csv", methods.ToString());
            var totals = new StringBuilder("system,method,cumulative_calls,cumulative_inclusive_ticks,cumulative_self_ticks,cumulative_errors,cumulative_inclusive_alloc_bytes\n");
            foreach (var p in Instrumentation.Probes) totals.Append(Csv(p.Scope.Name)).Append(',').Append(Csv(p.Name)).Append(',').Append(Interlocked.Read(ref p.Calls)).Append(',').Append(Interlocked.Read(ref p.Inclusive)).Append(',').Append(Interlocked.Read(ref p.Self)).Append(',').Append(Interlocked.Read(ref p.Errors)).Append(',').Append(Interlocked.Read(ref p.Allocated)).AppendLine();
            files.Add("method-totals.csv", totals.ToString());
            var coverage = new StringBuilder("system,enabled,trace,discovered_managed_methods,discovered_entry_points,patched,failed\n");
            foreach (var scope in Instrumentation.Scopes) coverage.Append(Csv(scope.Name)).Append(',').Append(scope.Enabled).Append(',').Append(scope.Trace).Append(',').Append(scope.Methods.Count).Append(',').Append(scope.Eligible).Append(',').Append(scope.Patched).Append(',').Append(scope.Failed).AppendLine();
            files.Add("coverage.csv", coverage.ToString());
            files.Add("patch-failures.txt", string.Join(Environment.NewLine, Instrumentation.Failures.ToArray()));
            files.Add("trace.log", string.Join(Environment.NewLine, runtime.Trace.ToArray()));
            files.Add("counters.txt", string.Join(Environment.NewLine, Dashboard.Counters.ToArray()));
            string path = Path.Combine(Paths.ConfigPath, "WildWastelandDeveloper", "Captures", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", C) + "_" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
            string wwPath = typeof(WildWasteland.Core.Scheduler).Assembly.Location, devPath = Assembly.GetExecutingAssembly().Location;
            long frequency = System.Diagnostics.Stopwatch.Frequency;
            _writer = Task.Run(() =>
            {
                Directory.CreateDirectory(path);
                foreach (var file in files) File.WriteAllText(Path.Combine(path, file.Key), file.Value, new UTF8Encoding(false));
                string builds = "Stopwatch ticks per second: " + frequency + Environment.NewLine + wwPath + " MD5=" + Hash(wwPath) + Environment.NewLine + devPath + " MD5=" + Hash(devPath);
                File.WriteAllText(Path.Combine(path, "builds.txt"), builds, new UTF8Encoding(false));
                return path;
            });
            runtime.Status = "Writing capture in background...";
        }
        private static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var md5 = MD5.Create()) return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", ""); }
        internal static void Poll(Runtime runtime)
        {
            if (_writer == null || !_writer.IsCompleted) return;
            if (_writer.IsFaulted) { runtime.Status = "Capture failed: " + _writer.Exception.GetBaseException().Message; Plugin.Log.LogError(runtime.Status); }
            else { runtime.Status = "Capture: " + _writer.Result; Plugin.Log.LogInfo(runtime.Status); }
            _writer = null;
        }
    }
}
