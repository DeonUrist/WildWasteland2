using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>Gunplay part of the HUD entry (Wave A split, B09): subscribes the vanilla hit observer that feeds hit feedback while projectiles are off.</summary>
    public sealed partial class HudModule
    {
        /// <summary>Subscribes VanillaHits.BeforeSetFsmFloat to Core.Hooks.SetFsmFloatEnter (prefix, observe only).</summary>
        partial void InitGunplay() { Hooks.SetFsmFloatEnter.Prefix(this, VanillaHits.BeforeSetFsmFloat); }
    }
}
