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
        public override string Id { get { return "Core"; } }
        public override string DisplayName { get { return "Core optimization"; } }

        public static readonly OptimizationRuntime Instance = new OptimizationRuntime();
        public static PerfPreset Preset { get { return _preset != null ? _preset.Value : PerfPreset.Vanilla; } }
        static ConfigEntry<PerfPreset> _preset;
        static ConfigEntry<bool> _enabled;
        public static ConfigEntry<PerfPreset> PresetEntry { get { return _preset; } }
        /// <summary>Core optimization master in Performance / Preset, independent of ModuleHost switches.</summary>
        public static ConfigEntry<bool> EnabledEntry { get { return _enabled; } }

        readonly List<Feature> _features = new List<Feature>();
        public IList<Feature> Features { get { return _featuresView ?? (_featuresView = _features.AsReadOnly()); } }
        IList<Feature> _featuresView;
        readonly HashSet<string> _patched = new HashSet<string>();
        bool _running, _initialized;
        public bool Running { get { return _running; } }
        public WeatherVisualDetail WeatherDetail { get { return Preset == PerfPreset.Low ? WeatherVisualDetail.Low : Preset == PerfPreset.Max ? WeatherVisualDetail.Max : WeatherVisualDetail.Balanced; } }

        private OptimizationRuntime() { }

        /// <summary>Binds Core optimization before ordinary module settings; no Unity scene work is performed.</summary>
        public static void Bind(ConfigFile settings, ConfigFile tools)
        {
            if (_preset != null) return;
            Instance.Config = new ModuleConfig("Performance", settings, tools);
            Instance.BindConfig(Instance.Config);
        }

        public override void BindConfig(ModuleConfig cfg)
        {
            _enabled = cfg.Player("Preset", "Enabled", true, "Enable Core's base-game optimizations. Changes apply immediately.");
            cfg.Enabled = _enabled;
            _enabled.SettingChanged += EnabledChanged;
            _preset = cfg.Tool("Preset", "Preset", PerfPreset.Vanilla,
                "PERFORMANCE (Low), BALANCED, VISUALS (Max), VANILLA (default). Vanilla preserves original visuals, simulation settings and item cleanup; exact optimizations (no visible change) are not affected. Saved Low/Max values remain compatible.");
            _preset.SettingChanged += (s, a) => { if (_running) ApplyAll(true); };
            DiscoverFeatures();
            foreach (var f in _features)
            {
                f.Module = this;
                string sub = f.Category == Categories.PlayMaker ? "PlayMaker" : f.Category == Categories.Content ? "Game logic" : f.Category == Categories.Rendering ? "Rendering" : "World";
                f.ModeEntry = cfg.Tool(sub, f.Key, f.DefaultMode, f.PlanId + " " + f.Title + ". " + f.Help + (f.SupportsShadow ? " Shadow = vanilla runs, the optimized result is only compared (mismatches shown in Developer)." : ""));
                var captured = f;
                f.ModeEntry.SettingChanged += (s, a) => { if (_running) Apply(captured, true); };
                try { f.Bind(cfg); } catch (Exception e) { f.LastError = e.Message; Log.Error("[Performance] " + f.PlanId + " Bind", e); }
            }
        }

        /// <summary>Changes the optimization master without hopping through the module registry.</summary>
        void EnabledChanged(object sender, EventArgs args)
        {
            if (!_initialized) return;
            if (_enabled.Value) StartRuntime(); else StopRuntime();
        }

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

        public override void Init()
        {
            if (_initialized) return;
            _initialized = true;
            if (_enabled != null && _enabled.Value) StartRuntime();
        }

        /// <summary>Publishes Core's active visual policy to Survival without a module dependency.</summary>
        public override void ProvideApis() { if (_running) ModApi.Provide<IWeatherVisualPolicy>(this, this); }

        /// <summary>Subscribes the shared dispatchers and defers scene-dependent features until a scene is loaded.</summary>
        void StartRuntime()
        {
            if (_running) return;
            _running = true; Active = true;
            FsmPause.Own(this, FsmPause.CarLod | FsmPause.PollSleep | FsmPause.Stepped | FsmPause.FsmSleep);
            ProvideApis();
            WorldTiles.OnApplied(this, TileApplied);
            foreach (var f in _features)
            {
                try { f.SubscribeHooks(); } catch (Exception e) { f.LastError = e.Message; Log.Error("[Performance] " + f.PlanId + " hooks", e); }
            }
            // Features touch scene objects and render settings (RenderSettings.sun crashed natively when called from the
            // BepInEx chainloader, before any scene exists): start them on the first loaded scene, or now when switched on in game.
            _deferred = !SceneManager.GetActiveScene().isLoaded;
            if (_deferred) Log.Always("[Performance] waiting for the first scene to start " + _features.Count + " optimizations");
            else StartFeatures();
            Hooks.ApplyUsed();
        }

        bool _deferred;
        void StartFeatures()
        {
            _deferred = false;
            ApplyAll(false);
            int on = 0; foreach (var f in _features) if (f.Running != FeatureMode.Off) on++;
            Log.Always("[Performance] started: " + on + "/" + _features.Count + " optimizations active, preset " + Preset);
        }

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
            Hooks.RemoveAll(this);
            Patches.Unpatch(this);
            FsmPause.ReleaseAll(this);
            ModApi.RemoveAll(this);
            FsmRegistry.Unsubscribed();   // Core removes the module's hook handlers after Shutdown
            _patched.Clear();   // Core unpatches the module after Shutdown
        }

        static FeatureMode Wanted(Feature f)
        {
            if (f.ModeEntry == null) return FeatureMode.Off;
            if (f.UsesPreset && Preset == PerfPreset.Vanilla) return FeatureMode.Off;
            return f.ModeEntry.Value;
        }

        void ApplyAll(bool presetChanged)
        {
            foreach (var f in _features)
            {
                var want = Wanted(f);
                if (presetChanged && f.UsesPreset && want != FeatureMode.Off && f.Running != FeatureMode.Off) { SafeSettings(f); continue; }
                Apply(f, false);
            }
        }

        internal void Apply(Feature f, bool log)
        {
            if (!_running || _deferred) return;
            var want = Wanted(f);
            if (f.Running == want) return;
            f.Stop();
            if (want != FeatureMode.Off) f.Start(want);
            if (log) Log.Always("[Performance] " + f.PlanId + " " + f.Key + " -> " + f.Running);
        }

        internal void SettingsChanged(Feature f)
        {
            if (!_running || f.Running == FeatureMode.Off) return;
            SafeSettings(f);
        }

        static void SafeSettings(Feature f)
        {
            try { f.OnSettingsChanged(); } catch (Exception e) { f.LastError = e.Message; Log.Error("[Performance] " + f.PlanId + " settings", e); }
        }

        internal bool EnsurePatched(Type patchClass)
        {
            if (_patched.Contains(patchClass.FullName)) return true;
            int n = Patches.ApplyClass(this, patchClass);
            if (n > 0) { _patched.Add(patchClass.FullName); return true; }
            return false;
        }

        internal bool EnsurePatched(MethodBase original, MethodInfo prefix, MethodInfo postfix, MethodInfo transpiler, MethodInfo finalizer)
        {
            if (original == null) return false;
            string key = original.DeclaringType.FullName + "::" + original + "|" + (prefix != null ? prefix.DeclaringType.FullName + prefix.Name : "") + (postfix != null ? postfix.DeclaringType.FullName + postfix.Name : "")
                + (transpiler != null ? transpiler.DeclaringType.FullName + transpiler.Name : "") + (finalizer != null ? finalizer.DeclaringType.FullName + finalizer.Name : "");
            if (_patched.Contains(key)) return true;
            if (Patches.Patch(this, original, prefix, postfix, finalizer, transpiler)) { _patched.Add(key); return true; }
            return false;
        }

        void Each(Action<Feature> a, string what)
        {
            foreach (var f in _features)
            {
                if (f.Running == FeatureMode.Off) continue;
                try { a(f); } catch (Exception e) { f.LastError = e.Message; Log.Error("[Performance] " + f.PlanId + " " + what, e); }
            }
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_running) return;
            if (_deferred) { StartFeatures(); return; }   // features start against this scene: no separate OnSceneLoaded
            Each(f => f.OnSceneLoaded(), "OnSceneLoaded");
        }
        public override void OnWorldReady() { Each(f => f.OnWorldReady(), "OnWorldReady"); }
        public override void OnOriginShift(Vector3 delta) { Each(f => f.OnOriginShift(delta), "OnOriginShift"); }
        public override void OnSaveBegin(string slot) { Each(f => f.OnSaveBegin(), "OnSaveBegin"); }
        public override void OnSaveEnd(string slot) { Each(f => f.OnSaveEnd(), "OnSaveEnd"); }
        public override void OnLoadBegin(string slot) { }

        /// <summary>MapMagic applied a tile (main or draft): features that edit spawned objects re-run on that tile.</summary>
        public static event Action<TerrainTile> TileAppliedEvent;
        void TileApplied(TerrainTile tile, TileData data, StopToken stop)
        {
            if (!_running) return;
            var h = TileAppliedEvent;
            if (h == null) return;
            foreach (Action<TerrainTile> d in h.GetInvocationList())
            {
                try { d(tile); } catch (Exception e) { Log.Error("[Performance] tile handler", e); }
            }
        }

        public override IEnumerable<ToolPage> ToolPages() { return null; }   // the Performance tab is registered by Core (CorePages)
    }
}
