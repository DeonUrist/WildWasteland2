using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Debug
{
    internal sealed class DebugRuntime
    {
        internal static DebugRuntime Instance; internal static string SceneMarker;
        internal readonly EditorSession Session = new EditorSession();
        internal bool Visible, Recording = true, Rebinding;
        private float _captureStart = -1, _captureEnd = -1;
        private bool _captureBaseline;
        /// <summary>Closes the dashboard, waits five seconds and then records a thirty-second A/B sample.</summary>
        internal void ScheduleCapture(bool baseline)
        {
            _captureBaseline = baseline; _captureStart = Time.realtimeSinceStartup + 5; _captureEnd = -1;
            Close(); Status = "Capture starts in 5 seconds; then records 30 seconds and exports automatically."; Core.Log.Main(DebugModule.Instance, Status);
        }
        /// <summary>Starts a labeled bounded capture immediately and refuses overlapping or invalid durations.</summary>
        internal bool StartCapture(string label, float seconds)
        {
            if (Capturing || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) return false;
            Diagnosis.Scenario = label ?? "Capture";
            _captureBaseline = false; _captureStart = -1;
            Measurements.Reset(); Recording = true;
            _lastSnapshot = Time.realtimeSinceStartup; _snapshotAt = _lastSnapshot + 1;
            _captureEnd = _lastSnapshot + seconds;
            Close();
            return true;
        }
        internal string Status = "Overview active; detailed instrumentation is opt-in.";
        internal readonly List<string> Trace = new List<string>();
        private float _snapshotAt, _lastSnapshot, _summaryAt;
        private long _ownTicks; private int _ownFrames;
        private bool _openTools;
        private ToolPage _toolsPage;
        private string _lastFault;
        /// <summary>Initializes shared measurement/probe catalogs once after chainloading on the Core scheduler.</summary>
        private void Initialize()
        {
            try
            {
                Instance = this;
                Instrumentation.Initialize(); Measurements.Initialize();
                _lastSnapshot = Time.realtimeSinceStartup; _snapshotAt = _lastSnapshot + 1;
                Core.Log.Main(DebugModule.Instance, "Debug performance dashboard ready; shared log policy active. Detailed timing starts only on request.");
            }
            catch (Exception e) { Status = "Initialization failed: " + e.GetBaseException().Message; Core.Log.Error(DebugModule.Instance, "Debug initialization failed", e); }
        }
        private bool _initialized;
        internal bool Capturing { get { return _captureStart >= 0 || _captureEnd >= 0; } }
        /// <summary>Runs Debug's measurement/capture lifecycle once per scheduled frame; probes remain opt-in and bounded.</summary>
        internal void Tick()
        {
            if (!_initialized) { _initialized = true; Initialize(); return; }
            Measurements.SetMeasuring(Visible || Capturing);
            long begin = Stopwatch.GetTimestamp();
            Instrumentation.Frame = Time.frameCount;
            Measurements.Frame();
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (Rebinding)
                {
                    foreach (var key in keyboard.allKeys) if (key.wasPressedThisFrame) { if (key.keyCode != Key.Escape) DebugModule.MenuKey.Value = key.keyCode; Rebinding = false; break; }
                }
                else if (Visible && keyboard.escapeKey.wasPressedThisFrame) Close();
            }
            if (Visible && !Session.Open) Visible = false;
            if (Visible) Session.Maintain();
            if (_openTools && !EditorSession.Busy) { _openTools = false; ToolWindow.Open(_toolsPage); _toolsPage = null; }
            Instrumentation.Timing = Core.Log.L2 && Recording && Instrumentation.PendingCount == 0 && Instrumentation.Fault == null;
            Instrumentation.Allocations = DebugModule.AllocationTiming.Value && Instrumentation.AllocationSupported;
            Instrumentation.Spikes = Core.Log.L2; Instrumentation.Lifecycle = Core.Log.L2; Instrumentation.Threshold = DebugModule.SpikeThreshold.Value;
            Instrumentation.InstallStep();
            if (Instrumentation.Fault != null && Instrumentation.Fault != _lastFault) { _lastFault = Instrumentation.Fault; Status = "Detailed timing stopped safely: " + _lastFault; Core.Log.Error(DebugModule.Instance, Status); }
            TraceEvent ev; int drained = 0;
            while (drained++ < 32 && Instrumentation.Dequeue(out ev))
            {
                string line = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " frame=" + ev.Frame + " thread=" + ev.Thread + " " + ev.Method
                    + " " + (ev.Ticks * 1000.0 / Stopwatch.Frequency).ToString("0.###", CultureInfo.InvariantCulture) + "ms" + (ev.Error == null ? "" : " exception=" + ev.Error);
                AddTrace(line); Core.Log.Main(DebugModule.Instance, line);
            }
            if (SceneMarker != null)
            {
                string line = "Scene: " + SceneMarker; SceneMarker = null; AddTrace(line);
                if (Core.Log.L2) Core.Log.Main(DebugModule.Instance, line);
            }
            float now = Time.realtimeSinceStartup;
            if (now >= _snapshotAt)
            {
                double seconds = Math.Max(.001, now - _lastSnapshot);
                if (Recording) Measurements.Snapshot(seconds);
                Measurements.OwnMilliseconds = _ownFrames == 0 ? 0 : _ownTicks * 1000.0 / Stopwatch.Frequency / _ownFrames;
                _ownTicks = 0; _ownFrames = 0; _lastSnapshot = now; _snapshotAt = now + 1;
                if (Recording && Core.Log.L1 && now >= _summaryAt) { _summaryAt = now + 30f; WriteSummary(); }
            }
            Reports.Poll(this);
            if (_captureStart >= 0 && now >= _captureStart)
            {
                _captureStart = -1; Measurements.Reset(); Recording = true; _lastSnapshot = now; _snapshotAt = now + 1; _captureEnd = now + 30;
                Status = "Recording timed 30-second capture; keep playing.";
            }
            if (_captureEnd >= 0 && now >= _captureEnd)
            {
                _captureEnd = -1; Measurements.Snapshot(Math.Max(.001, now - _lastSnapshot));
                Reports.Export(this); if (_captureBaseline) Diagnosis.KeepBaseline(this);
                Recording = false; Instrumentation.Timing = false;
            }
            _ownTicks += Stopwatch.GetTimestamp() - begin; _ownFrames++;
        }
        /// <summary>Draws the dashboard only while its Core editor session is open and tracks its own GUI cost.</summary>
        internal void Draw()
        {
            if (!Visible) return; long begin = Stopwatch.GetTimestamp();
            Dashboard.Draw(this); _ownTicks += Stopwatch.GetTimestamp() - begin;
        }
        /// <summary>Acquires the non-pausing editor session or closes the dashboard without changing gameplay modules.</summary>
        internal void Toggle()
        {
            if (Visible) { Close(); return; }
            if (Session.Acquire(false, true)) Visible = true;
            else { Status = "Close the other editor/menu first."; Core.Log.Main(DebugModule.Instance, Status); }
        }
        /// <summary>Closes the dashboard session and persists its Debug measurement settings.</summary>
        internal void Close() { Visible = false; Rebinding = false; Session.Close(); DebugModule.SettingsFile.Save(); }
        /// <summary>Closes the dashboard and queues the shared tool window for the next unowned editor frame.</summary>
        internal void OpenTools() { Close(); _openTools = true; }
        /// <summary>Queues a selected tool page and enables its existing Debug/Developer visibility gates.</summary>
        internal void OpenTool(ToolPage page)
        {
            WildWasteland.Plugin.EnableDebug.Value = true; WildWasteland.Plugin.DeveloperMode.Value = true;
            _toolsPage = page; Close(); _openTools = true;
        }
        /// <summary>Retains the latest 512 diagnostic lines for capture export.</summary>
        internal void AddTrace(string line) { if (Trace.Count >= 512) Trace.RemoveAt(0); Trace.Add(line); }
        /// <summary>Writes current frame/job/probe summaries through the shared log policy.</summary>
        internal void WriteSummary()
        {
            var c = CultureInfo.InvariantCulture;
            string line = "PERF " + Measurements.Context + " | frame avg/p95/p99 " + Measurements.Average.ToString("0.0", c) + "/" + Measurements.P95.ToString("0.0", c) + "/" + Measurements.P99.ToString("0.0", c)
                + "ms | scheduler " + Measurements.SchedulerMsPerSecond.ToString("0.0", c) + "ms/s | managed " + (Measurements.ManagedBytes / 1048576.0).ToString("0.0", c) + "MiB | GC " + Measurements.Gc0 + "/" + Measurements.Gc1 + "/" + Measurements.Gc2;
            Core.Log.Main(DebugModule.Instance, line); AddTrace(line);
            for (int i = 0; i < Math.Min(8, Measurements.Methods.Count); i++)
            {
                var row = Measurements.Methods[i]; if (row.Calls <= 0) continue;
                string detail = "PERF-CALL " + row.Name + " calls/s=" + row.Calls.ToString("0.0", c) + " self/main/worker ms/s=" + row.Self.ToString("0.###", c) + "/" + row.Main.ToString("0.###", c) + "/" + row.Worker.ToString("0.###", c);
                Core.Log.Main(DebugModule.Instance, detail); AddTrace(detail);
            }
            for (int i = 0; i < Math.Min(5, Measurements.Jobs.Count); i++) { var job = Measurements.Jobs[i]; string detail = "PERF-JOB " + job.System + "." + job.Name + " ms/s=" + job.Milliseconds.ToString("0.###", c); Core.Log.Main(DebugModule.Instance, detail); AddTrace(detail); }
        }
        private bool _disposed;
        /// <summary>Releases the editor session, optional probes and engine counters on Debug shutdown.</summary>
        internal void Dispose()
        {
            if (_disposed) return; _disposed = true; Session.Close(true);
            try { Instrumentation.Remove(); } catch (Exception e) { Core.Log.Warn(DebugModule.Instance, e.Message); }
            Measurements.Dispose(); DebugModule.SettingsFile.Save(); Instance = null;
        }
    }
}
