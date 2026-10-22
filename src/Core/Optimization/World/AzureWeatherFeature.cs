using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Den.Tools.Tasks;
using HarmonyLib;
using MapMagic.Core;
using NwhOrigin = NWH.Common.FloatingOrigin.FloatingOrigin;
using UnityEngine;
using UnityEngine.AzureSky;
using UnityEngine.Events;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;
using Object = UnityEngine.Object;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// EN7 (weather part): AzureWeatherController.FixedUpdate re-evaluates every weather profile curve and writes ~44 override
    /// properties by reflection (boxing) on every physics step (AzureWeatherController.cs:L115-L118, L211-L277). Outside a weather
    /// transition the result only depends on the slowly moving time of day, so it is evaluated at the preset interval (0.2 s
    /// Balanced); during a transition (m_isWeatherChanging, 7 s fades) it runs once per frame so the fade stays smooth.
    /// </summary>
    public sealed class AzureWeatherFeature : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "EN7"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Azure weather"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Sky weather evaluation rate"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.World; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 31; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Sky/fog weather curves are re-evaluated a few times per second instead of on every physics step (every frame during weather changes)."; } }

        Knob _interval;
        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
        public override void Bind(ModuleConfig cfg) { _interval = NewKnob(cfg, "Interval (s)", "Weather evaluation interval outside transitions (vanilla: every physics step).", 0.25f, 0.2f, 0.1f, 0f); }

        internal static bool On; static float _every, _next; static int _frame = -1; internal static long Skipped;
        static readonly Func<AzureWeatherController, bool> _changing = Getter();
        /// <summary>Resolves the supported Azure weather-transition flag once, retaining native behavior when unavailable.</summary>
        static Func<AzureWeatherController, bool> Getter()
        {
            // compiled once (mcs cannot dereference HarmonyX FieldRef returns)
            try
            {
                var fi = AccessTools.Field(typeof(AzureWeatherController), "m_isWeatherChanging");
                if (fi == null) return null;
                return Access.Getter<AzureWeatherController, bool>(typeof(AzureWeatherController), "m_isWeatherChanging");
            }
            catch (Exception e) { Log.Warn(OptimizationRuntime.Instance, "EN7 weather: transition flag not readable (" + e.Message + ") - weather left vanilla"); return null; }
        }
        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            if (_changing == null) throw new Exception("AzureWeatherController.m_isWeatherChanging not readable");
            if (!EnsurePatched(typeof(Patch))) throw new Exception("AzureWeatherController.FixedUpdate patch failed");
            _every = Mathf.Max(0f, _interval.Value); On = true;
        }
        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged() { _every = Mathf.Max(0f, _interval.Value); }
        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable() { On = false; }
        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l) { l.Value("Evaluations skipped", Skipped.ToString()); }
        [HarmonyPatch]
        static class Patch
        {
            /// <summary>Native engine callback prefix: immediately yields while the enclosing feature is disabled and otherwise applies its documented per-thread/frame cadence.</summary>
            [HarmonyPatch(typeof(AzureWeatherController), "FixedUpdate"), HarmonyPrefix]
            static bool Pre(AzureWeatherController __instance)
            {
                if (!On) return true;
                int f = Time.frameCount;
                if (f == _frame) { Skipped++; return false; }                // never twice in one frame
                if (_changing != null && !_changing(__instance))
                {
                    float now = Time.unscaledTime;
                    if (now < _next) { Skipped++; return false; }
                    _next = now + _every;
                }
                _frame = f; return true;
            }
        }
    }
}
