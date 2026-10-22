using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Core.Optimization;
using WildWasteland.Core.Events;

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
        internal static ConfigEntry<bool> AllocationTiming;
        internal static ConfigEntry<float> SpikeThreshold;
        private static ConfigEntry<bool> _traceHandlers, _navigationOverlay, _patrolOverlay, _trafficDebugMap, _recordNpcNames;
        private static ConfigEntry<Key> _factionDumpKey;
        private readonly QuestNameRecorder _nameRecorder = new QuestNameRecorder();
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
            _navigationOverlay = cfg.Tool("Navigation", "Show navigation", false, "Draw copied NPC ghosts, states and waypoints.");
            _patrolOverlay = cfg.Tool("Patrols", "AI overlay", false, "Show the existing per-pilot driving state lines.");
            _trafficDebugMap = cfg.Tool("Patrols", "Traffic debug map", true, "Draw routes on the native debug map when HUD has no route policy provider.");
            _recordNpcNames = cfg.Tool("Quests", "Record NPC names", false, "Record talked-to NPC names and locations for quest authoring.");
            _factionDumpKey = cfg.Hotkey("Faction diagnostic dump", Key.None, "Write nearby faction/FSM/quest details under Debug.");
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
            _navigationOverlay.SettingChanged += PolicyChanged;
            _patrolOverlay.SettingChanged += PolicyChanged;
            _trafficDebugMap.SettingChanged += PolicyChanged;
            _recordNpcNames.SettingChanged += PolicyChanged;
            QuestDiagnosticState.NpcIdentified += _nameRecorder.Record;
            ApplyPolicy();
            Core.Trace.Marked += Measurements.MarkTag;
            ModEvents.Subscribe<TileApplied>(this, TileAppliedTag);
            ModEvents.Subscribe<OriginShifted>(this, OriginTag);
            OptimizationMetrics.Marked += Measurements.MarkTag;
            Hotkeys.Register(this, _factionDumpKey, WriteFactionDump, CanDumpFaction);
            Scheduler.Gui(this, "Debug.PatrolOverlay", PatrolDiagnosticOverlay.Draw, PatrolOverlayWanted);
            Scheduler.Gui(this, "Debug.NavigationOverlay", NavigationOverlay.Draw, NavigationWanted);
            Scheduler.EveryFrame(this, "Debug.Measurements", _runtime.Tick, Phase.EarlyUpdate);
            Scheduler.Gui(this, "Debug.Dashboard", _runtime.Draw, DashboardWanted);
        }

        /// <summary>Publishes logging and capture controls through the frozen Core contracts.</summary>
        public override void ProvideApis()
        {
            ModApi.Provide<ILogPolicy>(this, this);
            ModApi.Provide<ITraceControl>(this, this);
            ModApi.Provide<INavigationDiagnosticSink>(this, new NavigationMapExporter());
        }

        /// <summary>Adds the shared logging level and dashboard launcher to Core's settings shell.</summary>
        public override void Settings(SettingsShell shell)
        {
            shell.Custom(this, SettingsShell.Page.Debug, "Tools", DrawLaunchers);
            shell.Custom(this, SettingsShell.Page.Developer, "Tools", DrawTools);
            shell.Row(this, SettingsShell.Page.Debug, "Logging", VerboseLevel, "Logging level");
            shell.Row(this, SettingsShell.Page.Debug, "Navigation", _navigationOverlay, "Show navigation overlay");
            shell.Custom(this, SettingsShell.Page.Debug, "Navigation", DrawNavigation);
            shell.Row(this, SettingsShell.Page.Debug, "Patrols", _patrolOverlay, "Driving AI overlay");
            shell.Row(this, SettingsShell.Page.Debug, "Patrols", _trafficDebugMap, "Traffic debug map");
            shell.Custom(this, SettingsShell.Page.Debug, "Patrols", DrawPatrolStatus);
            shell.Row(this, SettingsShell.Page.Debug, "Quest authoring", _recordNpcNames, "Record NPC names and locations");
            shell.Custom(this, SettingsShell.Page.Debug, "Quest authoring", DrawQuestDiagnostics);
            shell.Row(this, SettingsShell.Page.Debug, "Measurements", _traceHandlers, "Trace hook and event handlers");
            shell.Custom(this, SettingsShell.Page.Debug, "Measurements", DrawMeasurements);
            shell.Custom(this, SettingsShell.Page.Developer, "Optimizations", OptimizationDiagnosticsPage.DrawDeveloper);
            shell.Resettable(this, SettingsShell.Page.Debug, VerboseLevel);
            shell.Resettable(this, SettingsShell.Page.Debug, _traceHandlers);
        }

        private void DrawLaunchers(ToolLayout layout)
        {
            if (layout.Button("Performance", _runtime != null)) ToolWindow.CloseThen(_runtime.Toggle);
            layout.Toggle(Plugin.DeveloperMode, "Developer");
        }

        private void DrawTools(ToolLayout layout)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1) layout.Heading("Advanced settings");
                foreach (var page in ToolWindow.DeveloperPages)
                {
                    if (page.IsTool != (pass == 0) || page.DebugOnly && !Plugin.EnableDebug.Value) continue;
                    bool available = page.Owner == null || ModuleHost.IsRunning(page.Owner) || page.ShowWhenModuleOff;
                    if (layout.Button(page.Title + " / " + page.Group, available)) { ToolWindow.Open(page); return; }
                }
            }
        }

        /// <summary>Draws private Patrols status through the Core diagnostic source.</summary>
        private void DrawPatrolStatus(ToolLayout layout)
        {
            var source = ModApi.Get<IPatrolDiagnosticSource>();
            layout.Text(source != null ? source.Status : "Patrol diagnostics unavailable (Patrols disabled).");
        }
        /// <summary>Shows the faction dump action; Debug owns its hotkey and output directory.</summary>
        private void DrawQuestDiagnostics(ToolLayout layout)
        {
            if (layout.Button("Write faction diagnostic dump", CanDumpFaction())) WriteFactionDump();
        }
        /// <summary>Native debug dumps require an active playable world and player position.</summary>
        private bool CanDumpFaction() { return GameRefs.InGame && GameRefs.Player != null; }
        /// <summary>Writes the original faction dump content using native data plus copied quest context.</summary>
        private void WriteFactionDump()
        {
            var source = ModApi.Get<IQuestDiagnosticSource>();
            try { FactionDump.Run(GameRefs.Player.transform.position, 60f, System.IO.Path.Combine(Assets.ModuleConfigDir("Debug"), "faction_dump.txt"), source != null ? source.PeopleWords : new string[0]); }
            catch (Exception error) { Core.Log.Error(this, "Faction dump failed", error); }
        }
        /// <summary>Requests GUI work only while the pilot overlay is visible and the game is unpaused.</summary>
        private bool PatrolOverlayWanted() { return RuntimeDiagnosticState.AiOverlay && Time.timeScale > 0; }
        /// <summary>Requests an IMGUI pass only while the NPC overlay is enabled.</summary>
        private bool NavigationWanted() { return NavigationDiagnosticState.Overlay; }
        /// <summary>Shows current NPC status and offers a one-shot map capture instead of a persistent dump logging switch.</summary>
        private void DrawNavigation(ToolLayout layout)
        {
            var source = ModApi.Get<INpcDiagnosticSource>();
            if (source == null) { layout.Text("NPC diagnostics are unavailable while NPCAI is disabled."); return; }
            layout.Text(source.Status);
            if (layout.Button(NavigationDiagnosticState.Capture ? "Stop navigation trace capture" : "Start navigation trace capture"))
            {
                NavigationDiagnosticState.Capture = !NavigationDiagnosticState.Capture;
                if (NavigationDiagnosticState.Capture) source.CaptureMaps();
            }
            layout.Text("Trace capture is temporary, writes existing map/trail BMPs and stops when Debug is disabled. Deep logging enables detailed chase traces.");
        }
        /// <summary>Reports whether the dashboard needs an IMGUI pass.</summary>
        private bool DashboardWanted() { return _runtime != null && _runtime.Visible; }

        /// <summary>Draws the measurement summary and opens the full non-pausing capture dashboard.</summary>
        private void DrawMeasurements(ToolLayout layout)
        {
            layout.Value("Managed frame budget", "average <= 0.5 ms/frame; worst <= 2 ms");
            layout.Value("Frame samples", Measurements.Count.ToString());
            if (layout.Button("Write FSM and scene census", _runtime != null)) DebugCensus.WriteCensus();
            layout.Value("Frame avg / p95 / p99", Measurements.Average.ToString("0.00") + " / " + Measurements.P95.ToString("0.00") + " / " + Measurements.P99.ToString("0.00") + " ms");
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
            NavigationDiagnosticState.Overlay = _running && _navigationOverlay.Value;
            RuntimeDiagnosticState.AiOverlay = _running && _patrolOverlay.Value;
            RuntimeDiagnosticState.TrafficDebugMap = _running && _trafficDebugMap.Value;
            QuestDiagnosticState.RecordNpcNames = _running && _recordNpcNames.Value;
            Core.Trace.On = _running && (_traceHandlers.Value || Level >= 2);
            if (!Core.Log.L2 && Instrumentation.Probes.Count > 0) Instrumentation.Remove();
        }

        /// <summary>Reapplies logging and handler timing after a live settings change.</summary>
        private void PolicyChanged(object sender, EventArgs args) { ApplyPolicy(); }

        /// <summary>Tags completed tile publication in the next frame sample.</summary>
        private void TileAppliedTag(ref TileApplied value) { Measurements.MarkTag("tile"); }
        /// <summary>Tags floating-origin relocation without persisting a scene-space position.</summary>
        private void OriginTag(ref OriginShifted value) { Measurements.MarkTag("origin"); }
        /// <summary>Tags scene transitions in the capture context without creating a second scene subscription.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { DebugRuntime.SceneMarker = scene.name; Measurements.MarkTag("scene"); }

        /// <summary>Stops measurements, removes diagnostic probes and leaves Core's log level and trace switch disabled.</summary>
        public override void Shutdown()
        {
            _running = false;
            Core.Trace.Marked -= Measurements.MarkTag;
            OptimizationMetrics.Marked -= Measurements.MarkTag;
            VerboseLevel.SettingChanged -= PolicyChanged;
            _traceHandlers.SettingChanged -= PolicyChanged;
            _navigationOverlay.SettingChanged -= PolicyChanged;
            _patrolOverlay.SettingChanged -= PolicyChanged;
            _trafficDebugMap.SettingChanged -= PolicyChanged;
            _recordNpcNames.SettingChanged -= PolicyChanged;
            QuestDiagnosticState.NpcIdentified -= _nameRecorder.Record;
            NavigationDiagnosticState.Reset(); NavigationOverlay.Release();
            if (_runtime != null) _runtime.Dispose();
            _runtime = null;
            ApplyPolicy();
        }
    }
}
