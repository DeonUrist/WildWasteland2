namespace WildWasteland.Modules.Guns
{
    /// <summary>HUD part of the Guns entry (Wave A split, B10): the crossbow tooltip damage text.</summary>
    public sealed partial class GunsModule
    {
        /// <summary>Applies the UiTextSetText patch that shows the projectile crossbow damage in the tooltip.</summary>
        partial void InitHud() { Core.Patches.ApplyClass(this, typeof(WeaponDamageText)); }
    }
}
