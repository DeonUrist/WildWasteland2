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
    /// RC7 (plan §8, research C §2 #10): items keep Continuous CCD forever (LockPhysics / CheckTag set it on every pickup or part
    /// removal, bumper_10.md:L33, L98). A 1 s sliced sweep over ArrayList_Items roots on the Item, Weapon and Food layers switches
    /// non-kinematic bodies slower than 2 m/s from Continuous to ContinuousSpeculative and back to Continuous above 8 m/s.
    /// Only bodies this feature switched are touched again; the game setting Continuous itself (pickup) is respected. Disable puts
    /// Continuous back on every switched body that is not kinematic (Unity rejects Continuous on kinematic bodies with a warning).
    /// </summary>
    public sealed class CcdSweepFeature : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "RC7"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "CCD sweep"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Resting items use cheaper CCD"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.World; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 11; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Loose items lying still use speculative continuous collision; fast-moving ones keep full continuous collision."; } }

        internal static bool On;
        Job _job;
        readonly Dictionary<Rigidbody, bool> _switched = new Dictionary<Rigidbody, bool>(RefComparer<Rigidbody>.Instance);
        readonly List<Rigidbody> _tmp = new List<Rigidbody>();
        PlayMakerArrayListProxy _items; GameObject _arraysGo;
        int _cursor; float _passStart;
        internal static long ToSpec, ToCont;

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode) { On = true; _job = Scheduler.Sliced(Module, "Core.Optimization.CcdSweep", 300, Step); }

        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable()
        {
            On = false;
            if (_job != null) { _job.Remove(); _job = null; }
            foreach (var kv in _switched)
            {
                var rb = kv.Key;
                if (rb != null && !rb.isKinematic && rb.collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative) rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
            _switched.Clear();
        }

        /// <summary>Drops scene-bound references and refreshes this feature for the newly loaded scene.</summary>
        public override void OnSceneLoaded() { _switched.Clear(); _items = null; _arraysGo = null; _cursor = 0; }

        /// <summary>Processes a bounded saved-item slice, selecting cheaper CCD only within the established resting/speed rules.</summary>
        bool Step()
        {
            if (!On || Time.timeScale <= 0f) return false;
            if (_cursor == 0)
            {
                if (Time.time - _passStart < 1f) return false;
                _passStart = Time.time;
                if (!Resolve()) return false;
            }
            var list = _items.arrayList;
            for (int k = 0; k < 32; k++)
            {
                if (_cursor >= list.Count) { _cursor = 0; Prune(); return false; }
                var go = list[_cursor++] as GameObject;
                if (go == null || !go.activeInHierarchy) continue;
                int layer = go.layer;
                if (layer != 9 && layer != 12 && layer != 21) continue;
                var rb = go.GetComponent<Rigidbody>();
                if (rb == null || rb.isKinematic) continue;
                var mode = rb.collisionDetectionMode;
                float v2 = rb.velocity.sqrMagnitude;
                if (mode == CollisionDetectionMode.Continuous && v2 < 4f) { rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; _switched[rb] = true; ToSpec++; }
                else if (mode == CollisionDetectionMode.ContinuousSpeculative && v2 > 64f && _switched.ContainsKey(rb)) { rb.collisionDetectionMode = CollisionDetectionMode.Continuous; _switched.Remove(rb); ToCont++; }
                else if (mode != CollisionDetectionMode.ContinuousSpeculative && _switched.ContainsKey(rb)) _switched.Remove(rb);   // the game changed it
            }
            return true;
        }

        /// <summary>Caches the current native saved-item array list before the CCD sweep can run.</summary>
        bool Resolve()
        {
            var go = GameRefs.NewGOArrayList;
            if (go == null) return false;
            if (go != _arraysGo || _items == null)
            {
                _arraysGo = go; _items = null;
                var ps = go.GetComponents<PlayMakerArrayListProxy>();
                for (int i = 0; i < ps.Length; i++) if (ps[i] != null && ps[i].referenceName == "ArrayList_Items") { _items = ps[i]; break; }
            }
            return _items != null;
        }

        /// <summary>Drops destroyed rigidbodies from the CCD restoration lookup.</summary>
        void Prune()
        {
            _tmp.Clear();
            foreach (var kv in _switched) if (kv.Key == null) _tmp.Add(kv.Key);
            for (int i = 0; i < _tmp.Count; i++) _switched.Remove(_tmp[i]);
            _tmp.Clear();
        }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l) { l.Value("Bodies on speculative CCD now / switches", _switched.Count + " / " + ToSpec + " -> " + ToCont); }
    }
}
