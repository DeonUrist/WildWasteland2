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
    /// EN7 (sun part, research C §2 #11): AzureTimeController.FixedUpdate recomputes the celestial coordinates and the sun/moon
    /// light rotation on every physics step (AzureTimeController.cs:L286-L290). Its inputs (timeline, date, location) change only in
    /// its Update, so 2-3 physics steps in one frame compute the same result: the prefix lets only the first step of a frame run. Exact.
    /// </summary>
    public sealed class AzureSunFeature : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "EN7"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Azure sun"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Sky sun position once per frame"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.World; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 30; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "The sky's sun position is computed once per rendered frame instead of on every physics step (exact)."; } }
        internal static bool On; static int _frame = -1; internal static long Skipped;
        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode) { if (!EnsurePatched(typeof(Patch))) throw new Exception("AzureTimeController.FixedUpdate patch failed"); On = true; }
        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable() { On = false; }
        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l) { l.Value("Duplicate steps skipped", Skipped.ToString()); }
        [HarmonyPatch]
        static class Patch
        {
            /// <summary>Native engine callback prefix: immediately yields while the enclosing feature is disabled and otherwise applies its documented per-thread/frame cadence.</summary>
            [HarmonyPatch(typeof(AzureTimeController), "FixedUpdate"), HarmonyPrefix]
            static bool Pre()
            {
                if (!On) return true;
                int f = Time.frameCount;
                if (f == _frame) { Skipped++; return false; }
                _frame = f; return true;
            }
        }
    }
}
