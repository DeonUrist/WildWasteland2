using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
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
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "RC8"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Texture quality"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Texture resolution and anisotropic filtering (Low)"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.Rendering; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 11; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Performance preset: half-resolution textures and non-forced anisotropic filtering (saves video memory)."; } }

        Knob _limit, _aniso;
        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
        public override void Bind(ModuleConfig cfg)
        {
            _limit = NewKnob(cfg, "Texture limit", "Minimum masterTextureLimit (0 full, 1 half, 2 quarter resolution).", 1, 0, 0, 0);
            _aniso = NewKnob(cfg, "Anisotropic cap", "0 Disable, 1 Enable (per texture), 2 ForceEnable (= no cap).", 1, 2, 2, 2);
        }

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode) { Apply(); }
        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged() { Apply(); }
        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable() { QualityGuard.Release(QualityGuard.Q.TextureLimit); QualityGuard.Release(QualityGuard.Q.Aniso); }

        /// <summary>Applies the preset's texture-size/aniso limits through the shared native-quality guard.</summary>
        void Apply()
        {
            int lim = Mathf.Clamp(_limit.Int, 0, 3);
            if (lim > 0) QualityGuard.Set(QualityGuard.Q.TextureLimit, lim, true); else QualityGuard.Release(QualityGuard.Q.TextureLimit);
            int an = Mathf.Clamp(_aniso.Int, 0, 2);
            if (an < 2) QualityGuard.Set(QualityGuard.Q.Aniso, an); else QualityGuard.Release(QualityGuard.Q.Aniso);
        }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l)
        {
            l.Value("Texture limit / aniso", QualityGuard.Describe(QualityGuard.Q.TextureLimit) + " / " + QualityGuard.Describe(QualityGuard.Q.Aniso));
        }
    }
}
