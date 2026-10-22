using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Weather
{
    // Native encounters keep their save anchors; Weather owns presentation and annular hazards.
    // Wave A split (refactor): storm regions/tick/hazards live in src/Modules/Survival/Weather/_fromWeather/WeatherModule.Regions.cs,
    // diagnostic snapshot in src/Modules/Debug/_fromWeather/WeatherModule.Debug.cs, the F3 preview page in src/Modules/Tools/_fromWeather/WeatherModule.Tools.cs.
    public sealed partial class WeatherModule : ModuleBase, IWeatherRegions
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
        public override string Id { get { return "Weather"; } }
        public override string DisplayName { get { return "Weather"; } }
        public int Count { get { return _count; } }
        public bool MapAreas { get { return _mapAreas.Value; } set { _mapAreas.Value = value; } }
        public float HurricaneDensity { get { return _density.Value; } set { _density.Value = value; } }
        public float FrontWidthKm { get { return _width.Value; } set { _width.Value = value; } }
        public float DepthKm { get { return _depth.Value; } set { _depth.Value = value; } }

        public override void BindConfig(ModuleConfig cfg)
        {
            WeatherPalette.Bind(cfg);
            _mapAreas = cfg.Player("Map", "Storm areas", true,
                "Show moving Weather storm areas on the map. Critical storms lift vehicles and damage actors in hurricane sand walls; eyes are safe.");
            _density = cfg.Player("Storms", "Hurricanes per km²", .8f, "Columns per square kilometre of critical storm area, rounded to the nearest whole column. Zero disables columns. Applies live and does not change with Performance presets.", new AcceptableValueRange<float>(0,1));
            _width = cfg.Player("Storms", "Front width (km)", 20f, "Width of the broad leading sand front. Applies live to preview regions and their map areas.", new AcceptableValueRange<float>(6,20));
            _depth = cfg.Player("Storms", "Depth (km)", 4f, "Depth behind the front; sand fades and the footprint narrows toward the rear. Applies live.", new AcceptableValueRange<float>(1,4));
        }

        public override void Init()
        {
            _running = true; _count = 0; _clock = 0; _nativeAssetsReady=false;
            if (!Core.Patches.Patch(this,Core.Patches.Method(typeof(UnityEngine.AzureSky.AzureFogScattering),"OnRenderImage"),
                prefix: Core.Patches.Method(typeof(WeatherSky),"NativeFogBefore"), finalizer: Core.Patches.Method(typeof(WeatherSky),"NativeFogAfter")))
                throw new System.InvalidOperationException("Weather requires the native Azure fog scope.");
            _audio = new WeatherAudio(System.IO.Path.Combine(Dir,"sound","sandstorm.wav"));
            PrepareAssets();
            Scheduler.Interval(this,"Weather.PrepareNativeAssets",1,PrepareNativeAssets);
            Services.Weather = this;
            _draw = Scheduler.EveryFrame(this, "Moving storm visuals", Tick, Phase.LateUpdateLast);
            _draw.Enabled = false;
            _native = new WeatherNative(this); _hazards = new WeatherHazards();
            Scheduler.EveryFrame(this,"Weather.HurricaneHazards",TickHazards,Phase.FixedUpdate);
        }

        public bool TryGet(int index, out WeatherRegion region)
        {
            region = index >= 0 && index < _count ? _regions[index] : default(WeatherRegion);
            return index >= 0 && index < _count;
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Clear(); if (_native!=null) _native.Reset(); }
        public override void OnLoadBegin(string slot) { Clear(); if (_native!=null) _native.Reset(); PrepareAssets(); }
        public override void OnWorldReady() { PrepareAssets(); if (_native!=null) { _native.Scan(); _native.Save(false); } }
        public override void OnSaveBegin(string slot) { if (_native!=null) _native.Save(true); }
        public override void OnSaveEnd(string slot) { if (_native!=null) _native.Save(false); }
        public override void Shutdown()
        {
            _running = false; Clear();
            if (_native!=null) _native.Dispose(); _native=null; _hazards=null;
            if (ReferenceEquals(Services.Weather, this)) Services.Weather = null;
            if (_visuals != null) _visuals.Dispose(); _visuals = null; _draw = null;
            if (_audio != null) _audio.Dispose(); _audio = null;
        }
    }
}
