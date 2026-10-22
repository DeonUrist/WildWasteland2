using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// Debug part of the Performance tab (Wave A split): Telemetry measurements, the scheduler job table and the Developer
    /// feature list (modes, stats, shadow results, tuning overrides). B03 takes this over.
    /// </summary>
    public sealed partial class PerformancePage
    {
        bool _tuning;
        static readonly string[] _modes2 = { "Off", "On" };
        static readonly string[] _modes3 = { "Off", "On", "Shadow" };

        /// <summary>Tail of the Performance tab: Telemetry measurements and the per-module/per-job scheduler table.</summary>
        partial void DrawMeasurements(ToolLayout l)
        {
            l.Heading("Measurements");
            Telemetry.Draw(l);
            l.Heading("Performance and jobs");
            CorePages.Performance(l);
        }

        /// <summary>Developer page section: every optimization's mode, stats, errors, shadow mismatches and tuning overrides.</summary>
        internal void DrawDeveloper(ToolLayout l)
        {
            var m = PerformanceModule.Instance;
            if (m == null) { l.Text("Performance module not loaded."); return; }
            bool running = ModuleHost.IsRunning(m);
            l.Text("Individual optimization modes, feature statistics and tuning overrides. Shadow keeps the original game code running and compares the optimized result. Presets and overall measurements are on the Performance tab.");
            string cat = null;
            foreach (var f in m.Features)
            {
                if (f.Category != cat) { cat = f.Category; l.Heading(cat); }
                var labels = f.SupportsShadow ? _modes3 : _modes2;
                int cur = (int)f.ModeEntry.Value; if (cur >= labels.Length) cur = 1;
                string state = f.Running == FeatureMode.Off ? (f.ModeEntry.Value != FeatureMode.Off && running && f.UsesPreset && PerformanceModule.Preset == PerfPreset.Vanilla ? "  (Vanilla preset)" : "") : "";
                int next = l.Choices(f.PlanId + "  " + f.Title + (f.UsesPreset ? " *" : ""), cur, labels);
                if (next != cur) f.ModeEntry.Value = (FeatureMode)next;
                if (!string.IsNullOrEmpty(f.Help) || state.Length > 0) l.Text(f.Help + state);
                if (f.LastError != null) l.Text("Error: " + f.LastError, Ledger.Danger);
                if (f.Mismatches > 0) l.Text("Shadow mismatches: " + f.Mismatches + " (see log)", Ledger.Danger);
                if (f.Running != FeatureMode.Off)
                {
                    try { f.DrawStats(l); } catch (Exception e) { l.Text("Stats error: " + e.Message, Ledger.Danger); }
                }
            }

            l.Space(10);
            _tuning = l.Toggle("Show tuning overrides (-1 = follow preset)", _tuning);
            if (_tuning)
                foreach (var f in m.Features)
                    foreach (var k in f.Knobs) l.Entry(k.Override);
        }
    }
}
