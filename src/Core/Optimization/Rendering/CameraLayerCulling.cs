using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// RC3 (research C §2 Tier 1.4, §3). Spherical per-layer cull distances on the game's view cameras (PlayerCamera and the vehicles'
    /// 3rdCamera/Camera): Hinge 13 300 m, Actor 10 600 m, Car 8 2500 m, x preset scale. Item, Food and Weapon retain their original
    /// camera distances to protect vehicle cargo. Other layers (Default = buildings/props, left to RC2) unchanged.
    /// ES3 writes Camera.layerCullSpherical of vehicle cameras (ES3Type_Camera; layerCullDistances is not written): reverted while
    /// a save runs. Cameras of other mods / WW (CleanHUD map, occlusion) are not touched.
    /// </summary>
    public sealed class CameraLayerCulling : Feature, SaveGuard.IClient
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "RC3"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Layer cull distances"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Per-layer view distances"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.Rendering; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 9; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Car parts stop rendering beyond 300 m, NPCs beyond 600 m, cars beyond 2.5 km. Items, food and weapons retain their original camera distances."; } }

        Knob _scale;
        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
        public override void Bind(ModuleConfig cfg) { _scale = NewKnob(cfg, "Scale", "Multiplier of the layer distances (Hinge 300, Actor 600, Car 2500 m); 0 = off. Items, food and weapons are unchanged.", 0.6f, 1f, 1.5f, 0f); }

        static readonly int[] Layers = { 13, 10, 8 };
        static readonly float[] Dists = { 300f, 600f, 2500f };
        /// <summary>Captures one camera's original values and whether ES3 can save it on a vehicle.</summary>
        sealed class Rec { public Camera C; public int Id; public float[] Orig; public bool OrigSpherical, Vehicle; }
        readonly Dictionary<int, Rec> _recs = new Dictionary<int, Rec>();
        readonly List<Rec> _list = new List<Rec>();
        Job _job; int _version = -1;
        float[] _want;

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            Build(); _version = -1;
            SaveGuard.Register(this);
            _job = Scheduler.Interval(Module, "Core.Optimization.LayerCull", 1f, Tick);
            Tick();
        }

        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable()
        {
            if (_job != null) { _job.Remove(); _job = null; }
            SaveGuard.Unregister(this);
            for (int i = 0; i < _list.Count; i++) Restore(_list[i]);
            _list.Clear(); _recs.Clear();
        }

        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged() { Build(); for (int i = 0; i < _list.Count; i++) if (!(_list[i].Vehicle && SaveGuard.Reverted)) Apply(_list[i]); }

        /// <summary>Builds the preset's native-layer cull-distance array from the current override values.</summary>
        void Build()
        {
            _want = new float[32];
            float s = _scale.Value;
            if (s > 0f) for (int i = 0; i < Layers.Length; i++) _want[Layers[i]] = Dists[i] * s;
        }

        /// <summary>Updates recognized game cameras and applies the active feature's preset values outside serialization guards.</summary>
        void Tick()
        {
            GameCameras.Refresh();
            if (GameCameras.Version == _version) return;
            _version = GameCameras.Version;
            for (int i = _list.Count - 1; i >= 0; i--) if (_list[i].C == null) { _recs.Remove(_list[i].Id); _list.RemoveAt(i); }
            var cams = GameCameras.List;
            for (int i = 0; i < cams.Count; i++)
            {
                var c = cams[i];
                if (c == null || _recs.ContainsKey(c.GetInstanceID())) continue;
                var r = new Rec { C = c, Id = c.GetInstanceID(), Orig = c.layerCullDistances, OrigSpherical = c.layerCullSpherical, Vehicle = GameCameras.IsVehicleCamera(c) };
                _recs[c.GetInstanceID()] = r; _list.Add(r);
                if (!(r.Vehicle && SaveGuard.Reverted)) Apply(r);
            }
        }

        /// <summary>Applies the feature's cull settings to a recognized camera while retaining its original values.</summary>
        void Apply(Rec r)
        {
            if (r.C == null) return;
            var d = new float[32];
            for (int i = 0; i < 32; i++)
            {
                float o = r.Orig != null && i < r.Orig.Length ? r.Orig[i] : 0f, w = _want[i];
                d[i] = w <= 0f ? o : o <= 0f ? w : Mathf.Min(o, w);
            }
            r.C.layerCullDistances = d;
            r.C.layerCullSpherical = true;
        }

        /// <summary>Restores only the fields this feature changed on a still-live camera.</summary>
        static void Restore(Rec r)
        {
            if (r.C == null) return;
            r.C.layerCullDistances = r.Orig != null && r.Orig.Length == 32 ? r.Orig : new float[32];
            r.C.layerCullSpherical = r.OrigSpherical;
        }

        /// <summary>Restores the original fields of saved objects before serialization.</summary>
        public void RevertSaved() { for (int i = 0; i < _list.Count; i++) if (_list[i].Vehicle) Restore(_list[i]); }
        /// <summary>Reapplies active optimization settings after serialization releases saved objects.</summary>
        public void ReapplySaved() { for (int i = 0; i < _list.Count; i++) if (_list[i].Vehicle) Apply(_list[i]); }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l) { l.Value("Cameras", _list.Count.ToString()); }
    }
}
