using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// The separate "Performance" tab of the settings window (between Modules and Debug; registered by Core's CorePages).
    /// The Core optimization master and preset stay here; measurement and per-feature controls belong to Debug.
    /// </summary>
    public sealed class PerformancePage : ToolPage
    {
        static PerformancePage _instance;
        /// <summary>The single Performance tab (created on first use by CorePages.Register).</summary>
        public static PerformancePage Instance { get { return _instance ?? (_instance = new PerformancePage()); } }

        /// <summary>Tab in the General group, visible while the module is off (it holds the module switch).</summary>
        PerformancePage() { Group = ToolGroups.General; Title = "Performance"; ShowWhenModuleOff = true; }

        static readonly string[] _presets = { "PERFORMANCE", "BALANCED", "VISUALS", "VANILLA" };
        static readonly PerfPreset[] _presetValues = { PerfPreset.Low, PerfPreset.Balanced, PerfPreset.Max, PerfPreset.Vanilla };

        /// <summary>Module switch, preset choice, then the Debug measurements.</summary>
        public override void Draw(ToolLayout l)
        {
            var m = OptimizationRuntime.Instance;
            if (m == null) { l.Text("Performance module not loaded."); return; }
            if (OptimizationRuntime.EnabledEntry != null) l.Toggle(OptimizationRuntime.EnabledEntry, "Performance optimizations");
            bool running = m.Running;
            l.Text(running
                ? "Experimental base-game optimizations. Choose a preset and compare measurements in the same scene. Changes apply immediately."
                : "Performance optimizations are off; generated mesh LODs remain active at 2x distance.", running ? (Color?)null : Ledger.Danger);

            var p = OptimizationRuntime.PresetEntry;
            if (p != null)
            {
                int sel = l.Choices("Preset", Array.IndexOf(_presetValues, p.Value), _presets);
                if (_presetValues[sel] != p.Value) p.Value = _presetValues[sel];
                l.Text("PERFORMANCE favors speed; BALANCED is the compromise; VISUALS favors detail. VANILLA (default) keeps generated mesh LODs at 2x distance and disables other changes to visuals, simulation and item cleanup, including extra culling (marked *). Exact optimizations remain active.");
            }
        }
    }
}
