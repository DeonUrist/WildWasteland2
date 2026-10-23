using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using ES3Internal;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Survival
{
    public sealed partial class SurvivalModule : ModuleBase, ISurvivalCatalog
    {
        internal const string GUID = "com.denis.apocalypter.stewpot";
        internal static string Assets;
        internal static ConfigEntry<bool> AllowStewPotSpilling;
        internal static ConfigEntry<float> Scale, NutritionMultiplier, CookingRate, InsanityBoilRate, SpoilingRate;
        private GameObject _effects;
        private readonly WeatherRuntime _weather = new WeatherRuntime();
        private ConfigEntry<bool> _weatherEnabled;
        private bool _active, _weatherActive;
        private Insanity _insanity;
        private SleepRecovery _sleepRecovery;
        private CookingSkills _cookingSkills;

        /// <summary>Canonical settings and resource owner.</summary>
        public override string Id { get { return "Survival"; } }
        /// <summary>Name shown in the module shell.</summary>
        public override string DisplayName { get { return "Survival"; } }
        /// <summary>Standalone stew-pot replacement conflict.</summary>
        public override string[] ConflictsWith { get { return new[] { GUID }; } }

        /// <summary>Binds the Stew Pot tools.cfg entries (model scale, debug switches, cooking/spoiling tuning) in display order.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            _weatherEnabled = cfg.Player("Weather", "Enabled", true, "Enable moving storm presentation and hurricane hazards.");
            _weatherEnabled.SettingChanged += WeatherChanged;
            _weather.SetParent(this);
            _weather.Bind(cfg);
            Scale = Bind(cfg, "Model", "Scale", .2f, "Pot model scale. Restart to apply.", .1f, .5f);
            AllowStewPotSpilling = cfg.Tool("Debug", "AllowStewPotSpilling", false, "Allow tilted stew pots to spill their contents, including pouring into another pot. Applies immediately.");
            NutritionMultiplier = Bind(cfg, "Stew Pot", "Nutrition conversion multiplier", 2f, "Cooked nutrition produced per raw nutrition point.", 1f, 10f);
            CookingRate = Bind(cfg, "Stew Pot", "Cooking rate", .25f, "Raw nutrition converted per unpaused second. Dog meat: 5 nutrition / 20 seconds.", .001f, 100f);
            InsanityBoilRate = Bind(cfg, "Stew Pot", "Insanity boilaway rate", .25f, "Insanity removed per unpaused second over a native Fire trigger.", 0f, 100f);
            SpoilingRate = Bind(cfg, "Stew Pot", "Spoiling rate", 15f / 5760f, "Spoiled nutrition accumulated per unpaused second.", 0f, 100f);
        }

        /// <summary>Binds one ranged float tool setting.</summary>
        private ConfigEntry<float> Bind(ModuleConfig cfg, string section, string key, float value, string description, float min, float max)
        {
            return cfg.Tool(section, key, value, description, new AcceptableValueRange<float>(min, max));
        }

        /// <summary>Starts pot interaction, native prefab handling and environmental services.</summary>
        public override void Init()
        {
            _active = true;
            _insanity = new Insanity(this); _insanity.Init();
            _sleepRecovery = new SleepRecovery(this); _sleepRecovery.Init();
            _cookingSkills = new CookingSkills(this); _cookingSkills.Init();
            _stormQueries = new StormQueries(this, _weather);
            WorldIndex.WatchComponent<UnityEngine.AzureSky.AzureTimeController>(this, DaylightState.Observe);
            Assets = Path.Combine(Dir, "Models");
            _effects = new GameObject("StewPot.Runner") { hideFlags = HideFlags.HideAndDontSave };
            _effects.transform.SetParent(Runner.Host.transform, false);
            Runtime.Init(_effects.transform);
            Hooks.GetPrefab.Resolver(this, ResolvePrefab);
            Hooks.GetButtonDownUpdate.Prefix(this, Runtime.DrinkInput);

            Core.Patches.Patch(this, AccessTools.Method(typeof(ES3Prefab), "Awake"), postfix: AccessTools.Method(typeof(SurvivalModule), "PrefabAwake"));

            StewLogic.LoadSound();
            Scheduler.EveryFrame(this, "Survival.StewPot", Runtime.LateTick, Phase.LateUpdateLast);
            Scheduler.Interval(this, "Survival.References", 1f, Runtime.Resolve).RunNow();
            Scheduler.Sliced(this, "Survival.Existing pots", 500, Runtime.Discover);
            StartWeather();
            NativeStormProtection.Install(this);
            Scheduler.EveryFrame(this, "Survival.StormShelter", NativeStormProtection.Tick);
            Core.Log.Always(null,"[Survival] Stew Pot and environment initialized; native saves retained.");
        }

        /// <summary>Resolves only the custom pot prefab through the native catalog chain.</summary>
        private static ES3Prefab ResolvePrefab(long id)
        {
            return id == Factory.PrefabId ? Factory.Template().GetComponent<ES3Prefab>() : HumanFood.Resolve(id);
        }

        /// <summary>Registers a newly awakened saved pot without adding saved components.</summary>
        private static void PrefabAwake(ES3Prefab __instance)
        {
            if (__instance.prefabId == Factory.PrefabId) Runtime.Register(__instance.gameObject);
        }

        /// <summary>Clears scene references only when the main scene changes.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (mode == LoadSceneMode.Additive) return; if (_insanity != null) _insanity.Clear(); if (_sleepRecovery != null) _sleepRecovery.Clear(); if (_cookingSkills != null) _cookingSkills.Clear(); Runtime.Clear(); DaylightState.Reset(); if (_stormQueries != null) _stormQueries.Clear(); NativeStormProtection.Restore(); NativeStormProtection.ResetForScene(); if (_weatherActive) _weather.OnSceneLoaded(scene, mode); }
        /// <summary>Queues existing pot discovery and prepares weather after world readiness.</summary>
        public override void OnWorldReady() { if (_insanity != null) { _insanity.WorldReady(); _insanity.Resolve(); } if (_cookingSkills != null) _cookingSkills.Load(); Runtime.BeginDiscovery(); if (_weatherActive) _weather.OnWorldReady(); }
        /// <summary>Retains native storm anchors during world serialization.</summary>
        public override void OnSaveBegin(string slot) { if (_insanity != null) _insanity.Save(slot); if (_cookingSkills != null) _cookingSkills.Save(slot); if (_weatherActive) _weather.OnSaveBegin(slot); }
        /// <summary>Restores weather presentation after world serialization.</summary>
        public override void OnSaveEnd(string slot) { if (_weatherActive) _weather.OnSaveEnd(slot); }
        /// <summary>Clears weather state and prepares retained resources before loading another world.</summary>
        public override void OnLoadBegin(string slot) { if (_insanity != null) _insanity.LoadBegin(); if (_sleepRecovery != null) _sleepRecovery.Clear(); if (_cookingSkills != null) _cookingSkills.LoadBegin(); if (_weatherActive) _weather.OnLoadBegin(slot); }
        /// <summary>Publishes gameplay environment queries independently of the weather rendering switch.</summary>
        public override void ProvideApis()
        {
            ModApi.Provide<ISurvivalCatalog>(this, this);
            ModApi.Provide<IMentalBreak>(this, _insanity);
            ModApi.Provide<IPlayerMentalEffects>(this, _insanity);
            ModApi.Provide<ICookingSkills>(this, _cookingSkills);
            ModApi.Provide<IShelterRegistry>(this, _shelters);
            ModApi.Provide<IStormQueries>(this, _stormQueries);
            ModApi.Provide<IDayLight>(this, _daylight);
            PublishWeather();
        }
        /// <summary>Restarts only weather-owned runtime resources when its feature setting changes.</summary>
        private void WeatherChanged(object sender, EventArgs args)
        { if (!_active) return; StopWeather(); StartWeather(); Core.Hooks.ApplyUsed(); if (_weatherActive && GameRefs.InPlay) _weather.OnWorldReady(); }
        /// <summary>Initializes the dedicated weather ownership scope once and rolls back partial startup.</summary>
        private void StartWeather()
        {
            if (_weatherActive || !_weatherEnabled.Value) return;
            try { _weather.Init(); _weatherActive = true; PublishWeather(); }
            catch { _weather.Shutdown(); throw; }
        }
        /// <summary>Publishes real runtime state only while moving weather is enabled.</summary>
        private void PublishWeather()
        {
            if (_weatherActive) { ModApi.Provide<IWeatherRegions>(this, _weather); ModApi.Provide<IWeatherPreview>(this, _weather); }
            else { ModApi.Withdraw<IWeatherRegions>(this); ModApi.Withdraw<IWeatherPreview>(this); }
        }
        /// <summary>Restores native weather and releases only weather-owned hooks and jobs.</summary>
        private void StopWeather()
        { if (_weatherActive) { _weatherActive = false; _weather.Shutdown(); } PublishWeather(); }
        /// <summary>Restores native storm behavior and releases runtime pot effects.</summary>
        public override void Shutdown()
        {
            _active = false; StopWeather(); NativeStormProtection.Restore(); NativeStormProtection.ResetForScene();
            if (_insanity != null) _insanity.Shutdown();
            if (_sleepRecovery != null) _sleepRecovery.Clear();
            if (_cookingSkills != null) _cookingSkills.Shutdown();
            StewLogic.StopSoundLoad();
            Runtime.Shutdown(); _shelters.Clear(); if (_stormQueries != null) _stormQueries.Clear();
            if (_effects != null) UnityEngine.Object.Destroy(_effects);
            _effects = null;
        }

        /// <summary>Contributes the stew-pot tuning page to the shared tools shell.</summary>
        public override IEnumerable<ToolPage> ToolPages()
        {
            return new ToolPage[] { new ConfigPage(this, ToolGroups.General, "Survival / Stew Pot", Config.Tools,
                "Survival / Model", "Survival / Stew Pot") { Intro = "Spawn through Apocaspawner: Camp & Farming > Stew Pot. Native Use drinks; Secondary use adds held meat. Model scale needs a restart." } };
        }

        /// <summary>Resolves the pot only on demand; no scene prefab discovery occurs during config binding.</summary>
        public GameObject StewPot { get { return Factory.Template(); } }
    }
}
