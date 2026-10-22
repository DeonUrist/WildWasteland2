using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// RC1 (research C §1 "Renderers", §2 Tier 1.3, §3). 11,041 of 11,321 prefab MeshRenderers cast shadows; ~51% are smaller than
    /// 1 m. MeshRenderers whose world half extent (mesh bounds x lossy scale, largest axis) is below the preset threshold stop
    /// casting shadows (they still receive them). Applied to prefab assets of unsaved prefabs (POIs, props: spawns inherit) and to
    /// live instances (tile objects, CreateObject spawns, full pass). Excluded: anything under a VehicleController (car frames and
    /// attached parts re-parent), the player rig (Player / PlayerCameraHolder), the Weapon layer (12), ShadowsOnly renderers.
    /// Saved objects (ES3Type_MeshRenderer writes shadowCastingMode) are edited per instance only and reverted while a save runs.
    /// </summary>
    public sealed class SmallCasters : Feature, IScanClient, SaveGuard.IClient
    {
        public override string PlanId { get { return "RC1"; } }
        public override string Key { get { return "Small shadow casters"; } }
        public override string Title { get { return "No shadows from tiny objects"; } }
        public override string Category { get { return Categories.Rendering; } }
        public override int Order { get { return 7; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Objects smaller than a few centimetres to decimetres (cans, bolts, small props) stop casting shadows."; } }

        Knob _size;
        public override void Bind(ModuleConfig cfg) { _size = NewKnob(cfg, "Half extent (m)", "Renderers with a world half extent below this stop casting shadows.", 0.35f, 0.15f, 0.08f, 0f); }

        sealed class RRec { public MeshRenderer R; public ShadowCastingMode Orig; public bool Saved, Asset; public float Size; }
        readonly Dictionary<int, RRec> _recs = new Dictionary<int, RRec>();
        readonly List<RRec> _list = new List<RRec>();
        readonly Dictionary<long, ShadowCastingMode> _assetOrig = new Dictionary<long, ShadowCastingMode>();
        float _threshold;
        public ScanTypes Types { get { return ScanTypes.Renderers; } }

        protected override void OnEnable(FeatureMode mode) { _threshold = _size.Value; SaveGuard.Register(this); RenderScan.Register(this); }

        protected override void OnDisable()
        {
            RenderScan.Unregister(this); SaveGuard.Unregister(this);
            for (int i = 0; i < _list.Count; i++) Restore(_list[i]);
            _list.Clear(); _recs.Clear(); _assetOrig.Clear();
        }

        public override void OnSettingsChanged()
        {
            _threshold = _size.Value;
            for (int i = 0; i < _list.Count; i++)
            {
                var r = _list[i]; if (r.R == null || (r.Saved && SaveGuard.Reverted)) continue;
                if (r.Size < _threshold) Apply(r); else Restore(r);
            }
            RenderScan.RequestFull();   // a larger threshold catches renderers that were not recorded
        }

        public override void OnSceneLoaded() { RenderScan.RequestFull(); }
        public override void OnWorldReady() { RenderScan.RequestFull(); }

        static long MeshKey(MeshRenderer r, Transform root)
        {
            var mf = r.GetComponent<MeshFilter>();
            int mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.GetInstanceID() : 0;
            return ((long)mesh << 32) ^ (uint)RenderScan.RootHash(root);
        }

        public void OnRenderer(MeshRenderer r, ref ScanCtx c)
        {
            if (c.Player || c.Vehicle || (c.Asset && c.Saved)) return;
            int id = r.GetInstanceID();
            if (_recs.ContainsKey(id)) return;
            if (r.gameObject.layer == 12) return;
            var mode = r.shadowCastingMode;
            if (mode == ShadowCastingMode.ShadowsOnly) return;
            ShadowCastingMode orig = mode;
            if (mode == ShadowCastingMode.Off)
            {
                // an instance that inherited our edit from its prefab asset: take the asset's original
                if (c.Asset || _assetOrig.Count == 0 || !_assetOrig.TryGetValue(MeshKey(r, c.Root), out orig)) return;
            }
            float size = RenderScan.HalfExtent(r);
            if (size < 0f || size >= _threshold) return;
            var rec = new RRec { R = r, Orig = orig, Saved = c.Saved, Asset = c.Asset, Size = size };
            _recs[id] = rec; _list.Add(rec);
            if (c.Asset) _assetOrig[MeshKey(r, c.Root)] = orig;
            if (!(rec.Saved && SaveGuard.Reverted)) Apply(rec);
        }

        static void Apply(RRec r) { if (r.R != null && r.R.shadowCastingMode != ShadowCastingMode.Off) r.R.shadowCastingMode = ShadowCastingMode.Off; }
        static void Restore(RRec r) { if (r.R != null && r.R.shadowCastingMode != r.Orig) r.R.shadowCastingMode = r.Orig; }

        public void RevertSaved() { for (int i = 0; i < _list.Count; i++) if (_list[i].Saved) Restore(_list[i]); }
        public void ReapplySaved() { for (int i = 0; i < _list.Count; i++) { var r = _list[i]; if (r.Saved && r.Size < _threshold) Apply(r); } }

        public void OnLight(Light l, ref ScanCtx c) { }
        public void OnParticles(ParticleSystem p, ref ScanCtx c) { }
        public void OnAnimator(Animator a, ref ScanCtx c) { }
        public void OnRoot(Transform root, ref ScanCtx c) { }
        public void OnPassDone()
        {
            int n = 0;
            for (int i = 0; i < _list.Count; i++) { var r = _list[i]; if (r.R != null) _list[n++] = r; }
            if (n == _list.Count) return;
            _list.RemoveRange(n, _list.Count - n);
            _recs.Clear(); for (int i = 0; i < _list.Count; i++) _recs[_list[i].R.GetInstanceID()] = _list[i];
        }

        public override void DrawStats(ToolLayout l) { l.Value("Renderers without shadow", _list.Count + " (" + _assetOrig.Count + " prefab meshes)"); }
    }
}
