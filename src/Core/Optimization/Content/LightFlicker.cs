using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// CT6 (plan §7, research B §2 #7): ~60 LightFlicker FSMs (and the identical "FSM" on the Light_Flicker prefab) compute
    /// PerlinNoise(seed, speed*Time.time) x2, clamp 0.7..1.5 and SetLightIntensity every frame for every lamp of every loaded POI
    /// (Sources fsm/prefabs/Camp_2.md:L260-L278, Light_Flicker.md:L8-L16). The manager pauses them (Fsm.ManualUpdate, the PlayMaker
    /// mechanism for externally stepped FSMs) and steps each one itself with Fsm.Update() at the preset rate (20 Hz Balanced) while it
    /// is within the preset distance of the camera (200 m) and its Light is enabled; farther lamps keep their last intensity. Each
    /// step runs the FSM's own four actions, so the curve is vanilla's sampled at a lower rate. Only FSMs whose single state has
    /// exactly that action shape are taken (verified per instance; "FSM" is a generic name). The Rendering features only toggle Light
    /// components; this feature owns the FSMs.
    /// </summary>
    public sealed class LightFlickerFeature : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "CT6"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Light flicker"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Light flicker manager"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.Content; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 6; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Flickering camp lights update at a fixed rate and only near the camera instead of every frame everywhere."; } }

        Knob _hz, _dist;
        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
        public override void Bind(ModuleConfig cfg)
        {
            _hz = NewKnob(cfg, "Rate (Hz)", "Flicker update rate.", 15f, 20f, 30f, 0f);
            _dist = NewKnob(cfg, "Distance (m)", "Lamps farther from the camera keep their last intensity.", 120f, 200f, 300f, 0f);
        }

        /// <summary>Retains one native light FSM's original state and transform for distance-based stepping.</summary>
        sealed class Lamp { public PlayMakerFSM Comp; public Fsm Fsm; public FsmState State; public Light Light; public Transform T; public float Next; }
        readonly List<Lamp> _lamps = new List<Lamp>(64);
        static bool On;
        Job _job;
        float _interval, _dist2;
        internal static long Steps;

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            On = true;
            Apply();
            FsmEdits.Watch(this, "LightFlicker", null, Ready);
            FsmEdits.Watch(this, "FSM", null, Ready);      // generic name: Ready keeps only the exact flicker shape
            _job = Scheduler.EveryFrame(Module, "Core.Optimization.LightFlicker", Tick);
        }

        /// <summary>Resolves the current preset's light flicker frequency and distance limits.</summary>
        void Apply()
        {
            float hz = Mathf.Max(1f, _hz.Value);
            _interval = 1f / hz;
            float d = Mathf.Max(1f, _dist.Value); _dist2 = d * d;
        }

        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged() { Apply(); }

        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable()
        {
            On = false;
            FsmEdits.Unwatch(this);
            if (_job != null) { _job.Remove(); _job = null; }
            for (int i = 0; i < _lamps.Count; i++) if (_lamps[i].Comp != null) FsmPause.Clear(_lamps[i].Fsm, FsmPause.Stepped);
            _lamps.Clear();
        }

        /// <summary>Registers only validated native flicker FSMs, retaining their original update behavior for restoration.</summary>
        void Ready(PlayMakerFSM f)
        {
            var fsm = f.Fsm;
            var st = fsm.States;
            if (st == null || st.Length != 1 || st[0].Name != "flicker") return;     // "FSM" is generic: shape check below decides
            var a = st[0].Actions;
            if (a == null || a.Length != 4 || !(a[0] is PerlinNoise) || !(a[1] is FloatMultiply) || !(a[2] is FloatClamp) || !(a[3] is SetLightIntensity))
            { if (fsm.Name == "LightFlicker") FsmEdits.Warn(this, "LightFlicker/flicker", "unexpected actions"); return; }
            for (int i = 0; i < 4; i++) if (!a[i].Enabled) return;
            var lamp = new Lamp { Comp = f, Fsm = fsm, State = st[0], Light = f.GetComponent<Light>(), T = f.transform, Next = Time.time + Random.value * _interval };
            _lamps.Add(lamp);
        }

        /// <summary>Updates eligible nearby lights at the configured cadence and restores lights excluded by the feature.</summary>
        void Tick()
        {
            if (!On || _lamps.Count == 0) return;
            var cam = GameRefs.MainCamera;
            Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
            float now = Time.time;
            for (int i = _lamps.Count - 1; i >= 0; i--)
            {
                var l = _lamps[i];
                if (l.Comp == null) { FsmPause.Clear(l.Fsm, FsmPause.Stepped); _lamps.RemoveAt(i); continue; }
                var fsm = l.Fsm;
                bool ours = FsmPause.Held(fsm, FsmPause.Stepped);
                if (!ours)
                {
                    // take it once it is running its flicker state (restart on enable re-enters the same single state)
                    if (fsm.Started && !fsm.Finished && fsm.ActiveState == l.State && !fsm.IsSwitchingState && fsm.DelayedEvents.Count == 0)
                        FsmPause.Set(fsm, FsmPause.Stepped);
                    continue;
                }
                if (fsm.Finished || fsm.ActiveState != l.State) { FsmPause.Clear(fsm, FsmPause.Stepped); continue; }
                if (now < l.Next) continue;
                l.Next += _interval;
                if (l.Next < now) l.Next = now + _interval * Random.value;
                if (!l.Comp.isActiveAndEnabled) continue;
                if (cam != null && (l.T.position - cp).sqrMagnitude > _dist2) continue;
                if (l.Light != null && !l.Light.enabled) continue;
                fsm.Update(); Steps++;
            }
        }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l)
        {
            int held = 0; for (int i = 0; i < _lamps.Count; i++) if (_lamps[i].Comp != null && FsmPause.Held(_lamps[i].Fsm, FsmPause.Stepped)) held++;
            l.Value("Flicker FSMs managed", held + " / " + _lamps.Count);
            l.Value("Steps", Steps.ToString());
        }
    }
}
