using UnityEngine;
using UnityEngine.Scripting;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// EN8 (research C Tier 3 "GC settings", §3). The player runs Unity's incremental GC (boot.config gc-max-time-slice=3 per the plan);
    /// a smaller time slice spreads the mark work over more frames (shorter per-frame GC cost, more frames with GC work).
    /// Only applied when the GC is incremental; the original slice is restored on disable. Never switches the GC mode.
    /// </summary>
    public sealed class GcSlice : Feature
    {
        public override string PlanId { get { return "EN8"; } }
        public override string Key { get { return "GC time slice"; } }
        public override string Title { get { return "Incremental GC time slice"; } }
        public override string Category { get { return Categories.World; } }
        public override int Order { get { return 90; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Shorter incremental garbage-collection slices per frame (smoother frame times while the GC runs)."; } }

        Knob _ms;
        public override void Bind(ModuleConfig cfg) { _ms = NewKnob(cfg, "Slice (ms)", "Incremental GC time slice per frame.", 1f, 1.5f, 2f, 3f); }

        ulong _orig; bool _set;

        protected override void OnEnable(FeatureMode mode) { Apply(); }
        public override void OnSettingsChanged() { Apply(); }

        void Apply()
        {
            if (!GarbageCollector.isIncremental) return;
            if (!_set) { _orig = GarbageCollector.incrementalTimeSliceNanoseconds; _set = true; }
            float ms = Mathf.Clamp(_ms.Value, 0.25f, 10f);
            GarbageCollector.incrementalTimeSliceNanoseconds = (ulong)(ms * 1000000f);
        }

        protected override void OnDisable()
        {
            if (_set) GarbageCollector.incrementalTimeSliceNanoseconds = _orig;
            _set = false;
        }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("GC", (GarbageCollector.isIncremental ? "incremental, slice " + (GarbageCollector.incrementalTimeSliceNanoseconds / 1000000.0).ToString("0.##") + " ms" : "not incremental (no change)")
                + (_set ? " (vanilla " + (_orig / 1000000.0).ToString("0.##") + " ms)" : ""));
        }
    }
}
