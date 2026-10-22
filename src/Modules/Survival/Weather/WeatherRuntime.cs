using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Survival
{
    /// <summary>Survival-owned weather scope preserves native anchors and restores native rendering when disabled.</summary>
    internal sealed partial class WeatherRuntime : ModuleBase, IWeatherRegions
    {
        private readonly WeatherRegion[] _regions = new WeatherRegion[4];
        private readonly float[] _ages = new float[4];
        private readonly bool[] _criticalLogged = new bool[4];
        private int _count;
        private float _clock;
        private ConfigEntry<bool> _mapAreas;
        private ConfigEntry<float> _density, _width, _depth;
        private readonly Vector3[] _columns = new Vector3[80];
        private int _columnCount;
        private float _lastDensity = -1, _lastWidth, _lastDepth;
        private WeatherVisuals _visuals;
        private WeatherAudio _audio;
        private WeatherNative _native;
        private WeatherHazards _hazards;
        private readonly WeatherNative.Storm[] _anchors = new WeatherNative.Storm[4];
        private Job _draw;
        private bool _running;
        private bool _nativeAssetsReady;
        private Job _startup;
        private bool _prepared;
        private IModule _parent;
        /// <summary>Retains the public service owner while patches use the weather child scope.</summary>
        internal void SetParent(IModule parent) { _parent = parent; }
        /// <summary>Private weather lifecycle scope beneath the registered Survival owner.</summary>
        public override string Id { get { return "Survival.Weather"; } }
        /// <summary>Diagnostic name of the private weather scope.</summary>
        public override string DisplayName { get { return "Survival.Weather"; } }
        /// <summary>Number of currently selected natural or preview regions.</summary>
        public int Count { get { return _count; } }
        public bool MapAreas { get { return _mapAreas.Value; } set { _mapAreas.Value = value; } }
        public float HurricaneDensity { get { return _density.Value; } set { _density.Value = value; } }
        public float FrontWidthKm { get { return _width.Value; } set { _width.Value = value; } }
        public float DepthKm { get { return _depth.Value; } set { _depth.Value = value; } }

        /// <summary>Shares Survival configuration with the private weather scope.</summary>
        internal void Bind(ModuleConfig cfg) { Config = cfg; BindConfig(cfg); }

        /// <summary>Binds live storm geometry and map presentation settings.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            WeatherPalette.Bind(cfg);
            _mapAreas = cfg.Player("Map", "Storm areas", true,
                "Show moving Weather storm areas on the map. Critical storms lift vehicles and damage actors in hurricane sand walls; eyes are safe.");
            _density = cfg.Player("Storms", "Hurricanes per km²", .8f, "Columns per square kilometre of critical storm area, rounded to the nearest whole column. Zero disables columns. Applies live and does not change with Performance presets.", new AcceptableValueRange<float>(0,1));
            _width = cfg.Player("Storms", "Front width (km)", 20f, "Width of the broad leading sand front. Applies live to preview regions and their map areas.", new AcceptableValueRange<float>(6,20));
            _depth = cfg.Player("Storms", "Depth (km)", 4f, "Depth behind the front; sand fades and the footprint narrows toward the rear. Applies live.", new AcceptableValueRange<float>(1,4));
        }

        /// <summary>Installs the fog scope and defers scene-dependent preparation to a later frame.</summary>
        public override void Init()
        {
            _running = true; _prepared = false; _count = 0; _clock = 0; _nativeAssetsReady=false;
            if (!Core.Patches.Patch(this,Core.Patches.Method(typeof(UnityEngine.AzureSky.AzureFogScattering),"OnRenderImage"),
                prefix: Core.Patches.Method(typeof(WeatherSky),"NativeFogBefore"), finalizer: Core.Patches.Method(typeof(WeatherSky),"NativeFogAfter")))
                throw new System.InvalidOperationException("Weather requires the native Azure fog scope.");
            _audio = new WeatherAudio(System.IO.Path.Combine(Core.Assets.ModuleDir("Survival"),"sound","sandstorm.wav"));
            _draw = Scheduler.EveryFrame(this, "Survival.Weather.Visuals", Tick, Phase.LateUpdateLast);
            _draw.Enabled = false;
            _startup = Scheduler.EveryFrame(this, "Survival.Weather.Startup", StartWhenSceneReady, Phase.EarlyUpdate);
        }
        /// <summary>Defers all scene/native asset discovery until a loaded scene exists after chainloading.</summary>
        private void StartWhenSceneReady()
        {
            if (!_running || _prepared || !SceneManager.GetActiveScene().isLoaded) return;
            try
            {
                PrepareAssets();
                _native = new WeatherNative(this); _hazards = new WeatherHazards();
                Scheduler.Interval(this, "Survival.Weather.PrepareNativeAssets", 1, PrepareNativeAssets);
                Scheduler.EveryFrame(this, "Survival.Weather.HurricaneHazards", TickHazards, Phase.FixedUpdate);
                _prepared = true;
                if (_startup != null) _startup.Remove(); _startup = null;
                Hooks.ApplyUsed();
            }
            catch
            {
                Shutdown(); ModApi.Withdraw<IWeatherRegions>(_parent); ModApi.Withdraw<IWeatherPreview>(_parent);
                throw;
            }
        }

        /// <summary>Returns a detached region value when the index is active.</summary>
        public bool TryGet(int index, out WeatherRegion region)
        {
            region = index >= 0 && index < _count ? _regions[index] : default(WeatherRegion);
            return index >= 0 && index < _count;
        }

        /// <summary>Clears old scene anchors while retaining prepared weather assets.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Clear(); if (_native!=null) _native.Reset(); }
        /// <summary>Restores old anchors before preparing resources for a loaded world.</summary>
        public override void OnLoadBegin(string slot) { Clear(); if (_native!=null) _native.Reset(); if (_prepared) PrepareAssets(); }
        /// <summary>Prepares late native resources and discovers the loaded world’s storm anchors.</summary>
        public override void OnWorldReady() { StartWhenSceneReady(); if (_prepared) PrepareAssets(); if (_native!=null) { _native.Scan(); _native.Save(false); } }
        /// <summary>Restores native storm components before serialization.</summary>
        public override void OnSaveBegin(string slot) { if (_native!=null) _native.Save(true); }
        /// <summary>Reapplies replacement presentation after native serialization.</summary>
        public override void OnSaveEnd(string slot) { if (_native!=null) _native.Save(false); }
        /// <summary>Restores native state and releases all weather-owned jobs, hooks and assets.</summary>
        public override void Shutdown()
        {
            _running = false; _prepared = false; Clear();
            if (_native!=null) _native.Dispose(); _native=null; _hazards=null;

            if (_visuals != null) _visuals.Dispose(); _visuals = null; _draw = null;
            if (_audio != null) _audio.Dispose(); _audio = null;
            Scheduler.RemoveAll(this); Hooks.RemoveAll(this); Core.Patches.Unpatch(this); ModApi.RemoveAll(this);
        }
    }
}
