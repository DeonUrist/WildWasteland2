namespace WildWasteland.Modules.HUD
{
    /// <summary>Former Gunplay part of the HUD entry (Wave A split): the VanillaHits subscription moved to GunplayModule.Init (B09).</summary>
    public sealed partial class HudModule
    {
        /// <summary>Empty: VanillaHits.BeforeSetFsmFloat is subscribed by GunplayModule.</summary>
        partial void InitGunplay() { }
    }
}
