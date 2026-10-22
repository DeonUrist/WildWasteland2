using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// RC4 (research C §0, §2 Tier 1.4, §3): Low preset only - far clip plane 6000 m instead of 10000. The PlayerCamera's
    /// SetClippingPlanes FSM sets nearClipPlane 0.03 / farClipPlane 10000 three seconds after start (Sources
    /// fsm/scene1_game/PlayerCameraHolder.md:L660-668); vehicle cameras are 10000 in their prefabs. A 1 s check re-applies whenever
    /// a camera's far plane differs from what we wrote (the FSM, or anything else, set it; that value becomes the one restored).
    /// Vehicle cameras' farClipPlane is written by ES3 (ES3Type_Camera): reverted while a save runs. Fog: Azure's
    /// globalFogDistance is driven every FixedUpdate by the weather controller's override property (AzureOverridePropertiesManually /
    /// AzureWeatherController), so it is not changed here.
    /// </summary>
    public sealed class FarClip : Feature, SaveGuard.IClient
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "RC4"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Far clip"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Shorter far clip plane (Performance)"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.Rendering; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 10; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Performance preset: the view ends at 6 km instead of 10 km."; } }

        Knob _far;
        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
        public override void Bind(ModuleConfig cfg) { _far = NewKnob(cfg, "Far plane", "Far clip plane cap (m); 0 = unchanged.", 6000, 0, 0, 0); }

        /// <summary>Captures one camera's original values and whether ES3 can save it on a vehicle.</summary>
        sealed class Rec { public Camera C; public float Orig, Applied; public bool Vehicle, Set; }
        readonly List<Rec> _list = new List<Rec>();
        readonly HashSet<int> _ids = new HashSet<int>();
        Job _job;

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            SaveGuard.Register(this);
            _job = Scheduler.Interval(Module, "Core.Optimization.FarClip", 1f, Tick);
            Tick();
        }

        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable()
        {
            if (_job != null) { _job.Remove(); _job = null; }
            SaveGuard.Unregister(this);
            for (int i = 0; i < _list.Count; i++) Restore(_list[i]);
            _list.Clear(); _ids.Clear();
        }

        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged() { for (int i = 0; i < _list.Count; i++) Restore(_list[i]); Tick(); }

        /// <summary>Updates recognized game cameras and applies the active feature's preset values outside serialization guards.</summary>
        void Tick()
        {
            if (SaveGuard.Reverted) return;
            GameCameras.Refresh();
            var cams = GameCameras.List;
            for (int i = 0; i < cams.Count; i++)
            {
                var c = cams[i];
                if (c != null && _ids.Add(c.GetInstanceID())) _list.Add(new Rec { C = c, Vehicle = GameCameras.IsVehicleCamera(c) });
            }
            float far = _far.Value;
            for (int i = _list.Count - 1; i >= 0; i--)
            {
                var r = _list[i];
                if (r.C == null) { _list.RemoveAt(i); continue; }
                float cur = r.C.farClipPlane;
                if (r.Set && Mathf.Approximately(cur, r.Applied)) continue;
                r.Orig = cur;                   // first sight, or someone (SetClippingPlanes FSM) wrote a new value
                if (far <= 0f || cur <= far) { r.Set = false; continue; }
                r.C.farClipPlane = far; r.Applied = r.C.farClipPlane; r.Set = true;
            }
        }

        /// <summary>Restores only the fields this feature changed on a still-live camera.</summary>
        static void Restore(Rec r)
        {
            if (r.C != null && r.Set && Mathf.Approximately(r.C.farClipPlane, r.Applied)) r.C.farClipPlane = r.Orig;
            r.Set = false;
        }

        /// <summary>Restores the original fields of saved objects before serialization.</summary>
        public void RevertSaved() { for (int i = 0; i < _list.Count; i++) if (_list[i].Vehicle) Restore(_list[i]); }
        /// <summary>Reapplies active optimization settings after serialization releases saved objects.</summary>
        public void ReapplySaved() { if (_job != null) _job.RunNow(); }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l) { l.Value("Cameras", _list.Count + (_far.Value > 0 ? ", far plane " + _far.Value.ToString("0") + " m" : ", unchanged")); }
    }
}
