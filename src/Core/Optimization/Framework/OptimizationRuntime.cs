using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using Den.Tools;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;
using Patches = WildWasteland.Core.Patches;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// Core-owned base-game optimizations with an explicit feature list and shared resource owner. Not a registered module.
    /// </summary>
    public sealed class OptimizationRuntime : ModuleBase, IWeatherVisualPolicy
    {
        /// <summary>Uses Core as the optimization resource owner without adding a gameplay module registry entry.</summary>
        public override string Id { get { return "Core"; } }
        /// <summary>Name used for Core optimization diagnostic attribution.</summary>
        public override string DisplayName { get { return "Core optimization"; } }

        public static readonly OptimizationRuntime Instance = new OptimizationRuntime();
        public static PerfPreset Preset { get { return _preset != null ? _preset.Value : PerfPreset.Vanilla; } }
        static ConfigEntry<PerfPreset> _preset;
        static ConfigEntry<bool> _enabled;
        /// <summary>Shared preset entry used by Core settings and Debug's feature controls.</summary>
        public static ConfigEntry<PerfPreset> PresetEntry { get { return _preset; } }
        /// <summary>Core optimization master in Performance / Preset, independent of ModuleHost switches.</summary>
        public static ConfigEntry<bool> EnabledEntry { get { return _enabled; } }

        readonly List<Feature> _features = new List<Feature>();
        /// <summary>Stable read-only view of the explicitly registered feature instances.</summary>
        public IList<Feature> Features { get { return _featuresView ?? (_featuresView = _features.AsReadOnly()); } }
        IList<Feature> _featuresView;
        readonly HashSet<string> _patched = new HashSet<string>();
        bool _running, _initialized, _sceneSafe;
        Job _startup;
        /// <summary>Whether the Core optimization master is active, including deferred scene startup.</summary>
        public bool Running { get { return _running && _enabled.Value; } }
        public WeatherVisualDetail WeatherDetail { get { return Preset == PerfPreset.Low ? WeatherVisualDetail.Low : Preset == PerfPreset.Max ? WeatherVisualDetail.Max : WeatherVisualDetail.Balanced; } }

        /// <summary>Creates the one Core lifetime owner without accessing scene objects.</summary>
        private OptimizationRuntime() { }

        /// <summary>Binds Core optimization before ordinary module settings; no Unity scene work is performed.</summary>
        public static void Bind(ConfigFile settings, ConfigFile tools)
        {
            if (_preset != null) return;
            Instance.Config = new ModuleConfig("Performance", settings, tools);
            Instance.BindConfig(Instance.Config);
        }

        /// <summary>Binds the optimization master, quality preset and explicit feature entries without legacy migration.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            _enabled = cfg.Player("Preset", "Enabled", true, "Enable Core's base-game optimizations. Generated mesh LODs remain active at 2x distance when off. Changes apply immediately.");
            cfg.Enabled = _enabled;
            _enabled.SettingChanged += EnabledChanged;
            _preset = cfg.Tool("Preset", "Preset", PerfPreset.Vanilla,
                "PERFORMANCE (Low), BALANCED, VISUALS (Max), VANILLA (default). Vanilla keeps generated mesh LODs at 2x distance and preserves other original visuals, simulation settings and item cleanup; exact optimizations (no visible change) are not affected. Saved Low/Max values remain compatible.");
            _preset.SettingChanged += (s, a) => { if (_running) ApplyAll(true); };
            DiscoverFeatures();
            foreach (var f in _features)
            {
                f.Module = this;
                string sub = f.Category == Categories.PlayMaker ? "PlayMaker" : f.Category == Categories.Content ? "Game logic" : f.Category == Categories.Rendering ? "Rendering" : "World";
                f.ModeEntry = cfg.Tool(sub, f.Key, f.DefaultMode, f.PlanId + " " + f.Title + ". " + f.Help + (f.SupportsShadow ? " Shadow = vanilla runs, the optimized result is only compared (mismatches shown in Developer)." : ""));
                var captured = f;
                f.ModeEntry.SettingChanged += (s, a) => { if (_running) Apply(captured, true); };
                try { f.Bind(cfg); } catch (Exception e) { f.LastError = e.Message; Log.Error(OptimizationRuntime.Instance, f.PlanId + " Bind", e); }
            }
        }

        /// <summary>Changes the optimization master without hopping through the module registry.</summary>
        void EnabledChanged(object sender, EventArgs args)
        {
            if (!_initialized) return;
            StopRuntime();
            StartRuntime();
        }

        /// <summary>Instantiates the 43 approved features explicitly and retains their category/order/plan-id sorting.</summary>
        void DiscoverFeatures()
        {
            _features.Clear();
            _features.AddRange(new Feature[]
            {
                new TargetedEventsFeature(), new BroadcastFeature(), new FastActionLoadFeature(), new FastCallMethodFeature(),
                new FastPropertyFeature(), new FastFsmRemovalFeature(), new FsmSleepFeature(), new GuiLoopFeature(),
                new MousePickFeature(), new StringConvertFeature(), new TriggerProxyFeature(),
                new CoyoteBroadcastFeature(), new CullerFeature(), new DataFsmRestartFeature(), new DuplicateRigidbodyFeature(),
                new HudFsmFeature(), new LightFlickerFeature(), new PlayerCapsuleFeature(), new VehicleReflectionFeature(),
                new HingePollFeature(), new WaitPollFeature(), new OptionTogglePollFeature(),
                new AnimatorCulling(), new CameraLayerCulling(), new FarClip(), new CullLods(), new GcSlice(),
                new GeneratedLods(), new LightPolicy(), new ParticleTuning(), new PostFxExact(), new AntiAliasing(),
                new SmallCasters(), new SunShadows(), new TerrainDetail(), new TextureQuality(), new CarLodFeature(),
                new MapMagicBudgetFeature(), new CcdSweepFeature(), new TimePhysicsFeature(), new OriginJumpFeature(),
                new AzureSunFeature(), new AzureWeatherFeature()
            });
            _features.Sort((a, b) =>
            {
                int ca = Array.IndexOf(Categories.Order, a.Category), cb = Array.IndexOf(Categories.Order, b.Category);
                if (ca != cb) return ca.CompareTo(cb);
                if (a.Order != b.Order) return a.Order.CompareTo(b.Order);
                return string.CompareOrdinal(a.PlanId, b.PlanId);
            });
        }

        /// <summary>Starts the shared runtime even with the master off so generated mesh LODs remain available.</summary>
        public override void Init()
        {
            if (_initialized) return;
            _initialized = true;
            StartRuntime();
        }

        /// <summary>Publishes Core's active visual policy to Survival without a module dependency.</summary>
        public override void ProvideApis() { if (Running) ModApi.Provide<IWeatherVisualPolicy>(this, this); }

        /// <summary>Subscribes the shared dispatchers and defers scene-dependent features until a scene is loaded.</summary>
        void StartRuntime()
        {
            if (_running) return;
            _running = true; Active = true;
            if (_enabled.Value) FsmPause.Own(this, FsmPause.CarLod | FsmPause.PollSleep | FsmPause.Stepped | FsmPause.FsmSleep);
            ProvideApis();
            WorldTiles.OnApplied(this, TileApplied);
            Hooks.CreateObjectEnter.Postfix(this, RenderScan.AfterCreateObject);
            // Wakes and GUI cleanup precede replacement bodies that may clear the native FSM reference.
            foreach (var f in _features) if (f is FsmSleepFeature || f is GuiLoopFeature) SubscribeFeature(f);
            foreach (var f in _features)
            {
                if (f is FsmSleepFeature || f is GuiLoopFeature) continue;
                SubscribeFeature(f);
            }
            // Plugin.Awake may run while the chainloader still owns startup. Query the scene only from a runner frame
            // or the scene-loaded callback; a loaded-looking scene alone does not make render access safe during Awake.
            _deferred = !_sceneSafe;
            if (_deferred) { if (Log.L1) Log.Main(OptimizationRuntime.Instance, "waiting for the first scene to start " + _features.Count + " optimizations"); }
            else StartFeatures();
            if (_deferred) _startup = Scheduler.EveryFrame(this, "Core.Optimization.Startup", StartWhenSceneReady, Phase.EarlyUpdate);
            Hooks.ApplyUsed();
        }

        /// <summary>First runner-frame startup barrier: scene/render features never execute inside Plugin.Awake.</summary>
        void StartWhenSceneReady()
        {
            if (!_running || !_deferred || !SceneManager.GetActiveScene().isLoaded) return;
            _sceneSafe = true;
            StartFeatures();
        }

        /// <summary>Contains one feature's optional hook registration failure so unrelated features still initialize.</summary>
        void SubscribeFeature(Feature feature)
        {
            if (!_enabled.Value && !(feature is GeneratedLods)) return;
            try { feature.SubscribeHooks(); }
            catch (Exception ex) { feature.LastError = ex.Message; Log.Error(this, feature.PlanId + " hooks", ex); }
        }

        bool _deferred;
        /// <summary>Applies the effective feature modes after scene-dependent startup becomes safe.</summary>
        void StartFeatures()
        {
            if (_startup != null) { _startup.Remove(); _startup = null; }
            _deferred = false;
            ApplyAll(false);
            int on = 0; foreach (var f in _features) if (f.Running != FeatureMode.Off) on++;
            if (Log.L1) Log.Main(OptimizationRuntime.Instance, "started: " + on + "/" + _features.Count + " optimizations active, preset " + Preset);
        }

        /// <summary>Ends the Core optimization lifetime and restores every active feature.</summary>
        public override void Shutdown()
        {
            _initialized = false;
            StopRuntime();
        }

        /// <summary>Restores every feature and releases only optimization-owned registrations and resources.</summary>
        void StopRuntime()
        {
            if (!_running) return;
            _running = false; Active = false;
            _deferred = false;
            WorldTiles.RemoveAll(this);
            for (int i = _features.Count - 1; i >= 0; i--) _features[i].Stop();
            Scheduler.RemoveAll(this);
            _startup = null;
            Hooks.RemoveAll(this);
            Patches.Unpatch(this);
            FsmPause.ReleaseAll(this);
            ModApi.RemoveAll(this);
            FsmRegistry.Unsubscribed();   // Core removes the module's hook handlers after Shutdown
            FsmEdits.Unsubscribed();
            PollSleep.Unsubscribed();
            _patched.Clear();   // Core unpatches the module after Shutdown
        }

        /// <summary>Computes the effective feature mode, forcing preset-dependent features off under Vanilla.</summary>
        static FeatureMode Wanted(Feature f)
        {
            if (f is GeneratedLods) return FeatureMode.On;
            if (!_enabled.Value) return FeatureMode.Off;
            if (f.ModeEntry == null) return FeatureMode.Off;
            if (f.UsesPreset && Preset == PerfPreset.Vanilla) return FeatureMode.Off;
            return f.ModeEntry.Value;
        }

        /// <summary>Refreshes active preset-dependent features or applies changed effective feature modes.</summary>
        void ApplyAll(bool presetChanged)
        {
            foreach (var f in _features)
            {
                var want = Wanted(f);
                if (presetChanged && f.UsesPreset && want != FeatureMode.Off && f.Running != FeatureMode.Off) { SafeSettings(f); continue; }
                Apply(f, false);
            }
        }

        /// <summary>Transitions one feature between effective modes; deferred or stopped runtimes do no work.</summary>
        internal void Apply(Feature f, bool log)
        {
            if (!_running || _deferred) return;
            var want = Wanted(f);
            if (f.Running == want) return;
            f.Stop();
            if (want != FeatureMode.Off) f.Start(want);
            if (log && Log.L1) Log.Main(OptimizationRuntime.Instance, f.PlanId + " " + f.Key + " -> " + f.Running);
        }

        /// <summary>Refreshes a running feature after one of its tuning overrides changes.</summary>
        internal void SettingsChanged(Feature f)
        {
            if (!_running || f.Running == FeatureMode.Off) return;
            SafeSettings(f);
        }

        /// <summary>Contains an individual feature's settings failure and retains the error for Debug.</summary>
        static void SafeSettings(Feature f)
        {
            try { f.OnSettingsChanged(); } catch (Exception e) { f.LastError = e.Message; Log.Error(OptimizationRuntime.Instance, f.PlanId + " settings", e); }
        }

        /// <summary>Installs the specified optimization-only patch once per runtime lifetime and records success.</summary>
        internal bool EnsurePatched(Type patchClass)
        {
            if (_patched.Contains(patchClass.FullName)) return true;
            int n = Patches.ApplyClass(this, patchClass);
            if (n > 0) { _patched.Add(patchClass.FullName); return true; }
            return false;
        }

        /// <summary>Installs the specified optimization-only patch once per runtime lifetime and records success.</summary>
        internal bool EnsurePatched(MethodBase original, MethodInfo prefix, MethodInfo postfix, MethodInfo transpiler, MethodInfo finalizer)
        {
            if (original == null) return false;
            string key = original.DeclaringType.FullName + "::" + original + "|" + (prefix != null ? prefix.DeclaringType.FullName + prefix.Name : "") + (postfix != null ? postfix.DeclaringType.FullName + postfix.Name : "")
                + (transpiler != null ? transpiler.DeclaringType.FullName + transpiler.Name : "") + (finalizer != null ? finalizer.DeclaringType.FullName + finalizer.Name : "");
            if (_patched.Contains(key)) return true;
            if (Patches.Patch(this, original, prefix, postfix, finalizer, transpiler)) { _patched.Add(key); return true; }
            return false;
        }

        /// <summary>Forwards a lifecycle callback only to active features and contains each feature's failure.</summary>
        void Each(Action<Feature> a, string what)
        {
            if (!_running) return;
            foreach (var f in _features)
            {
                if (f.Running == FeatureMode.Off) continue;
                try { a(f); } catch (Exception e) { f.LastError = e.Message; Log.Error(OptimizationRuntime.Instance, f.PlanId + " " + what, e); }
            }
        }

        /// <summary>Drops scene-bound references and refreshes this feature for the newly loaded scene.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _sceneSafe = true;
            if (!_running) return;
            if (_deferred) { StartFeatures(); return; }   // features start against this scene: no separate OnSceneLoaded
            Each(f => f.OnSceneLoaded(), "OnSceneLoaded");
        }
        /// <summary>Refreshes this feature after native gameplay objects have finished loading.</summary>
        public override void OnWorldReady() { Each(f => f.OnWorldReady(), "OnWorldReady"); }
        /// <summary>Adjusts cached positions after the floating origin moves.</summary>
        public override void OnOriginShift(Vector3 delta) { Each(f => f.OnOriginShift(delta), "OnOriginShift"); }
        /// <summary>Restores this feature's saved-object state before the native save transaction.</summary>
        public override void OnSaveBegin(string slot) { Each(f => f.OnSaveBegin(), "OnSaveBegin"); }
        /// <summary>Resumes feature work after the native save transaction has completed.</summary>
        public override void OnSaveEnd(string slot) { Each(f => f.OnSaveEnd(), "OnSaveEnd"); }
        /// <summary>Debug owns typed load attribution; optimization has no additional load-begin mutation.</summary>
        public override void OnLoadBegin(string slot) { }

        /// <summary>MapMagic applied a tile (main or draft): features that edit spawned objects re-run on that tile.</summary>
        public static event Action<TerrainTile> TileAppliedEvent;
        /// <summary>WorldTiles main-thread callback: forwards applied tiles to active feature handlers with isolated failures.</summary>
        void TileApplied(TerrainTile tile, TileData data, StopToken stop)
        {
            if (!_running) return;
            var h = TileAppliedEvent;
            if (h == null) return;
            foreach (Action<TerrainTile> d in h.GetInvocationList())
            {
                try { d(tile); } catch (Exception e) { Log.Error(OptimizationRuntime.Instance, "tile handler", e); }
            }
        }

        /// <summary>Core registers the shared Performance tab directly; optimization contributes no module-owned pages.</summary>
        public override IEnumerable<ToolPage> ToolPages() { return null; }   // the Performance tab is registered by Core (CorePages)
    }
}
