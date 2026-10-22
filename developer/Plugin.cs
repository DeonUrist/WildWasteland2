using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WildWasteland.Core.UI;

namespace WildWastelandDeveloper
{
    [BepInPlugin(Guid, "Wild Wasteland Developer", "0.1.0")]
    [BepInDependency("com.denis.apocalypter.wildwasteland", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.denis.apocalypter.wildwastelanddeveloper";
        internal static ManualLogSource Log;
        internal static ConfigEntry<Key> MenuKey;
        internal static ConfigEntry<bool> Summaries, SpikeLog, LifecycleLog, AllocationTiming;
        internal static ConfigEntry<float> LogInterval, SpikeThreshold;
        internal static ConfigFile Settings;
        private void Awake()
        {
            Log = Logger; Settings = Config;
            Config.SaveOnConfigSet = false;
            MenuKey = Config.Bind("Menu", "Key", Key.F4, "Open the standalone, non-pausing performance dashboard.");
            Summaries = Config.Bind("Logging", "Periodic summaries", false, "Write targeted developer summaries without enabling WW verbose logging.");
            SpikeLog = Config.Bind("Logging", "Method spikes", false, "Log slow instrumented calls for systems with Trace enabled.");
            LifecycleLog = Config.Bind("Logging", "Lifecycle events", false, "Log instrumented save/load/terrain/spawn events for systems with Trace enabled.");
            AllocationTiming = Config.Bind("Capture", "Thread allocation timing", false, "Optional allocation deltas per instrumented call; unavailable runtimes show n/a.");
            LogInterval = Config.Bind("Logging", "Summary interval seconds", 5f, new ConfigDescription("Summary interval, independent of WW verbose.", new AcceptableValueRange<float>(1, 60)));
            SpikeThreshold = Config.Bind("Logging", "Slow call milliseconds", 2f, new ConfigDescription("Slow-call threshold.", new AcceptableValueRange<float>(.1f, 1000)));
            SceneManager.sceneLoaded += Loaded;
            Config.Save();
        }
        private void Start() { Runtime.Ensure(); }
        private static void Loaded(Scene scene, LoadSceneMode mode) { Runtime.Ensure(); Runtime.SceneMarker = scene.name; }
        internal static void Unhook() { SceneManager.sceneLoaded -= Loaded; }
    }

    [DefaultExecutionOrder(-9000)]
    internal sealed class Runtime : MonoBehaviour
    {
        internal static Runtime Instance; internal static string SceneMarker;
        internal readonly EditorSession Session = new EditorSession();
        private Func<bool, bool, bool> _acquire;
        internal bool Visible, Recording = true, Rebinding;
        private float _captureStart = -1, _captureEnd = -1;
        private bool _captureBaseline;
        internal void ScheduleCapture(bool baseline)
        {
            _captureBaseline = baseline; _captureStart = Time.realtimeSinceStartup + 5; _captureEnd = -1;
            Close(); Status = "Capture starts in 5 seconds; then records 30 seconds and exports automatically."; Plugin.Log.LogInfo(Status);
        }
        internal string Status = "Overview active; detailed instrumentation is opt-in.";
        internal readonly List<string> Trace = new List<string>();
        private float _snapshotAt, _lastSnapshot, _summaryAt;
        private long _ownTicks; private int _ownFrames;
        private bool _openTools;
        private ToolPage _toolsPage;
        private string _lastFault;
        internal static void Ensure()
        {
            if (Instance != null) return;
            var host = new GameObject("WildWastelandDeveloper.Runtime"); host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host); Instance = host.AddComponent<Runtime>();
        }
        private void Start()
        {
            try
            {
                Instrumentation.Initialize(); Measurements.Initialize();
                var method = typeof(EditorSession).GetMethod("Acquire", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool), typeof(bool) }, null);
                if (method != null) _acquire = (Func<bool, bool, bool>)Delegate.CreateDelegate(typeof(Func<bool, bool, bool>), Session, method);
                _lastSnapshot = Time.realtimeSinceStartup; _snapshotAt = _lastSnapshot + 1;
                Plugin.Log.LogInfo("F4 performance dashboard ready; separate logging; WW verbose unchanged. Detailed timing starts only on request.");
            }
            catch (Exception e) { Status = "Initialization failed: " + e.GetBaseException().Message; Plugin.Log.LogError(e); }
        }
        private void Update()
        {
            long begin = Stopwatch.GetTimestamp();
            Instrumentation.Frame = Time.frameCount;
            if (Recording) Measurements.Frame();
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (Rebinding)
                {
                    foreach (var key in keyboard.allKeys) if (key.wasPressedThisFrame) { if (key.keyCode != Key.Escape) Plugin.MenuKey.Value = key.keyCode; Rebinding = false; break; }
                }
                else if (Plugin.MenuKey.Value != Key.None && keyboard[Plugin.MenuKey.Value].wasPressedThisFrame) Toggle();
                else if (Visible && keyboard.escapeKey.wasPressedThisFrame) Close();
            }
            if (Visible && !Session.Open) Visible = false;
            if (Visible) Session.Maintain();
            if (_openTools && !EditorSession.Busy) { _openTools = false; ToolWindow.Open(_toolsPage); _toolsPage = null; }
            Instrumentation.Timing = Recording && Instrumentation.PendingCount == 0 && Instrumentation.Fault == null;
            Instrumentation.Allocations = Plugin.AllocationTiming.Value && Instrumentation.AllocationSupported;
            Instrumentation.Spikes = Plugin.SpikeLog.Value; Instrumentation.Lifecycle = Plugin.LifecycleLog.Value; Instrumentation.Threshold = Plugin.SpikeThreshold.Value;
            Instrumentation.InstallStep();
            if (Instrumentation.Fault != null && Instrumentation.Fault != _lastFault) { _lastFault = Instrumentation.Fault; Status = "Detailed timing stopped safely: " + _lastFault; Plugin.Log.LogError(Status); }
            TraceEvent ev; int drained = 0;
            while (drained++ < 32 && Instrumentation.Dequeue(out ev))
            {
                string line = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " frame=" + ev.Frame + " thread=" + ev.Thread + " " + ev.Method
                    + " " + (ev.Ticks * 1000.0 / Stopwatch.Frequency).ToString("0.###", CultureInfo.InvariantCulture) + "ms" + (ev.Error == null ? "" : " exception=" + ev.Error);
                AddTrace(line); Plugin.Log.LogInfo(line);
            }
            if (SceneMarker != null)
            {
                string line = "Scene: " + SceneMarker; SceneMarker = null; AddTrace(line);
                if (Plugin.LifecycleLog.Value) Plugin.Log.LogInfo(line);
            }
            float now = Time.realtimeSinceStartup;
            if (now >= _snapshotAt)
            {
                double seconds = Math.Max(.001, now - _lastSnapshot);
                if (Recording) Measurements.Snapshot(seconds);
                Measurements.OwnMilliseconds = _ownFrames == 0 ? 0 : _ownTicks * 1000.0 / Stopwatch.Frequency / _ownFrames;
                _ownTicks = 0; _ownFrames = 0; _lastSnapshot = now; _snapshotAt = now + 1;
                if (Recording && Plugin.Summaries.Value && now >= _summaryAt) { _summaryAt = now + Plugin.LogInterval.Value; WriteSummary(); }
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
        private void OnGUI()
        {
            if (!Visible) return; long begin = Stopwatch.GetTimestamp();
            Dashboard.Draw(this); _ownTicks += Stopwatch.GetTimestamp() - begin;
        }
        internal void Toggle()
        {
            if (Visible) { Close(); return; }
            if ((_acquire != null ? _acquire(false, true) : Session.Acquire(false))) Visible = true;
            else { Status = "Close the other editor/menu first."; Plugin.Log.LogInfo(Status); }
        }
        internal void Close() { Visible = false; Rebinding = false; Session.Close(); Plugin.Settings.Save(); }
        internal void OpenTools() { Close(); _openTools = true; }
        internal void OpenTool(ToolPage page)
        {
            WildWasteland.Plugin.EnableDebug.Value = true; WildWasteland.Plugin.DeveloperMode.Value = true;
            _toolsPage = page; Close(); _openTools = true;
        }
        internal void AddTrace(string line) { if (Trace.Count >= 512) Trace.RemoveAt(0); Trace.Add(line); }
        internal void WriteSummary()
        {
            var c = CultureInfo.InvariantCulture;
            string line = "PERF " + Measurements.Context + " | frame avg/p95/p99 " + Measurements.Average.ToString("0.0", c) + "/" + Measurements.P95.ToString("0.0", c) + "/" + Measurements.P99.ToString("0.0", c)
                + "ms | scheduler " + Measurements.SchedulerMsPerSecond.ToString("0.0", c) + "ms/s | managed " + (Measurements.ManagedBytes / 1048576.0).ToString("0.0", c) + "MiB | GC " + Measurements.Gc0 + "/" + Measurements.Gc1 + "/" + Measurements.Gc2;
            Plugin.Log.LogInfo(line); AddTrace(line);
            for (int i = 0; i < Math.Min(8, Measurements.Methods.Count); i++)
            {
                var row = Measurements.Methods[i]; if (row.Calls <= 0) continue;
                string detail = "PERF-CALL " + row.Name + " calls/s=" + row.Calls.ToString("0.0", c) + " self/main/worker ms/s=" + row.Self.ToString("0.###", c) + "/" + row.Main.ToString("0.###", c) + "/" + row.Worker.ToString("0.###", c);
                Plugin.Log.LogInfo(detail); AddTrace(detail);
            }
            for (int i = 0; i < Math.Min(5, Measurements.Jobs.Count); i++) { var job = Measurements.Jobs[i]; string detail = "PERF-JOB " + job.System + "." + job.Name + " ms/s=" + job.Milliseconds.ToString("0.###", c); Plugin.Log.LogInfo(detail); AddTrace(detail); }
        }
        private void OnApplicationQuit() { Dispose(); }
        private void OnDestroy() { if (Instance == this) { Dispose(); Instance = null; } }
        private bool _disposed;
        private void Dispose()
        {
            if (_disposed) return; _disposed = true; Session.Close(true); Plugin.Unhook();
            try { Instrumentation.Remove(); } catch (Exception e) { Plugin.Log.LogWarning(e.Message); }
            Measurements.Dispose(); Plugin.Settings.Save();
        }
    }
}
