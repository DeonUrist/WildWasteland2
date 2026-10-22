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
    /// EN6 (plan §5, research C §2 #10): Time.maximumDeltaTime 0.333 -> 0.1 (Max 0.15), so one slow frame no longer runs up to 16
    /// physics steps; Physics.sleepThreshold (the default for new bodies) 0.005 -> 0.14 from load, the value NWH FloatingOrigin
    /// gives every existing body after its first jump anyway (FloatingOrigin.cs AfterJump). Originals restored on disable.
    /// </summary>
    public sealed class TimePhysicsFeature : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "EN6"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Time and sleep"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Physics step cap and sleep threshold"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.World; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 20; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Caps physics catch-up after a slow frame (the game slows down instead of stalling) and lets resting bodies sleep from the start."; } }

        Knob _maxDt, _sleep;
        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
        public override void Bind(ModuleConfig cfg)
        {
            _maxDt = NewKnob(cfg, "Max delta time", "Time.maximumDeltaTime (vanilla 0.333).", 0.1f, 0.1f, 0.15f, 0.3333333f);
            _sleep = NewKnob(cfg, "Sleep threshold", "Physics.sleepThreshold for new bodies (vanilla 0.005; NWH sets 0.14 on all bodies after the first origin jump).", 0.14f, 0.14f, 0.14f, 0.005f);
        }

        bool _saved; float _dtOrig, _sleepOrig;
        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            if (!_saved) { _dtOrig = Time.maximumDeltaTime; _sleepOrig = Physics.sleepThreshold; _saved = true; }
            Apply();
        }
        /// <summary>Applies this feature's current preset values to its captured native world/engine settings.</summary>
        void Apply() { Time.maximumDeltaTime = Mathf.Max(Time.fixedDeltaTime, _maxDt.Value); Physics.sleepThreshold = Mathf.Max(0f, _sleep.Value); }
        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged() { Apply(); }
        /// <summary>Drops scene-bound references and refreshes this feature for the newly loaded scene.</summary>
        public override void OnSceneLoaded() { Apply(); }
        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable() { if (_saved) { Time.maximumDeltaTime = _dtOrig; Physics.sleepThreshold = _sleepOrig; _saved = false; } }
        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l) { l.Value("maximumDeltaTime / sleepThreshold", Time.maximumDeltaTime.ToString("0.###") + " / " + Physics.sleepThreshold.ToString("0.###")); }
    }
}
