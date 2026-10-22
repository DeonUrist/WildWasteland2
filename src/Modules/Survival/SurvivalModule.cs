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
    public sealed partial class SurvivalModule : ModuleBase
    {
        internal const string GUID = "com.denis.apocalypter.stewpot";
        internal static string Assets;
        internal static ConfigEntry<bool> AllowStewPotSpilling;
        internal static ConfigEntry<float> Scale, NutritionMultiplier, CookingRate, InsanityBoilRate, SpoilingRate;
        private GameObject _effects;
        private Coroutine _sound;

        public override string Id { get { return "Survival"; } }
        public override string DisplayName { get { return "Survival"; } }
        public override string[] ConflictsWith { get { return new[] { GUID }; } }

        /// <summary>Binds the Stew Pot tools.cfg entries (model scale, debug switches, cooking/spoiling tuning) in display order.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
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

        // Wave A split (refactor): parts owned by other target modules live in partial files in their folders.
        /// <summary>Loot part: installs the natural Stew Pot substitution hook (src/Modules/Loot/_fromSurvival/SurvivalModule.Loot.cs).</summary>
        partial void InitLoot();
        /// <summary>Compatibility part: installs the Apocaspawner catalog bridge (src/Modules/Compatibility/_fromSurvival/SurvivalModule.Compatibility.cs).</summary>
        partial void InitCompatibility();
        /// <summary>Compatibility part: removes the Apocaspawner catalog entry again.</summary>
        partial void ShutdownCompatibility();

        public override void Init()
        {
            InstallAssets();
            Assets = Path.Combine(Dir, "Models");
            _effects = new GameObject("StewPot.Runner") { hideFlags = HideFlags.HideAndDontSave };
            _effects.transform.SetParent(Runner.Host.transform, false);
            Runtime.Init(_effects.transform);
            Hooks.GetPrefab.Resolver(this, ResolvePrefab);
            Hooks.GetButtonDownUpdate.Prefix(this, Runtime.DrinkInput);
            InitLoot();
            Core.Patches.Patch(this, AccessTools.Method(typeof(ES3Prefab), "Awake"), postfix: AccessTools.Method(typeof(SurvivalModule), "PrefabAwake"));
            InitCompatibility();
            _sound = Scheduler.StartCoroutine(StewLogic.LoadSound());
            Scheduler.EveryFrame(this, "Survival.StewPot", Runtime.LateTick, Phase.LateUpdateLast);
            Scheduler.Interval(this, "Survival.References", 1f, Runtime.Resolve).RunNow();
            Scheduler.Sliced(this, "Survival.Existing pots", 500, Runtime.Discover);
            Core.Log.Always("[Survival] Stew Pot 0.1.1 integrated; native pot saves retained.");
        }

        private static ES3Prefab ResolvePrefab(long id)
        {
            return id == Factory.PrefabId ? Factory.Template().GetComponent<ES3Prefab>() : null;
        }

        private static void PrefabAwake(ES3Prefab __instance)
        {
            if (__instance.prefabId == Factory.PrefabId) Runtime.Register(__instance.gameObject);
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Runtime.Clear(); }
        public override void OnWorldReady() { Runtime.BeginDiscovery(); }
        public override void Shutdown()
        {
            if (_sound != null) Scheduler.StopCoroutine(_sound);
            _sound = null;
            Runtime.Shutdown(); ShutdownCompatibility();
            if (_effects != null) UnityEngine.Object.Destroy(_effects);
            _effects = null;
        }

        public override IEnumerable<ToolPage> ToolPages()
        {
            return new ToolPage[] { new ConfigPage(this, ToolGroups.General, "Survival / Stew Pot", Config.Tools,
                "Survival / Model", "Survival / Stew Pot") { Intro = "Spawn through Apocaspawner: Camp & Farming > Stew Pot. Native Use drinks; Secondary use adds held meat. Model scale needs a restart." } };
        }

        /// <summary>Extracts the embedded Survival Models/Sounds files into the module folder when they are missing (custom files are kept).</summary>
        private void InstallAssets()
        {
            const string prefix = "Survival.";
            var assembly = typeof(SurvivalModule).Assembly;
            foreach (string folder in new[] { "Models", "Sounds" })
            {
                string destination = Path.Combine(Dir, folder);
                Directory.CreateDirectory(destination);
                string resourcePrefix = prefix + folder + ".";
                foreach (string resource in WildWasteland.Core.DiskResources.Names)
                {
                    if (!resource.StartsWith(resourcePrefix, StringComparison.Ordinal)) continue;
                    string target = Path.Combine(destination, resource.Substring(resourcePrefix.Length));
                    if (File.Exists(target)) continue;
                    using (var input = WildWasteland.Core.DiskResources.Open(resource))
                    using (var output = File.Create(target)) input.CopyTo(output);
                }
            }
        }
    }
}
