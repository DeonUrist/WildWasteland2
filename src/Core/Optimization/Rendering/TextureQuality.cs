using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// RC8 (research C §0 "Textures", §2 Tier 2.12, §3). 38 textures at 4096 and 206 at 2048, no streaming mipmaps; Ultra forces
    /// anisotropic filtering (Sources data/settings.md QualitySettings "Ultra": anisotropicTextures 2, textureQuality 0).
    /// Low preset: half-resolution textures (masterTextureLimit at least 1) and per-texture anisotropic filtering (Enable instead of
    /// ForceEnable). Both through QualityGuard (re-applied after the graphics menu, the level's values restored on disable).
    /// Changing masterTextureLimit re-uploads textures once (short hitch when the preset changes).
    /// </summary>
    public sealed class TextureQuality : Feature
    {
        public override string PlanId { get { return "RC8"; } }
        public override string Key { get { return "Texture quality"; } }
        public override string Title { get { return "Texture resolution and anisotropic filtering (Low)"; } }
        public override string Category { get { return Categories.Rendering; } }
        public override int Order { get { return 11; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Performance preset: half-resolution textures and non-forced anisotropic filtering (saves video memory)."; } }

        Knob _limit, _aniso;
        public override void Bind(ModuleConfig cfg)
        {
            _limit = NewKnob(cfg, "Texture limit", "Minimum masterTextureLimit (0 full, 1 half, 2 quarter resolution).", 1, 0, 0, 0);
            _aniso = NewKnob(cfg, "Anisotropic cap", "0 Disable, 1 Enable (per texture), 2 ForceEnable (= no cap).", 1, 2, 2, 2);
        }

        protected override void OnEnable(FeatureMode mode) { Apply(); }
        public override void OnSettingsChanged() { Apply(); }
        protected override void OnDisable() { QualityGuard.Release(QualityGuard.Q.TextureLimit); QualityGuard.Release(QualityGuard.Q.Aniso); }

        void Apply()
        {
            int lim = Mathf.Clamp(_limit.Int, 0, 3);
            if (lim > 0) QualityGuard.Set(QualityGuard.Q.TextureLimit, lim, true); else QualityGuard.Release(QualityGuard.Q.TextureLimit);
            int an = Mathf.Clamp(_aniso.Int, 0, 2);
            if (an < 2) QualityGuard.Set(QualityGuard.Q.Aniso, an); else QualityGuard.Release(QualityGuard.Q.Aniso);
        }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("Texture limit / aniso", QualityGuard.Describe(QualityGuard.Q.TextureLimit) + " / " + QualityGuard.Describe(QualityGuard.Q.Aniso));
        }
    }
}
