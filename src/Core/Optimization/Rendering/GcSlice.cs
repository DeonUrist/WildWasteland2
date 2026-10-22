using UnityEngine;
using UnityEngine.Scripting;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// EN8 (research C Tier 3 "GC settings", §3). The player runs Unity's incremental GC (boot.config gc-max-time-slice=3 per the plan);
    /// a smaller time slice spreads the mark work over more frames (shorter per-frame GC cost, more frames with GC work).
    /// Only applied when the GC is incremental; the original slice is restored on disable. Never switches the GC mode.
    /// </summary>
    public sealed class GcSlice : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "EN8"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "GC time slice"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Incremental GC time slice"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.World; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 90; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Shorter incremental garbage-collection slices per frame (smoother frame times while the GC runs)."; } }

        Knob _ms;
        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
        public override void Bind(ModuleConfig cfg) { _ms = NewKnob(cfg, "Slice (ms)", "Incremental GC time slice per frame.", 1f, 1.5f, 2f, 3f); }

        ulong _orig; bool _set;

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode) { Apply(); }
        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged() { Apply(); }

        /// <summary>Sets the supported incremental-GC time slice from the current preset/override.</summary>
        void Apply()
        {
            if (!GarbageCollector.isIncremental) return;
            if (!_set) { _orig = GarbageCollector.incrementalTimeSliceNanoseconds; _set = true; }
            float ms = Mathf.Clamp(_ms.Value, 0.25f, 10f);
            GarbageCollector.incrementalTimeSliceNanoseconds = (ulong)(ms * 1000000f);
        }

        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable()
        {
            if (_set) GarbageCollector.incrementalTimeSliceNanoseconds = _orig;
            _set = false;
        }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l)
        {
            l.Value("GC", (GarbageCollector.isIncremental ? "incremental, slice " + (GarbageCollector.incrementalTimeSliceNanoseconds / 1000000.0).ToString("0.##") + " ms" : "not incremental (no change)")
                + (_set ? " (vanilla " + (_orig / 1000000.0).ToString("0.##") + " ms)" : ""));
        }
    }
}
