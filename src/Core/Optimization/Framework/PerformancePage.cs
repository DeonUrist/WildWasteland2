using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// The separate "Performance" tab of the settings window (between Modules and Debug; registered by Core's CorePages).
    /// The module switch, preset and measurements stay here. Individual optimization modes, stats and tuning live in Developer.
    /// </summary>
    public sealed partial class PerformancePage : ToolPage
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
            var m = PerformanceModule.Instance;
            if (m == null) { l.Text("Performance module not loaded."); return; }
            var entry = ModuleHost.Of(m);
            if (entry != null && entry.Config != null) l.Toggle(entry.Config.Enabled, "Performance optimizations");
            bool running = ModuleHost.IsRunning(m);
            l.Text(running
                ? "Experimental base-game optimizations. Choose a preset and compare measurements in the same scene. Changes apply immediately."
                : "The Performance module is off: the game runs unmodified.", running ? (Color?)null : Ledger.Danger);

            var p = PerformanceModule.PresetEntry;
            if (p != null)
            {
                int sel = l.Choices("Preset", Array.IndexOf(_presetValues, p.Value), _presets);
                if (_presetValues[sel] != p.Value) p.Value = _presetValues[sel];
                l.Text("PERFORMANCE favors speed; BALANCED is the compromise; VISUALS favors detail. VANILLA (default) disables changes to visuals, simulation and item cleanup, including extra culling (marked *). Exact optimizations remain active.");
            }

            DrawMeasurements(l);
        }

        // Wave A split (refactor): measurements and the Developer feature list are drawn by the Debug part
        // (src/Modules/Debug/Framework/_fromPerformance/PerformancePage.Debug.cs).
        /// <summary>Debug part: Telemetry measurements and the scheduler job table at the end of the tab.</summary>
        partial void DrawMeasurements(ToolLayout l);
    }
}
