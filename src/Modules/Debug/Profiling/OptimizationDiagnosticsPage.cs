using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Core.Optimization;

namespace WildWasteland.Modules.Debug
{
    /// <summary>
    /// Debug part of the Performance tab (Wave A split): Telemetry measurements, the scheduler job table and the Developer
    /// feature list (modes, stats, shadow results, tuning overrides). B03 takes this over.
    /// </summary>
    internal static class OptimizationDiagnosticsPage
    {
        static bool _tuning;
        static readonly string[] _modes2 = { "Off", "On" };
        static readonly string[] _modes3 = { "Off", "On", "Shadow" };
        static readonly string[] _presets = { "Vanilla", "Performance", "Balanced", "Visuals" };

        /// <summary>Developer page section: every optimization's mode, stats, errors, shadow mismatches and tuning overrides.</summary>
        internal static void DrawDeveloper(ToolLayout l)
        {
            var m = OptimizationRuntime.Instance;
            if (m == null) { l.Text("Core optimization runtime is unavailable."); return; }
            bool running = m.Running;
            l.Text("Experimental settings apply while Performance and Developer mode are on. Features marked * require a preset other than Vanilla. Shadow compares results while the game code still runs.");
            var preset = OptimizationRuntime.PresetEntry;
            if (preset != null) preset.Value = (PerfPreset)l.Choices("Preset", (int)preset.Value, _presets);
            if (FarClip.DistantTerrainEntry != null) l.Toggle(FarClip.DistantTerrainEntry, "Distant terrain");
            string cat = null;
            foreach (var f in m.Features)
            {
                if (f.Category != cat) { cat = f.Category; l.Heading(cat); }
                var labels = f.SupportsShadow ? _modes3 : _modes2;
                int cur = (int)f.ModeEntry.Value; if (cur >= labels.Length) cur = 1;
                bool needsPreset = f.UsesPreset && !(f is GcSlice);
                string state = f.Running == FeatureMode.Off && cur != 0 && running && needsPreset && OptimizationRuntime.Preset == PerfPreset.Vanilla ? "  (Vanilla preset)" : "";
                if (f is GeneratedLods) l.Value(f.Title, "Always on");
                else
                {
                    int next = l.Choices(f.PlanId + "  " + f.Title + (needsPreset ? " *" : ""), cur, labels);
                    if (next != cur) f.ModeEntry.Value = (FeatureMode)next;
                }
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
