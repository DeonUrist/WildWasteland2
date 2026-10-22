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
    /// EN6 (origin part, research C §0): every NWH FloatingOrigin jump runs FindObjectsOfType&lt;Rigidbody&gt;().ToList() in its
    /// BeforeJump and again in AfterJump listener (FloatingOrigin.cs). Nothing between the two (moving scene roots, pausing/playing
    /// world-space particles) creates, destroys or (de)activates a body, so both find the same set: the listeners are replaced by
    /// two that share one FindObjectsOfType array and skip the List copies (same sleepThreshold writes, same Physics.SyncTransforms).
    /// The ParticleSystem scan inside Jump is unchanged. No Harmony patch (WW Core owns FloatingOrigin.Jump through Hooks.OriginJump);
    /// listeners are swapped on the live FloatingOrigin.Instance (checked once a second) and swapped back on disable.
    /// </summary>
    public sealed class OriginJumpFeature : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "EN6"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Origin jump"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Floating-origin jump body scan"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.World; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 21; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Floating-origin jumps scan the scene's rigidbodies once instead of twice (exact)."; } }

        static Rigidbody[] _bodies;
        NwhOrigin _fo; UnityAction _origBefore, _origAfter;
        static readonly UnityAction _myBefore = MyBefore, _myAfter = MyAfter;
        Job _job;
        internal static long Jumps;

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode) { Swap(); _job = Scheduler.Interval(Module, "Core.Optimization.OriginJump", 1f, Swap); }
        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable()
        {
            if (_job != null) { _job.Remove(); _job = null; }
            Unswap();
        }

        /// <summary>Replaces only the audited native origin callbacks with the shared rigidbody-snapshot pair.</summary>
        void Swap()
        {
            var fo = NwhOrigin.Instance;
            if (fo == _fo && _fo != null) return;
            if (_fo != null) Unswap();
            if (fo == null) return;
            var before = AccessTools.Method(typeof(NwhOrigin), "BeforeJump");
            var after = AccessTools.Method(typeof(NwhOrigin), "AfterJump");
            if (before == null || after == null) { Log.Warn(OptimizationRuntime.Instance, "EN6 origin: BeforeJump/AfterJump not found - left vanilla"); return; }
            _origBefore = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), fo, before);
            _origAfter = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), fo, after);
            fo.onBeforeJump.RemoveListener(_origBefore); fo.onAfterJump.RemoveListener(_origAfter);
            fo.onBeforeJump.AddListener(_myBefore); fo.onAfterJump.AddListener(_myAfter);
            _fo = fo;
        }

        /// <summary>Restores the native floating-origin callbacks and drops the shared rigidbody snapshot.</summary>
        void Unswap()
        {
            if (_fo != null)
            {
                _fo.onBeforeJump.RemoveListener(_myBefore); _fo.onAfterJump.RemoveListener(_myAfter);
                _fo.onBeforeJump.AddListener(_origBefore); _fo.onAfterJump.AddListener(_origAfter);
            }
            _fo = null; _bodies = null;
        }

        // FloatingOrigin.BeforeJump: every body sleepThreshold = MaxValue
        /// <summary>Origin pre-jump callback: captures one rigidbody array and applies the native temporary sleep threshold.</summary>
        static void MyBefore()
        {
            _bodies = Object.FindObjectsOfType<Rigidbody>();
            for (int i = 0; i < _bodies.Length; i++) _bodies[i].sleepThreshold = float.MaxValue;
            Jumps++;
        }

        // FloatingOrigin.AfterJump: every body sleepThreshold = 0.14, then SyncTransforms
        /// <summary>Origin post-jump callback: restores native sleep thresholds and synchronizes transforms once using the shared snapshot.</summary>
        static void MyAfter()
        {
            var b = _bodies ?? Object.FindObjectsOfType<Rigidbody>();
            for (int i = 0; i < b.Length; i++) if (b[i] != null) b[i].sleepThreshold = 0.14f;
            _bodies = null;
            Physics.SyncTransforms();
        }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l) { l.Value("Jumps handled", Jumps + (_fo != null ? "" : " (no FloatingOrigin)")); }
    }
}
