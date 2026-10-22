using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Core.Optimization;

namespace WildWasteland.Modules.Debug
{
    /// <summary>
    /// Owns the single logging policy, frame measurements, optional probes and capture dashboard. Core provides timing mechanisms;
    /// Debug owns their policy and presentation. All jobs and patches belong to this module and are removed on shutdown.
    /// </summary>
    public sealed class DebugModule : ModuleBase, ILogPolicy, ITraceControl
    {
        internal static DebugModule Instance;
        internal static ConfigEntry<int> VerboseLevel;
        internal static ConfigEntry<Key> MenuKey;
        internal static ConfigEntry<bool> AllocationTiming;
        internal static ConfigEntry<float> SpikeThreshold;
        private static ConfigEntry<bool> _traceHandlers;
        private DebugRuntime _runtime;
        private bool _running;

        /// <summary>The shared technical config containing Debug's own entries.</summary>
        internal static ConfigFile SettingsFile { get { return Instance.Config.Tools; } }

        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Debug"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Debug & tracing"; } }

        /// <summary>Binds one logging level and measurement controls. D12 fresh starts do not import any old log switches.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            VerboseLevel = cfg.Player(null, "VerboseLevel", 0, "Logging: 0 off, 1 main actions, 2 deep traces and diagnostic probes.", new AcceptableValueList<int>(0, 1, 2));
            MenuKey = cfg.Hotkey("Performance dashboard", Key.F4, "Open Debug performance measurements and A/B capture controls.");
            _traceHandlers = cfg.Tool("Measurements", "Trace handlers", false, "Measure individual Core hook, event and named-scope handlers.");
            AllocationTiming = cfg.Tool("Measurements", "Thread allocation timing", false, "Measure allocation deltas inside optional method probes; adds overhead.");
            SpikeThreshold = cfg.Tool("Measurements", "Slow call milliseconds", 2f, "Threshold for deep-level method spike traces.", new AcceptableValueRange<float>(.1f, 1000f));
        }

        /// <summary>Starts module-owned measurements and input; heavy profiler discovery is deferred until the first scheduler tick.</summary>
        public override void Init()
        {
            Instance = this; _running = true;
            _runtime = new DebugRuntime();
            VerboseLevel.SettingChanged += PolicyChanged;
            _traceHandlers.SettingChanged += PolicyChanged;
            ApplyPolicy();
            Core.Trace.Marked += Measurements.MarkTag;
            OptimizationMetrics.Marked += Measurements.MarkTag;
            WeatherDiagnosticState.StateChanged += WeatherDiagnosticTools.OnStateChanged;
            WeatherDiagnosticState.CameraRecordRequested += WeatherDiagnosticTools.OnCameraRecord;
            WeatherDiagnosticState.Reset();
            Scheduler.Gui(this, "Debug.WeatherOverlay", WeatherDiagnosticTools.DrawOverlay, WeatherDiagnosticTools.WantsOverlay);
            Hotkeys.Register(this, MenuKey, _runtime.Toggle, null, true);
            Scheduler.EveryFrame(this, "Debug.Measurements", _runtime.Tick, Phase.EarlyUpdate);
            Scheduler.Gui(this, "Debug.Dashboard", _runtime.Draw, DashboardWanted);
        }

        /// <summary>Publishes logging and capture controls through the frozen Core contracts.</summary>
        public override void ProvideApis()
        {
            ModApi.Provide<ILogPolicy>(this, this);
            ModApi.Provide<ITraceControl>(this, this);
            ModApi.Provide<IWeatherDiagnosticControls>(this, new WeatherDiagnosticTools());
        }

        /// <summary>Adds the shared logging level and dashboard launcher to Core's settings shell.</summary>
        public override void Settings(SettingsShell shell)
        {
            shell.Row(this, SettingsShell.Page.Debug, "Logging", VerboseLevel, "Logging level");
            shell.Row(this, SettingsShell.Page.Debug, "Measurements", _traceHandlers, "Trace hook and event handlers");
            shell.Custom(this, SettingsShell.Page.Debug, "Measurements", DrawMeasurements);
            shell.Custom(this, SettingsShell.Page.Developer, "Optimizations", OptimizationDiagnosticsPage.DrawDeveloper);
            shell.Resettable(this, SettingsShell.Page.Debug, VerboseLevel);
            shell.Resettable(this, SettingsShell.Page.Debug, _traceHandlers);
        }

        /// <summary>Reports whether the dashboard needs an IMGUI pass.</summary>
        private bool DashboardWanted() { return _runtime != null && _runtime.Visible; }

        /// <summary>Draws the measurement summary and opens the full non-pausing capture dashboard.</summary>
        private void DrawMeasurements(ToolLayout layout)
        {
            layout.Value("Managed frame budget", "average <= 0.5 ms/frame; worst <= 2 ms");
            layout.Value("Frame samples", Measurements.Count.ToString());
            layout.Value("Frame avg / p95 / p99", Measurements.Average.ToString("0.00") + " / " + Measurements.P95.ToString("0.00") + " / " + Measurements.P99.ToString("0.00") + " ms");
            if (layout.Button("Open performance dashboard", _runtime != null)) ToolWindow.CloseThen(_runtime.Toggle);
        }

        /// <summary>Configured logging level; setting it persists the sole Debug log policy entry.</summary>
        public int Level { get { return VerboseLevel.Value; } set { VerboseLevel.Value = Mathf.Clamp(value, 0, 2); } }

        /// <summary>QuietFsm hides its missing-FSM warnings below the deep logging level.</summary>
        public bool QuietFsmWarnings { get { return !_running || Level < 2; } }

        /// <summary>Enables handler timing explicitly; level 2 also enables it while Debug runs.</summary>
        public bool TraceHandlers { get { return _traceHandlers.Value; } set { _traceHandlers.Value = value; } }

        /// <summary>A scheduled or active A/B capture is running.</summary>
        public bool Capturing { get { return _runtime != null && _runtime.Capturing; } }

        /// <summary>Starts a labeled capture immediately when no other capture is scheduled or active.</summary>
        public bool StartCapture(string label, float seconds)
        {
            return _running && _runtime != null && _runtime.StartCapture(label, seconds);
        }

        /// <summary>Resets frame and scheduler counters; per-handler counter resets are supplied by Core.</summary>
        public void ResetCounters()
        {
            Scheduler.ResetStats();
            Core.Trace.ResetStats();
            Hooks.ResetStats(); ModEvents.ResetStats(); OptimizationMetrics.Reset();
            Measurements.Reset();
        }

        /// <summary>Applies the configured level and removes optional method probes as soon as the level drops below 2.</summary>
        private void ApplyPolicy()
        {
            Core.Log.Level = _running ? Level : 0;
            Core.Trace.On = _running && (_traceHandlers.Value || Level >= 2);
            if (!Core.Log.L2 && Instrumentation.Probes.Count > 0) Instrumentation.Remove();
        }

        /// <summary>Reapplies logging and handler timing after a live settings change.</summary>
        private void PolicyChanged(object sender, EventArgs args) { ApplyPolicy(); }

        /// <summary>Tags scene transitions in the capture context without creating a second scene subscription.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { DebugRuntime.SceneMarker = scene.name; }

        /// <summary>Stops measurements, removes diagnostic probes and leaves Core's log level and trace switch disabled.</summary>
        public override void Shutdown()
        {
            _running = false;
            Core.Trace.Marked -= Measurements.MarkTag;
            OptimizationMetrics.Marked -= Measurements.MarkTag;
            WeatherDiagnosticState.Reset();
            WeatherDiagnosticState.StateChanged -= WeatherDiagnosticTools.OnStateChanged;
            WeatherDiagnosticState.CameraRecordRequested -= WeatherDiagnosticTools.OnCameraRecord;
            VerboseLevel.SettingChanged -= PolicyChanged;
            _traceHandlers.SettingChanged -= PolicyChanged;
            if (_runtime != null) _runtime.Dispose();
            _runtime = null;
            ApplyPolicy();
        }
    }
}
