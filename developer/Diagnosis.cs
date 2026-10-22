using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using UnityEngine;
using WildWasteland.Core;

namespace WildWastelandDeveloper
{
    // Immutable plain data is captured on the main thread; serialization/file IO runs on the writer.
    internal static class Diagnosis
    {
        internal static string Scenario = "Describe scene / location / activity / Performance on or off";
        private static Dictionary<string, object> _baseline;
        private static Dictionary<string, string> _startSettings;
        private static string _coverageAtReset;
        private static readonly List<string> WindowEvents = new List<string>();
        private static string _lastContext;
        internal static string BaselineLabel { get { return _baseline == null ? "No baseline A retained." : "Baseline A: " + _baseline["scenario"] + " | " + _baseline["utc"]; } }
        internal static void ResetWindow() { _startSettings = Settings(); _coverageAtReset = CoverageSignature(); WindowEvents.Clear(); _lastContext = null; }
        private static string CoverageSignature() { return string.Join(";", Instrumentation.Scopes.Select(s => s.Name + ":" + s.Enabled + ":" + s.Patched).ToArray()) + ";alloc=" + Plugin.AllocationTiming.Value; }
        internal static void Observe()
        {
            string context = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ":" + GameRefs.SaveLoadState + ":scale=" + Time.timeScale;
            if (context != _lastContext && WindowEvents.Count < 128) { WindowEvents.Add(DateTime.UtcNow.ToString("O") + " " + context); _lastContext = context; }
            if (_coverageAtReset != CoverageSignature() && !WindowEvents.Contains("Instrumentation changed since reset")) WindowEvents.Add("Instrumentation changed since reset");
        }
        internal static void KeepBaseline(Runtime runtime) { _baseline = Capture(runtime); }
        private static Dictionary<string, object> D(params object[] pairs)
        { var d = new Dictionary<string, object>(); for (int i = 0; i < pairs.Length; i += 2) d.Add((string)pairs[i], pairs[i + 1]); return d; }
        private static Dictionary<string, string> Settings()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            ConfigFile[] files = { WildWasteland.Plugin.Main, WildWasteland.Plugin.Settings, WildWasteland.Plugin.Tools, Plugin.Settings };
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i] == null) continue;
                foreach (var pair in files[i]) result[i + ":" + pair.Key.Section + "/" + pair.Key.Key] = pair.Value.GetSerializedValue();
            }
            return result;
        }
        private static List<string> Differences(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            var result = new List<string>(); if (a == null) { result.Add("Initial settings unavailable"); return result; }
            foreach (string key in a.Keys.Union(b.Keys).OrderBy(k => k)) { string av, bv; a.TryGetValue(key, out av); b.TryGetValue(key, out bv); if (av != bv) result.Add(key + ": " + (av ?? "absent") + " -> " + (bv ?? "absent")); }
            return result;
        }
        internal static Dictionary<string, object> Capture(Runtime runtime)
        {
            var settings = Settings();
            var modules = new List<object>(); string performance = "absent";
            foreach (var e in ModuleHost.Entries)
            {
                bool requested = e.Config.Enabled != null && e.Config.Enabled.Value;
                modules.Add(D("id", e.Module.Id, "requested_enabled", requested, "state", e.State.ToString(), "errors_lifetime", e.Errors, "last_error", e.LastError));
                if (e.Module.Id == "Performance") performance = "requested=" + requested + "; actual=" + e.State;
            }
            double seconds = Measurements.RecordedSeconds, factor = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            var methods = new List<Dictionary<string, object>>();
            foreach (var p in Instrumentation.Probes)
            {
                long calls = Interlocked.Read(ref p.Calls) - p.EpochCalls;
                methods.Add(D("system", p.Scope.Name, "method", p.Name, "calls", calls,
                    "inclusive_ms", (Interlocked.Read(ref p.Inclusive) - p.EpochInclusive) * factor,
                    "self_ms", (Interlocked.Read(ref p.Self) - p.EpochSelf) * factor,
                    "main_self_ms", (Interlocked.Read(ref p.Main) - p.EpochMain) * factor,
                    "worker_self_ms", (Interlocked.Read(ref p.Worker) - p.EpochWorker) * factor,
                    "max_call_ms", Interlocked.Read(ref p.Maximum) * factor,
                    "errors", Interlocked.Read(ref p.Errors) - p.EpochErrors,
                    "inclusive_alloc_bytes", Plugin.AllocationTiming.Value && Instrumentation.AllocationSupported ? (object)(Interlocked.Read(ref p.Allocated) - p.EpochAllocated) : null));
            }
            methods.Sort((a,b) => ((double)b["main_self_ms"]).CompareTo((double)a["main_self_ms"]));
            var coverage = new List<object>();
            foreach (var s in Instrumentation.Scopes) coverage.Add(D("system", s.Name, "enabled", s.Enabled, "trace", s.Trace, "installed", s.Patched, "failed", s.Failed, "entry_points", s.Eligible));
            var jobs = new List<object>(); foreach (var j in Measurements.Jobs) jobs.Add(D("system", j.System, "job", j.Name, "phase", j.Phase, "enabled", j.Enabled, "ms_per_second_last_snapshot", j.Milliseconds, "calls_per_second_last_snapshot", j.Calls, "max_ms_lifetime", j.Maximum, "errors_lifetime", j.Errors));
            var counters = new Dictionary<string, object>(); foreach (var c in Measurements.Engine) counters.Add(c.Name, c.Display);
            var plugins = new List<object>(); foreach (var p in Chainloader.PluginInfos) plugins.Add(D("guid", p.Key, "version", p.Value.Metadata.Version.ToString(), "location", p.Value.Location));
            var player = GameRefs.Player; var pos = player == null ? Vector3.zero : player.transform.position;
            var warnings = new List<string>();
            if (seconds < 30) warnings.Add("Short capture: collect at least 30 seconds after warm-up.");
            if (Measurements.PausedFrames > 0) warnings.Add("Paused frames included; steady-play comparison is contaminated.");
            if (Measurements.DashboardFrames > 0) warnings.Add("Developer dashboard was visible for " + Measurements.DashboardFrames + " frames; GUI overhead is included.");
            if (WindowEvents.Count > 1) warnings.Add("Window has state transitions; inspect window_events before treating it as steady gameplay.");
            if (_coverageAtReset != CoverageSignature()) warnings.Add("Probe coverage/allocation mode differs from reset; method totals have inconsistent coverage.");
            if (Measurements.Count < Measurements.Samples) warnings.Add("Frame percentiles cover the rolling last 8192 frames; method totals cover the whole reset window.");
            if (Instrumentation.PendingCount > 0) warnings.Add("Hooks are still installing; reset after installation.");
            if (Instrumentation.Probes.Count == 0) warnings.Add("No method probes installed: scheduler/counters cannot attribute all WW or native work.");
            if (Instrumentation.Fault != null) warnings.Add("Instrumentation stopped: " + Instrumentation.Fault);
            if (!runtime.Recording) warnings.Add("Capture stopped; settings/context may be newer than samples.");
            var changes = Differences(_startSettings, settings); if (changes.Count > 0) warnings.Add("Settings changed during the sample window.");
            return D("schema", "WildWastelandDeveloper.capture.v1", "utc", DateTime.UtcNow.ToString("O"), "scenario", Scenario,
                "context", Measurements.Context, "scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                "player_position", new object[] { pos.x, pos.y, pos.z }, "performance_module", performance,
                "hardware", D("cpu", SystemInfo.processorType, "logical_processors", SystemInfo.processorCount, "ram_mb", SystemInfo.systemMemorySize, "gpu", SystemInfo.graphicsDeviceName, "vram_mb", SystemInfo.graphicsMemorySize, "resolution", Screen.width + "x" + Screen.height, "vsync", QualitySettings.vSyncCount, "target_fps", Application.targetFrameRate, "quality", QualitySettings.GetQualityLevel()),
                "frame", D("recorded_seconds", seconds, "total_frames", Measurements.Samples, "rolling_samples", Measurements.Count, "paused_frames", Measurements.PausedFrames, "average_ms", Measurements.Average, "p95_ms", Measurements.P95, "p99_ms", Measurements.P99, "worst_ms", Measurements.Worst),
                "gc", D("managed_bytes", Measurements.ManagedBytes, "collections_gen0", Measurements.Gc0, "collections_gen1", Measurements.Gc1, "collections_gen2", Measurements.Gc2),
                "active_fsm_count", PlayMakerFSM.FsmList.Count, "developer_update_gui_ms_per_frame", Measurements.OwnMilliseconds,
                "instrumentation", D("timing_active", Instrumentation.Timing, "pending", Instrumentation.PendingCount, "fault", Instrumentation.Fault, "allocation_enabled_at_export", Plugin.AllocationTiming.Value, "dropped_trace_events_lifetime", Instrumentation.Dropped, "coverage", coverage, "failures", Instrumentation.Failures.ToArray()),
                "modules", modules, "settings", settings, "settings_changed_since_reset", changes, "plugins", plugins, "window_events", WindowEvents.ToArray(),
                "manual_collection_snapshot", Dashboard.Counters.ToArray(),
                "engine_counters_latest", counters, "methods_since_reset", methods, "scheduler_latest_snapshot", jobs, "warnings", warnings);
        }
        private static double Number(Dictionary<string, object> d, string key) { return Convert.ToDouble(d[key], CultureInfo.InvariantCulture); }
        internal static void AddFiles(Dictionary<string, string> files, Runtime runtime)
        {
            var current = Capture(runtime); files.Add("capture.json", Json(current));
            var b = new StringBuilder("# WW performance evidence\n\nRead this file, capture.json and builds.txt first. Raw CSVs support deeper inspection.\n\n");
            b.AppendLine("Scenario: " + Scenario).AppendLine("Performance: " + current["performance_module"]).AppendLine("Context: " + current["context"]);
            var frame = (Dictionary<string, object>)current["frame"];
            b.AppendLine("\nFrame ms avg / p95 / p99 / worst: " + frame["average_ms"] + " / " + frame["p95_ms"] + " / " + frame["p99_ms"] + " / " + frame["worst_ms"]);
            b.AppendLine("Window seconds: " + frame["recorded_seconds"] + "; active FSMs: " + current["active_fsm_count"]);
            foreach (var w in (List<string>)current["warnings"]) b.AppendLine("- LIMITATION: " + w);
            b.AppendLine("\nLargest observed main-thread managed costs since reset (candidates, not proven causes):");
            double seconds = Math.Max(.001, Number(frame, "recorded_seconds"));
            foreach (var m in ((List<Dictionary<string, object>>)current["methods_since_reset"]).Take(20))
                b.AppendLine("- " + m["system"] + " | " + m["method"] + " | main self " + (Number(m,"main_self_ms") / seconds).ToString("F2", CultureInfo.InvariantCulture) + " ms/s | calls " + m["calls"] + " | max " + m["max_call_ms"] + " ms | errors " + m["errors"]);
            b.AppendLine("\nInterpretation rules: inclusive method/job times overlap; never sum them as frame cost. Self subtracts instrumented children only. Worker time is separate. Native engine, graphics drivers, waits and uncovered code remain unattributed. Do not label the remainder base-game CPU cost. Engine counters are latest samples, not window averages. Missing counters mean unavailable. Detailed hooks and open UI add overhead. Coroutine/async MoveNext measures execution segments, not awaited wall time. An off Performance module is still WW; it is not a vanilla baseline. A true WW-disabled run requires an external profiler because this plugin depends on WW.");
            b.AppendLine("\nNext diagnosis: inspect warnings and coverage first; correlate slow methods with trace.log, errors, queue/cache counts and engine counters; reproduce with matching instrumentation/settings and a focused system. Use clean steady-play windows separately from terrain/load/save spikes.");
            files.Add("analysis.md", b.ToString());
            if (_baseline == null) return;
            files.Add("baseline-A.json", Json(_baseline));
            var comparison = new StringBuilder("# Observational A/B comparison\n\nA: " + _baseline["scenario"] + "\nB: " + current["scenario"] + "\n\nPerformance A: " + _baseline["performance_module"] + "\nPerformance B: " + current["performance_module"] + "\n");
            var af = (Dictionary<string, object>)_baseline["frame"];
            foreach (string key in new[] { "average_ms", "p95_ms", "p99_ms", "worst_ms" }) comparison.AppendLine(key + ": A=" + af[key] + ", B=" + frame[key] + ", B-A=" + (Number(frame,key)-Number(af,key)).ToString("F3", CultureInfo.InvariantCulture));
            comparison.AppendLine("\nSettings differences:"); foreach (string diff in Differences((Dictionary<string,string>)_baseline["settings"], (Dictionary<string,string>)current["settings"])) comparison.AppendLine("- " + diff);
            comparison.AppendLine("\nComparability checks:");
            foreach (string key in new[] { "scene", "hardware", "instrumentation", "plugins" }) if (Json(_baseline[key]) != Json(current[key])) comparison.AppendLine("- " + key + " differs; inspect both JSON files.");
            comparison.AppendLine("Compare player positions and action manually. Check BOTH captures' warnings, warm-up, scene population, camera and timing settings. Changed world activity or hook coverage can explain differences. This comparison establishes observations, not causation.");
            files.Add("comparison.md", comparison.ToString());
        }
        internal static string Json(object value)
        {
            if (value == null) return "null";
            string s = value as string; if (s != null) { var b = new StringBuilder("\""); foreach (char c in s) { switch (c) { case '"': b.Append("\\\""); break; case '\\': b.Append("\\\\"); break; case '\n': b.Append("\\n"); break; case '\r': b.Append("\\r"); break; case '\t': b.Append("\\t"); break; default: if (c < 32) b.Append("\\u" + ((int)c).ToString("x4")); else b.Append(c); break; } } return b.Append('"').ToString(); }
            if (value is bool) return (bool)value ? "true" : "false";
            var dict = value as IDictionary; if (dict != null) { var entries = new List<string>(); foreach (DictionaryEntry pair in dict) entries.Add(Json(pair.Key.ToString()) + ": " + Json(pair.Value)); return "{\n" + string.Join(",\n", entries) + "\n}"; }
            var list = value as IEnumerable; if (list != null) { var entries = new List<string>(); foreach (var item in list) entries.Add(Json(item)); return "[" + string.Join(",\n", entries) + "]"; }
            double number = Convert.ToDouble(value, CultureInfo.InvariantCulture); return double.IsNaN(number) || double.IsInfinity(number) ? "null" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
