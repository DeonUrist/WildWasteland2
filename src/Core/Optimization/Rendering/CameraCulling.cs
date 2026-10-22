using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
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
        public override string PlanId { get { return "RC3"; } }
        public override string Key { get { return "Layer cull distances"; } }
        public override string Title { get { return "Per-layer view distances"; } }
        public override string Category { get { return Categories.Rendering; } }
        public override int Order { get { return 9; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Car parts stop rendering beyond 300 m, NPCs beyond 600 m, cars beyond 2.5 km. Items, food and weapons retain their original camera distances."; } }

        Knob _scale;
        public override void Bind(ModuleConfig cfg) { _scale = NewKnob(cfg, "Scale", "Multiplier of the layer distances (Hinge 300, Actor 600, Car 2500 m); 0 = off. Items, food and weapons are unchanged.", 0.6f, 1f, 1.5f, 0f); }

        static readonly int[] Layers = { 13, 10, 8 };
        static readonly float[] Dists = { 300f, 600f, 2500f };
        sealed class Rec { public Camera C; public int Id; public float[] Orig; public bool OrigSpherical, Vehicle; }
        readonly Dictionary<int, Rec> _recs = new Dictionary<int, Rec>();
        readonly List<Rec> _list = new List<Rec>();
        Job _job; int _version = -1;
        float[] _want;

        protected override void OnEnable(FeatureMode mode)
        {
            Build(); _version = -1;
            SaveGuard.Register(this);
            _job = Scheduler.Interval(Module, "Performance.LayerCull", 1f, Tick);
            Tick();
        }

        protected override void OnDisable()
        {
            if (_job != null) { _job.Remove(); _job = null; }
            SaveGuard.Unregister(this);
            for (int i = 0; i < _list.Count; i++) Restore(_list[i]);
            _list.Clear(); _recs.Clear();
        }

        public override void OnSettingsChanged() { Build(); for (int i = 0; i < _list.Count; i++) if (!(_list[i].Vehicle && SaveGuard.Reverted)) Apply(_list[i]); }

        void Build()
        {
            _want = new float[32];
            float s = _scale.Value;
            if (s > 0f) for (int i = 0; i < Layers.Length; i++) _want[Layers[i]] = Dists[i] * s;
        }

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

        static void Restore(Rec r)
        {
            if (r.C == null) return;
            r.C.layerCullDistances = r.Orig != null && r.Orig.Length == 32 ? r.Orig : new float[32];
            r.C.layerCullSpherical = r.OrigSpherical;
        }

        public void RevertSaved() { for (int i = 0; i < _list.Count; i++) if (_list[i].Vehicle) Restore(_list[i]); }
        public void ReapplySaved() { for (int i = 0; i < _list.Count; i++) if (_list[i].Vehicle) Apply(_list[i]); }

        public override void DrawStats(ToolLayout l) { l.Value("Cameras", _list.Count.ToString()); }
    }

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
        public override string PlanId { get { return "RC4"; } }
        public override string Key { get { return "Far clip"; } }
        public override string Title { get { return "Shorter far clip plane (Performance)"; } }
        public override string Category { get { return Categories.Rendering; } }
        public override int Order { get { return 10; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Performance preset: the view ends at 6 km instead of 10 km."; } }

        Knob _far;
        public override void Bind(ModuleConfig cfg) { _far = NewKnob(cfg, "Far plane", "Far clip plane cap (m); 0 = unchanged.", 6000, 0, 0, 0); }

        sealed class Rec { public Camera C; public float Orig, Applied; public bool Vehicle, Set; }
        readonly List<Rec> _list = new List<Rec>();
        readonly HashSet<int> _ids = new HashSet<int>();
        Job _job;

        protected override void OnEnable(FeatureMode mode)
        {
            SaveGuard.Register(this);
            _job = Scheduler.Interval(Module, "Performance.FarClip", 1f, Tick);
            Tick();
        }

        protected override void OnDisable()
        {
            if (_job != null) { _job.Remove(); _job = null; }
            SaveGuard.Unregister(this);
            for (int i = 0; i < _list.Count; i++) Restore(_list[i]);
            _list.Clear(); _ids.Clear();
        }

        public override void OnSettingsChanged() { for (int i = 0; i < _list.Count; i++) Restore(_list[i]); Tick(); }

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

        static void Restore(Rec r)
        {
            if (r.C != null && r.Set && Mathf.Approximately(r.C.farClipPlane, r.Applied)) r.C.farClipPlane = r.Orig;
            r.Set = false;
        }

        public void RevertSaved() { for (int i = 0; i < _list.Count; i++) if (_list[i].Vehicle) Restore(_list[i]); }
        public void ReapplySaved() { if (_job != null) _job.RunNow(); }

        public override void DrawStats(ToolLayout l) { l.Value("Cameras", _list.Count + (_far.Value > 0 ? ", far plane " + _far.Value.ToString("0") + " m" : ", unchanged")); }
    }
}
