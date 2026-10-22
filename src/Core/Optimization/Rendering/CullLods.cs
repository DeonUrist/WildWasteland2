using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// RC2 (research C §1 "Renderers", §2 Tier 1.4, §3). Only 60 prefab + 24 scene LODGroups exist for 11k renderers, so most props
    /// are drawn (and shadow-culled) out to the 10 km far plane across 3x3 tiles of 5 km. Every prefab root without a LODGroup gets a
    /// single-level cull-only LODGroup (screen height 0.012 below 1 m, 0.006 below 5 m, else 0.003; no fade) over its own
    /// MeshRenderers, and QualitySettings.lodBias is capped per preset (also scales the game's own LODGroups).
    /// Skipped roots: anything ES3 saves (ES3Prefab on the root or above, an "ID" FSM, a VehicleController: items, cars, NPCs),
    /// the player rig, the Weapon layer, roots with a LODGroup / Animator / SkinnedMeshRenderer / VehicleController below them,
    /// roots carrying WW components (POI markers), DontDestroyOnLoad objects. Renderers under a nested ES3Prefab / "ID" FSM /
    /// non-kinematic Rigidbody are left out of the group (they can be picked up or roll away). MapMagic POIs / static props are
    /// the main target: the asset edit is inherited by the objects pool; live instances get their own group (recorded).
    /// Disable removes every injected group: recorded ones, asset ones, and inherited ones recognised by their exact screen heights.
    /// </summary>
    public sealed class CullLods : Feature, IScanClient
    {
        public override string PlanId { get { return "RC2"; } }
        public override string Key { get { return "Size culling"; } }
        public override string Title { get { return "Cull-only LOD groups and LOD bias"; } }
        public override string Category { get { return Categories.Rendering; } }
        public override int Order { get { return 8; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Props and buildings without LODs stop rendering when they are only a few pixels tall; LOD bias follows the preset."; } }

        Knob _bias, _scale;
        public override void Bind(ModuleConfig cfg)
        {
            _bias = NewKnob(cfg, "LOD bias", "QualitySettings.lodBias cap (vanilla Ultra 2).", 0.8f, 1.2f, 2f, 2f);
            _scale = NewKnob(cfg, "Injected scale", "Multiplier of the injected screen heights (higher = culled earlier); applied to newly injected groups.", 1f, 1f, 1f, 1f);
        }

        // exact marker heights: vanilla groups use 0.004-0.018 but never these values
        const float H1 = 0.0120123f, H2 = 0.0060123f, H3 = 0.0030123f;
        static bool IsInjectedHeight(float h, float scale) { return h == H1 * scale || h == H2 * scale || h == H3 * scale; }

        readonly List<LODGroup> _assetGroups = new List<LODGroup>();
        readonly List<LODGroup> _instGroups = new List<LODGroup>();
        readonly HashSet<int> _done = new HashSet<int>();
        readonly List<MeshRenderer> _rbuf = new List<MeshRenderer>();
        readonly List<Renderer> _keep = new List<Renderer>();
        readonly List<Component> _cbuf = new List<Component>();
        readonly List<PlayMakerFSM> _fbuf = new List<PlayMakerFSM>();
        float _usedScale = 1f;
        public static int Failed;
        public ScanTypes Types { get { return ScanTypes.Roots; } }

        protected override void OnEnable(FeatureMode mode)
        {
            _usedScale = Mathf.Clamp(_scale.Value, 0.1f, 10f);
            QualityGuard.Set(QualityGuard.Q.LodBias, _bias.Value);
            RenderScan.Register(this);
        }

        protected override void OnDisable()
        {
            RenderScan.Unregister(this);
            QualityGuard.Release(QualityGuard.Q.LodBias);
            for (int i = 0; i < _instGroups.Count; i++) if (_instGroups[i] != null) UnityEngine.Object.Destroy(_instGroups[i]);
            // instances that inherited a group from an edited asset
            var all = Resources.FindObjectsOfTypeAll<LODGroup>();
            for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].gameObject.scene.IsValid() && Injected(all[i])) UnityEngine.Object.Destroy(all[i]);
            for (int i = 0; i < _assetGroups.Count; i++) if (_assetGroups[i] != null) { try { UnityEngine.Object.DestroyImmediate(_assetGroups[i], true); } catch (Exception e) { Log.Warn("[Performance] RC2 asset restore: " + e.Message); } }
            _assetGroups.Clear(); _instGroups.Clear(); _done.Clear();
        }

        bool Injected(LODGroup g)
        {
            if (g.lodCount != 1 || g.fadeMode != LODFadeMode.None) return false;
            var lods = g.GetLODs();
            return lods.Length == 1 && IsInjectedHeight(lods[0].screenRelativeTransitionHeight, _usedScale);
        }

        internal static bool Owns(LODGroup group)
        {
            var module = PerformanceModule.Instance;
            if (module != null) foreach (var feature in module.Features)
            { var cull = feature as CullLods; if (cull != null && cull.Injected(group)) return true; }
            return false;
        }

        public override void OnSettingsChanged() { QualityGuard.Set(QualityGuard.Q.LodBias, _bias.Value); }
        public override void OnSceneLoaded() { _done.Clear(); RenderScan.RequestFull(); }
        public override void OnWorldReady() { RenderScan.RequestFull(); }

        public void OnRoot(Transform root, ref ScanCtx c)
        {
            if (c.Player || c.Saved || c.Vehicle) return;
            var go = root.gameObject;
            // RC9 supplies persistent flat wall panels; a cull-only parent would hide those as well.
            if (go.name.StartsWith("Towns perimeter ", StringComparison.Ordinal))
                foreach (var feature in PerformanceModule.Instance.Features) if (feature is GeneratedLods && feature.IsOn) return;
            if (!_done.Add(root.GetInstanceID())) return;
            if (!c.Asset && go.scene.buildIndex < 0) return;          // DontDestroyOnLoad / mod objects
            if (go.layer == 12) return;
            if (go.GetComponentInChildren<LODGroup>(true) != null) return;
            if (go.GetComponentInChildren<Animator>(true) != null || go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) return;
            if (go.GetComponentInChildren<NWH.VehiclePhysics2.VehicleController>(true) != null) return;
            if (HasIdFsm(root)) return;
            go.GetComponents(_cbuf);
            for (int i = 0; i < _cbuf.Count; i++) if (_cbuf[i] != null && _cbuf[i].GetType().Assembly == typeof(CullLods).Assembly) { _cbuf.Clear(); return; }
            _cbuf.Clear();

            go.GetComponentsInChildren(true, _rbuf);
            _keep.Clear();
            var toRoot = root.worldToLocalMatrix;
            Bounds b = new Bounds(); bool any = false;
            for (int i = 0; i < _rbuf.Count; i++)
            {
                var r = _rbuf[i];
                if (Detachable(r.transform, root)) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                _keep.Add(r);
                var mb = mf.sharedMesh.bounds;
                var m = toRoot * r.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            _rbuf.Clear();
            if (!any || _keep.Count == 0) { _keep.Clear(); return; }
            float localSize = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            var ls = root.lossyScale;
            float worldSize = localSize * Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
            if (localSize <= 0.001f || worldSize > 2000f) { _keep.Clear(); return; }
            float h = (worldSize < 1f ? H1 : worldSize < 5f ? H2 : H3) * _usedScale;
            LODGroup g;
            try { g = go.AddComponent<LODGroup>(); }
            catch (Exception e) { Failed++; Log.Warn("[Performance] RC2 AddComponent: " + e.Message); _keep.Clear(); return; }
            if (g == null) { Failed++; _keep.Clear(); return; }
            g.fadeMode = LODFadeMode.None;
            g.animateCrossFading = false;
            g.SetLODs(new[] { new LOD(h, _keep.ToArray()) });
            if (c.Asset) { g.localReferencePoint = b.center; g.size = localSize; _assetGroups.Add(g); }
            else { g.RecalculateBounds(); _instGroups.Add(g); }
            _keep.Clear();
        }

        bool HasIdFsm(Transform t)
        {
            t.GetComponents(_fbuf);
            bool id = false;
            for (int i = 0; i < _fbuf.Count; i++) if (_fbuf[i] != null && _fbuf[i].FsmName == "ID") { id = true; break; }
            _fbuf.Clear();
            return id;
        }

        /// <summary>Renderer that can leave the group's place: under a nested saved item, an "ID" FSM or a non-kinematic Rigidbody.</summary>
        bool Detachable(Transform t, Transform root)
        {
            for (var x = t; x != null && x != root; x = x.parent)
            {
                if (x.GetComponent<ES3Internal.ES3Prefab>() != null) return true;
                var rb = x.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic) return true;
                if (HasIdFsm(x)) return true;
            }
            var rr = root.GetComponent<Rigidbody>();
            return rr != null && !rr.isKinematic;
        }

        public void OnLight(Light l, ref ScanCtx c) { }
        public void OnRenderer(MeshRenderer r, ref ScanCtx c) { }
        public void OnParticles(ParticleSystem p, ref ScanCtx c) { }
        public void OnAnimator(Animator a, ref ScanCtx c) { }
        public void OnPassDone()
        {
            for (int i = _instGroups.Count - 1; i >= 0; i--) if (_instGroups[i] == null) _instGroups.RemoveAt(i);
            if (_done.Count > 50000) _done.Clear();
        }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("Injected LOD groups", _assetGroups.Count + " prefabs, " + _instGroups.Count + " live objects" + (Failed > 0 ? ", " + Failed + " failed" : ""));
            l.Value("LOD bias", QualityGuard.Describe(QualityGuard.Q.LodBias));
        }
    }
}
