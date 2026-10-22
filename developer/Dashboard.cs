using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace WildWastelandDeveloper
{
    internal static class Dashboard
    {
        private static Rect _window = new Rect(30, 35, 1080, 740);
        private static readonly GUI.WindowFunction Window = DrawWindow;
        private static readonly string[] Tabs = { "Overview", "Scheduler", "Detailed calls", "Systems & tracing", "Counters", "Logs & capture", "Development tools" };
        private static Runtime _runtime;
        private static int _tab;
        private static Vector2 _scroll;
        private static string _filter = "";
        private static int _selected;
        private static bool _deep;
        internal static readonly List<string> Counters = new List<string>();
        private static readonly List<WildWasteland.Core.UI.ToolPage> Tools = new List<WildWasteland.Core.UI.ToolPage>();
        private static GUIStyle _mono, _heading;
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        internal static void Draw(Runtime runtime)
        {
            _runtime = runtime;
            if (_mono == null) { _mono = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false }; _heading = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold }; }
            _window.width = Mathf.Min(1080, Screen.width - 20); _window.height = Mathf.Min(740, Screen.height - 20);
            _window.x = Mathf.Clamp(_window.x, 0, Mathf.Max(0, Screen.width - _window.width)); _window.y = Mathf.Clamp(_window.y, 0, Mathf.Max(0, Screen.height - _window.height));
            int depth = GUI.depth; GUI.depth = -950;
            _window = GUI.Window(0x574457, _window, Window, "WILD WASTELAND DEVELOPER  |  " + Plugin.MenuKey.Value);
            GUI.depth = depth;
        }
        private static void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            bool record = GUILayout.Toggle(_runtime.Recording, "Monitor / capture", GUILayout.Width(155));
            if (record != _runtime.Recording) { _runtime.Recording = record; if (record) Measurements.Reset(); }
            if (GUILayout.Button("Reset sample window", GUILayout.Width(165))) Measurements.Reset();
            if (GUILayout.Button("Export capture", GUILayout.Width(135))) Reports.Export(_runtime);
            if (GUILayout.Button("WW development tools", GUILayout.Width(170))) _runtime.OpenTools();
            if (GUILayout.Button("Close", GUILayout.Width(65))) _runtime.Close();
            GUILayout.EndHorizontal();
            GUILayout.Label(Measurements.Context, _mono);
            GUILayout.Label(_runtime.Status, GUILayout.Height(24));
            int tab = GUILayout.Toolbar(_tab, Tabs); if (tab != _tab) { _tab = tab; _scroll = Vector2.zero; }
            GUILayout.BeginHorizontal(); GUILayout.Label("Filter", GUILayout.Width(40)); _filter = GUILayout.TextField(_filter); GUILayout.EndHorizontal();
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: Overview(); break;
                case 1: Jobs(); break;
                case 2: Methods(); break;
                case 3: Systems(); break;
                case 4: CounterView(); break;
                case 5: Logs(); break;
                case 6: DevelopmentTools(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.Label("Live menu does not pause the world. Input is held while open; close it to play while capturing. Timing is disabled during hook installation.");
            GUI.DragWindow(new Rect(0, 0, _window.width, 20));
        }
        private static void Overview()
        {
            GUILayout.Label("Frame pacing", _heading);
            GUILayout.Label("FPS " + (Measurements.Average <= 0 ? "n/a" : (1000 / Measurements.Average).ToString("0.0", C)) + "   avg " + F(Measurements.Average) + " ms   p95 " + F(Measurements.P95) + " ms   p99 " + F(Measurements.P99) + " ms   worst " + F(Measurements.Worst) + " ms", _mono);
            Rect chart = GUILayoutUtility.GetRect(200, 125, GUILayout.ExpandWidth(true));
            Fill(chart, new Color(.06f, .07f, .09f));
            float scale = Mathf.Max(50, Mathf.Min(250, Measurements.Worst));
            int bars = Math.Min(240, Measurements.Count); float width = chart.width / Math.Max(1, bars);
            for (int i = 0; i < bars; i++)
            {
                int index = (Measurements.Head - bars + i + Measurements.Capacity) % Measurements.Capacity; float ms = Measurements.Frames[index];
                float height = Mathf.Clamp01(ms / scale) * chart.height;
                Fill(new Rect(chart.x + i * width, chart.yMax - height, Mathf.Max(1, width - 1), height), ms > 33.34f ? new Color(1, .35f, .25f) : ms > 16.67f ? new Color(1, .7f, .2f) : new Color(.2f, .8f, .65f));
            }
            Fill(new Rect(chart.x, chart.yMax - 16.67f / scale * chart.height, chart.width, 1), Color.green);
            Fill(new Rect(chart.x, chart.yMax - 33.34f / scale * chart.height, chart.width, 1), Color.yellow);
            GUILayout.Label("Most recent " + bars + " frames; green line = 60 FPS budget, yellow = 30 FPS. Statistics use up to " + Measurements.Capacity + " frames; menus/loading are included.");
            GUILayout.Label("Profiler counters", _heading);
            foreach (var counter in Measurements.Engine) GUILayout.Label(counter.Name + ": " + counter.Display, _mono);
            GUILayout.Label("Managed memory " + (Measurements.ManagedBytes / 1048576.0).ToString("0.0", C) + " MiB   GC collections since reset " + Measurements.Gc0 + "/" + Measurements.Gc1 + "/" + Measurements.Gc2, _mono);
            GUILayout.Label("Developer update + GUI average: " + F(Measurements.OwnMilliseconds) + " ms/frame. Instrumentation overhead is additional.");
            GUILayout.Label("Scheduler cost by system (observed ms/second, overlapping/nested calls are not an extra total)", _heading);
            foreach (var pair in Measurements.SystemCosts) if (Matches(pair.Key)) Bar(pair.Key, pair.Value, Math.Max(1, Measurements.SchedulerMsPerSecond));
        }
        private static void Jobs()
        {
            GUILayout.Label("All scheduler jobs, sampled each second", _heading);
            GUILayout.Label("ms/s = observed elapsed time per second. Average = per call. Maximum/errors are WW job lifetime values.");
            foreach (var row in Measurements.Jobs)
            {
                if (!Matches(row.System + "." + row.Name)) continue;
                GUILayout.Label(row.System + "." + row.Name + " [" + row.Phase + "] " + (row.Enabled ? "" : "DISABLED ") + "  " + F(row.Milliseconds) + " ms/s   " + F(row.Calls) + " calls/s   avg " + F(row.Average) + "ms   max " + F(row.Maximum) + "ms   errors " + row.Errors, _mono);
                Bar("", row.Milliseconds, Math.Max(1, Measurements.SchedulerMsPerSecond));
            }
        }
        private static void Methods()
        {
            GUILayout.Label("Calls outside and inside scheduler jobs", _heading);
            GUILayout.Label(Measurements.Coverage);
            GUILayout.Label("Self time subtracts instrumented child calls only. Inclusive times overlap. Worker time is not main-thread frame time.");
            GUILayout.Label("Patch failures and uninstrumented methods are coverage gaps. Very small/inlined methods may evade hooks. Native work is seen only through callers/counters.");
            foreach (var row in Measurements.Methods)
            {
                if (!Matches(row.Name)) continue;
                GUILayout.Label(row.Name, _mono);
                GUILayout.Label("   " + F(row.Calls) + " calls/s   self " + F(row.Self) + " ms/s   incl " + F(row.Inclusive) + " ms/s   main/worker " + F(row.Main) + "/" + F(row.Worker)
                    + " ms/s   max " + F(row.Maximum) + " ms   errors/snapshot " + row.Errors + (Instrumentation.AllocationSupported && Plugin.AllocationTiming.Value ? "   alloc " + F(row.Allocated / 1024) + " KiB/s (inclusive)" : "   allocations: off/n/a"), _mono);
                Bar("", row.Self, Measurements.Methods.Count == 0 ? 1 : Math.Max(1, Measurements.Methods[0].Self));
            }
        }
        private static void Systems()
        {
            GUILayout.Label("Per-system timing and independent logging", _heading);
            GUILayout.Label("Installing Harmony hooks can hitch. Install at the title/pause menu, wait for the queue to finish, then reset the sample window.");
            _deep = GUILayout.Toggle(_deep, "Deep mode: all eligible managed methods in selected system (higher overhead; use a focused system)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Install entry points: ALL systems")) { foreach (var scope in Instrumentation.Scopes) Instrumentation.Enable(scope, false); _runtime.Status = "Installing one method per frame; measurements resume when complete."; }
            if (GUILayout.Button("Remove all timing hooks")) { Instrumentation.Remove(); _runtime.Status = "Detailed hooks removed; low-overhead overview remains."; }
            GUILayout.EndHorizontal();
            GUILayout.Label(Measurements.Coverage);
            if (GUILayout.Button("Discover optional BASE GAME probes (FSM / terrain / save / scripts)")) Instrumentation.DiscoverNative();
            foreach (var scope in Instrumentation.Scopes)
            {
                if (!Matches(scope.Name)) continue;
                GUILayout.BeginHorizontal();
                scope.Enabled = GUILayout.Toggle(scope.Enabled, scope.Name, GUILayout.Width(150));
                scope.Trace = GUILayout.Toggle(scope.Trace, "Trace", GUILayout.Width(75));
                if (GUILayout.Button("Install " + (_deep ? "deep" : "entry points"), GUILayout.Width(150))) { Instrumentation.Enable(scope, _deep); _runtime.Status = "Installing " + scope.Name + " hooks; reset window after installation."; }
                GUILayout.Label(scope.Patched + " patched / " + scope.Eligible + " entry / " + scope.Methods.Count + " managed; failures " + scope.Failed);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Module state", _heading); foreach (var state in Measurements.ModuleStates) if (Matches(state)) GUILayout.Label(state, _mono);
            foreach (var failure in Instrumentation.Failures) if (Matches(failure)) GUILayout.Label("PATCH FAILURE: " + failure, _mono);
        }
        private static void CounterView()
        {
            GUILayout.Label("Collection / queue / cache sizes", _heading);
            GUILayout.Label("Manual reflection snapshot of static collections; no scene hierarchy scan. Counts are structural clues, not milliseconds.");
            if (Instrumentation.Scopes.Count == 0) return;
            string[] names = Instrumentation.ScopeNames;
            _selected = GUILayout.SelectionGrid(Math.Min(_selected, names.Length - 1), names, 5);
            if (GUILayout.Button("Refresh selected system counters")) RefreshCounters(Instrumentation.Scopes[_selected]);
            foreach (string row in Counters) if (Matches(row)) GUILayout.Label(row, _mono);
        }
        private static void RefreshCounters(Scope scope)
        {
            Counters.Clear();
            foreach (var field in scope.Counters)
            {
                try { var value = field.GetValue(null) as ICollection; Counters.Add(field.DeclaringType.FullName + "." + field.Name + " = " + (value == null ? "null" : value.Count.ToString(C))); }
                catch (Exception e) { Counters.Add(field.DeclaringType.FullName + "." + field.Name + " = unavailable: " + e.GetBaseException().Message); }
            }
        }
        private static void Logs()
        {
            GUILayout.Label("LLM diagnosis / controlled A-B captures", _heading);
            Diagnosis.Scenario = GUILayout.TextField(Diagnosis.Scenario);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture A: wait 5s, play 30s, export + retain baseline")) _runtime.ScheduleCapture(true);
            if (GUILayout.Button("Capture B: wait 5s, play 30s, export comparison")) _runtime.ScheduleCapture(false);
            GUILayout.EndHorizontal();
            GUILayout.Label("Label the location/action. Finish hook installation, reset, close F4, play 30-60 seconds, then capture. Change Performance yourself between A and B; use the same route, camera and hooks.");
            if (GUILayout.Button("Keep current window as baseline A")) { Diagnosis.KeepBaseline(_runtime); _runtime.Status = "Baseline A retained in memory; reset before collecting B."; }
            GUILayout.Label(Diagnosis.BaselineLabel);
            GUILayout.Label("Logging is owned by WildWastelandDeveloper; WW verbose stays unchanged", _heading);
            Plugin.Summaries.Value = GUILayout.Toggle(Plugin.Summaries.Value, "Periodic frame / scheduler / top-call summaries");
            Plugin.SpikeLog.Value = GUILayout.Toggle(Plugin.SpikeLog.Value, "Slow-call logs for systems with Trace checked");
            Plugin.LifecycleLog.Value = GUILayout.Toggle(Plugin.LifecycleLog.Value, "Save / load / terrain / spawn lifecycle logs for systems with Trace checked");
            Plugin.AllocationTiming.Value = GUILayout.Toggle(Plugin.AllocationTiming.Value, "Thread allocation measurements: " + (Instrumentation.AllocationSupported ? "available (adds overhead)" : "unavailable on this runtime"));
            GUILayout.Label("Slow-call threshold " + F(Plugin.SpikeThreshold.Value) + " ms"); Plugin.SpikeThreshold.Value = GUILayout.HorizontalSlider(Plugin.SpikeThreshold.Value, .1f, 50);
            GUILayout.Label("Summary interval " + F(Plugin.LogInterval.Value) + " seconds"); Plugin.LogInterval.Value = GUILayout.HorizontalSlider(Plugin.LogInterval.Value, 1, 60);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Write summary now")) _runtime.WriteSummary();
            if (GUILayout.Button("Export capture")) Reports.Export(_runtime);
            if (GUILayout.Button(_runtime.Rebinding ? "Press a key; Esc cancels" : "Rebind " + Plugin.MenuKey.Value)) _runtime.Rebinding = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("Trace queue is bounded; dropped events: " + Instrumentation.Dropped + ". Errors from instrumented calls are always retained. Last 512 lines kept for export.");
            foreach (var line in _runtime.Trace) if (Matches(line)) GUILayout.Label(line, _mono);
        }
        private static void DevelopmentTools()
        {
            GUILayout.Label("Existing WW editors, bakers and diagnostic pages", _heading);
            GUILayout.Label("Open hands control to the existing WW tool UI and enables its Debug/Developer visibility flags. It never enables global verbose logging.");
            if (GUILayout.Button("Load / refresh development tool catalogue"))
            {
                Tools.Clear();
                foreach (var entry in WildWasteland.Core.ModuleHost.Entries)
                {
                    try
                    {
                        var pages = entry.Module.ToolPages(); if (pages == null) continue;
                        foreach (var page in pages) { if (page.Owner == null) page.Owner = entry.Module; Tools.Add(page); }
                    }
                    catch (Exception e) { _runtime.Status = "Tool catalogue: " + entry.Module.Id + ": " + e.GetBaseException().Message; }
                }
            }
            foreach (var page in Tools)
            {
                string name = (page.Owner == null ? "Core" : page.Owner.Id) + " / " + page.Group + " / " + page.Title;
                if (!Matches(name)) continue;
                GUILayout.BeginHorizontal(); GUILayout.Label(name);
                if (GUILayout.Button("Open", GUILayout.Width(85))) _runtime.OpenTool(page);
                GUILayout.EndHorizontal();
            }
        }
        private static bool Matches(string value) { return _filter.Length == 0 || value.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0; }
        private static string F(double value) { return value.ToString("0.###", C); }
        private static void Bar(string label, double value, double maximum)
        {
            Rect rect = GUILayoutUtility.GetRect(100, string.IsNullOrEmpty(label) ? 5 : 22, GUILayout.ExpandWidth(true));
            Fill(rect, new Color(.1f, .13f, .17f)); Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01((float)(value / maximum)), rect.height), new Color(.13f, .45f, .5f));
            if (label.Length > 0) GUI.Label(rect, label + "  " + F(value) + " ms/s", _mono);
        }
        private static void Fill(Rect rect, Color color) { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
    }
}
