using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
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
    public sealed class SunShadows : Feature
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
        Job _job;

        protected override void OnEnable(FeatureMode mode)
        {
            ApplyQuality();
            _job = Scheduler.Interval(Module, "Performance.SunShadows", 5f, Tick);
            Tick();
        }

        protected override void OnDisable()
        {
            if (_job != null) { _job.Remove(); _job = null; }
            QualityGuard.Release(QualityGuard.Q.ShadowDistance);
            QualityGuard.Release(QualityGuard.Q.ShadowCascades);
            QualityGuard.Release(QualityGuard.Q.ShadowResolution);
            QualityGuard.Release(QualityGuard.Q.SkinWeights);
            RestoreSun();
        }

        public override void OnSettingsChanged() { ApplyQuality(); RestoreSun(); Tick(); }
        public override void OnSceneLoaded() { _sun = null; _sunSet = false; _sunOrig = null; }
        public override void OnWorldReady() { Tick(); }

        void ApplyQuality()
        {
            QualityGuard.Set(QualityGuard.Q.ShadowDistance, _dist.Value);
            int c = _casc.Int; c = c >= 4 ? 4 : c >= 2 ? 2 : 1;
            QualityGuard.Set(QualityGuard.Q.ShadowCascades, c);
            QualityGuard.Set(QualityGuard.Q.ShadowResolution, Mathf.Clamp(_res.Int, 0, 3));
            int s = _skin.Int; s = s >= 4 ? 4 : s >= 2 ? 2 : 1;
            QualityGuard.Set(QualityGuard.Q.SkinWeights, s);
        }

        void Tick()
        {
            if (_sunSet && _sun != null) return;
            _sunSet = false;
            var sun = RenderSettings.sun;
            if (sun == null)
            {
                var all = UnityEngine.Object.FindObjectsOfType<Light>();
                for (int i = 0; i < all.Length; i++) if (all[i].type == LightType.Directional) { sun = all[i]; break; }
            }
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
