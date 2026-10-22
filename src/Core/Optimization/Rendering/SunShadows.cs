using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// EN3 (research C §2 Tier 1.3, §3). Ultra = sun shadows to 200 m, 4 cascades, VeryHigh, skinWeights 4 (Sources data/settings.md
    /// QualitySettings "Ultra"). Caps them through QualityGuard (never above the player's chosen quality level; re-applied after
    /// the graphics menu's SetQualityLevel; the level's value comes back on release) and gives the sun per-layer shadow cull
    /// distances for small dynamic things: Item 9 30 m, Food 21 20 m, Weapon 12 20 m, Hinge 13 40 m, Actor 10 60 m (layers:
    /// data/settings.md "Layers"). The sun is the Azure "Directional Light" (data/hierarchy/scene1_game/Azure_Sky_ Dynamic Skybox.txt:L7-8),
    /// a scene object that is never saved.
    /// WW PlayerBody OcclusionCutaway lowers shadowDistance for its clear-camera pass and puts the read value back in the same frame,
    /// so it keeps working with the capped value.
    /// </summary>
    public sealed class SunShadows : Feature, IScanClient
    {
        public override string PlanId { get { return "EN3"; } }
        public override string Key { get { return "Sun shadows"; } }
        public override string Title { get { return "Sun shadow distance, cascades, resolution, layer culling"; } }
        public override string Category { get { return Categories.Rendering; } }
        public override int Order { get { return 4; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Shorter, cheaper sun shadows; small items, food, weapons and NPCs stop casting sun shadows a few dozen metres away."; } }

        Knob _dist, _casc, _res, _skin, _layerScale;
        public override void Bind(ModuleConfig cfg)
        {
            _dist = NewKnob(cfg, "Distance", "Sun shadow distance cap (m).", 80, 120, 160, 200);
            _casc = NewKnob(cfg, "Cascades", "Shadow cascades cap (1, 2 or 4).", 1, 2, 4, 4);
            _res = NewKnob(cfg, "Resolution", "Shadow resolution cap: 0 Low, 1 Medium, 2 High, 3 VeryHigh.", 1, 2, 3, 3);
            _skin = NewKnob(cfg, "Skin weights", "Bones per vertex cap (1, 2 or 4).", 2, 2, 4, 4);
            _layerScale = NewKnob(cfg, "Layer cull scale", "Scale of the per-layer sun shadow distances (Item 30, Food 20, Weapon 20, Hinge 40, Actor 60 m); 0 = off.", 0.6f, 1f, 1.5f, 0f);
        }

        static readonly int[] Layers = { 9, 21, 12, 13, 10 };
        static readonly float[] Dists = { 30f, 20f, 20f, 40f, 60f };
        Light _sun; float[] _sunOrig; bool _sunSet;
        bool _active;

        /// <summary>Receives directional lights from the shared startup/incremental scan.</summary>
        public ScanTypes Types { get { return ScanTypes.Lights; } }

        protected override void OnEnable(FeatureMode mode)
        {
            ApplyQuality();
            _active = true;
            RenderScan.Register(this);
            TryCurrentSun();
        }

        protected override void OnDisable()
        {
            _active = false;
            RenderScan.Unregister(this);
            QualityGuard.Release(QualityGuard.Q.ShadowDistance);
            QualityGuard.Release(QualityGuard.Q.ShadowCascades);
            QualityGuard.Release(QualityGuard.Q.ShadowResolution);
            QualityGuard.Release(QualityGuard.Q.SkinWeights);
            RestoreSun();
        }

        public override void OnSettingsChanged() { ApplyQuality(); RestoreSun(); TryCurrentSun(); RenderScan.RequestFull(); }
        public override void OnSceneLoaded() { RestoreSun(); RenderScan.RequestFull(); }
        public override void OnWorldReady() { TryCurrentSun(); RenderScan.RequestFull(); }

        void ApplyQuality()
        {
            QualityGuard.Set(QualityGuard.Q.ShadowDistance, _dist.Value);
            int c = _casc.Int; c = c >= 4 ? 4 : c >= 2 ? 2 : 1;
            QualityGuard.Set(QualityGuard.Q.ShadowCascades, c);
            QualityGuard.Set(QualityGuard.Q.ShadowResolution, Mathf.Clamp(_res.Int, 0, 3));
            int s = _skin.Int; s = s >= 4 ? 4 : s >= 2 ? 2 : 1;
            QualityGuard.Set(QualityGuard.Q.SkinWeights, s);
        }

        /// <summary>Uses the renderer's selected sun without searching the scene.</summary>
        void TryCurrentSun()
        {
            if (!_active) return;
            var sun = RenderSettings.sun;
            if (sun == null) return;
            if (_sunSet && _sun != sun) RestoreSun();
            ApplySun(sun);
        }

        /// <summary>Applies layer culling once to the active scene sun while preserving its exact original distances.</summary>
        void ApplySun(Light sun)
        {
            if (_sunSet && _sun != null) return;
            _sunSet = false;
            if (sun == null) return;
            float scale = _layerScale.Value;
            if (scale <= 0f) return;
            _sun = sun;
            _sunOrig = sun.layerShadowCullDistances;
            var d = new float[32];
            if (_sunOrig != null && _sunOrig.Length == 32) Array.Copy(_sunOrig, d, 32);
            for (int i = 0; i < Layers.Length; i++)
            {
                float v = Dists[i] * scale;
                if (d[Layers[i]] <= 0f || d[Layers[i]] > v) d[Layers[i]] = v;
            }
            sun.layerShadowCullDistances = d;
            _sunSet = true;
        }

        /// <summary>Shared scan fallback for a scene directional light when RenderSettings has no sun reference yet.</summary>
        public void OnLight(Light light, ref ScanCtx context)
        {
            if (!_active || context.Asset || light == null || light.type != LightType.Directional) return;
            if (_sunSet && _sun != null) return;
            var selected = RenderSettings.sun;
            if (selected != null && selected != light) return;
            ApplySun(light);
        }
        /// <summary>This client does not modify renderers.</summary>
        public void OnRenderer(MeshRenderer renderer, ref ScanCtx context) { }
        /// <summary>This client does not modify particle systems.</summary>
        public void OnParticles(ParticleSystem particles, ref ScanCtx context) { }
        /// <summary>This client does not modify animators.</summary>
        public void OnAnimator(Animator animator, ref ScanCtx context) { }
        /// <summary>This client does not need root geometry.</summary>
        public void OnRoot(Transform root, ref ScanCtx context) { }
        /// <summary>Rechecks the selected sun after a shared scan finishes.</summary>
        public void OnPassDone() { TryCurrentSun(); }

        void RestoreSun()
        {
            if (_sunSet && _sun != null) _sun.layerShadowCullDistances = _sunOrig != null && _sunOrig.Length == 32 ? _sunOrig : new float[32];
            _sunSet = false; _sun = null; _sunOrig = null;
        }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("Shadow distance", QualityGuard.Describe(QualityGuard.Q.ShadowDistance));
            l.Value("Cascades / resolution / skin", QualityGuard.Describe(QualityGuard.Q.ShadowCascades) + " / " + QualityGuard.Describe(QualityGuard.Q.ShadowResolution) + " / " + QualityGuard.Describe(QualityGuard.Q.SkinWeights));
            l.Value("Sun layer culling", _sunSet ? "on" : "off");
        }
    }
}
