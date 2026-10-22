using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// EN2, preset part. PlayerCamera and all 14 vehicle 3rd-person cameras run PPv2 TAA (antialiasingMode 3) with
    /// stationaryBlending 0 and motionBlending 0 (Sources data/hierarchy/scene1_game/PlayerCameraHolder.txt:L9,
    /// prefabs/R/Rustallion.txt:L63): jitter + motion vectors + resolve without any history blend = no anti-aliasing. Such a
    /// layer gets SMAA (Low: FXAA fast); stopNaN propagation off in Low/Balanced. PostProcessLayer is not written by ES3
    /// (no ES3Type for it) and no FSM touches it (Sources fsm/: no antialiasing/PostProcessLayer reference).
    /// </summary>
    public sealed class AntiAliasing : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "EN2"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Anti-aliasing"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Replace the no-op TAA with SMAA / FXAA"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.Rendering; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 3; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "The game's TAA has zero blending (costs time, smooths nothing). Performance uses FXAA, Balanced/Visuals SMAA."; } }

        Knob _mode, _quality, _nan;
        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
        public override void Bind(ModuleConfig cfg)
        {
            _mode = NewKnob(cfg, "Mode", "1 = FXAA, 2 = SMAA, 3 = keep TAA.", 1, 2, 2, 3);
            _quality = NewKnob(cfg, "SMAA quality", "0 Low, 1 Medium, 2 High.", 0, 1, 2, 0);
            _nan = NewKnob(cfg, "Stop NaN", "1 = keep PPv2 stopNaN propagation.", 0, 0, 1, 1);
        }

        /// <summary>Retains a post-processing layer's native anti-aliasing mode, quality and safety flags.</summary>
        sealed class Rec { public Component Layer; public int Mode, Quality; public bool Fast, Nan; }
        readonly Dictionary<int, Rec> _recs = new Dictionary<int, Rec>();
        Job _job; int _version = -1;
        static Type _layerType;
        static FieldInfo _fMode, _fNan, _fTaa, _fStat, _fMotion, _fSmaa, _fSmaaQ, _fFxaa, _fFast;

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            if (!Resolve()) throw new Exception("PostProcessLayer not found");
            _version = -1;
            _job = Scheduler.Interval(Module, "Core.Optimization.AntiAliasing", 1f, Tick);
            Tick();
        }

        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable()
        {
            if (_job != null) { _job.Remove(); _job = null; }
            foreach (var r in _recs.Values) { if (r.Layer != null) Write(r.Layer, r.Mode, r.Quality, r.Fast, r.Nan); }
            _recs.Clear();
        }

        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged()
        {
            foreach (var r in _recs.Values) if (r.Layer != null) ApplyTo(r);
        }

        /// <summary>Resolves supported post-processing types/members once and retains a native fallback if unavailable.</summary>
        static bool Resolve()
        {
            if (_layerType != null) return true;
            var t = AccessTools.TypeByName("UnityEngine.Rendering.PostProcessing.PostProcessLayer");
            if (t == null) return false;
            _fMode = AccessTools.Field(t, "antialiasingMode"); _fNan = AccessTools.Field(t, "stopNaNPropagation");
            _fTaa = AccessTools.Field(t, "temporalAntialiasing"); _fSmaa = AccessTools.Field(t, "subpixelMorphologicalAntialiasing"); _fFxaa = AccessTools.Field(t, "fastApproximateAntialiasing");
            _fStat = AccessTools.Field(_fTaa.FieldType, "stationaryBlending"); _fMotion = AccessTools.Field(_fTaa.FieldType, "motionBlending");
            _fSmaaQ = AccessTools.Field(_fSmaa.FieldType, "quality"); _fFast = AccessTools.Field(_fFxaa.FieldType, "fastMode");
            _layerType = t;
            return true;
        }

        /// <summary>Finds recognized camera layers and applies quality limits while preserving the captured native configuration.</summary>
        void Tick()
        {
            GameCameras.Refresh();
            if (GameCameras.Version == _version) return;
            _version = GameCameras.Version;
            var list = GameCameras.List;
            for (int i = 0; i < list.Count; i++)
            {
                var cam = list[i];
                if (cam == null || _recs.ContainsKey(cam.GetInstanceID())) continue;
                var layer = cam.GetComponent(_layerType);
                if (layer == null) continue;
                var r = new Rec { Layer = layer, Mode = Convert.ToInt32(_fMode.GetValue(layer)), Nan = (bool)_fNan.GetValue(layer) };
                var smaa = _fSmaa.GetValue(layer); var fxaa = _fFxaa.GetValue(layer);
                r.Quality = smaa != null ? Convert.ToInt32(_fSmaaQ.GetValue(smaa)) : 0;
                r.Fast = fxaa != null && (bool)_fFast.GetValue(fxaa);
                _recs[cam.GetInstanceID()] = r;
                ApplyTo(r);
            }
        }

        /// <summary>Applies the active anti-aliasing preset to one recorded post-processing layer.</summary>
        void ApplyTo(Rec r)
        {
            int mode = r.Mode, q = r.Quality; bool fast = r.Fast;
            if (r.Mode == 3 && DeadTaa(r.Layer))
            {
                int want = _mode.Int;
                if (want == 1) { mode = 1; fast = true; }
                else if (want == 2) { mode = 2; q = Mathf.Clamp(_quality.Int, 0, 2); }
            }
            bool nan = r.Nan && _nan.Value > 0.5f;
            Write(r.Layer, mode, q, fast, nan);
        }

        /// <summary>Checks whether a layer still carries the unsupported/dead temporal-AA implementation.</summary>
        static bool DeadTaa(Component layer)
        {
            var taa = _fTaa.GetValue(layer);
            if (taa == null) return false;
            return (float)_fStat.GetValue(taa) <= 0.001f && (float)_fMotion.GetValue(taa) <= 0.001f;
        }

        /// <summary>Writes the supported layer's anti-aliasing mode/quality and safety flags through cached accessors.</summary>
        static void Write(Component layer, int mode, int quality, bool fast, bool nan)
        {
            _fMode.SetValue(layer, Enum.ToObject(_fMode.FieldType, mode));
            _fNan.SetValue(layer, nan);
            var smaa = _fSmaa.GetValue(layer); if (smaa != null) _fSmaaQ.SetValue(smaa, Enum.ToObject(_fSmaaQ.FieldType, quality));
            var fxaa = _fFxaa.GetValue(layer); if (fxaa != null) _fFast.SetValue(fxaa, fast);
        }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l) { l.Value("Cameras", _recs.Count.ToString()); }
    }
}
