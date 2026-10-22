namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Former Gunplay part of the PlayerBody entry (Wave A split): the blast-lance-from-the-seat collision rule moved to Gunplay
    /// (LanceSeatCollision, config [Gunplay / Projectiles] LanceIgnoresOwnCar). The partial methods stay empty until Wave C.
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        /// <summary>Empty: LanceIgnoresOwnCar is bound by GunplaySettings.</summary>
        partial void BindGunplay(Core.ModuleConfig cfg) { }

        /// <summary>Empty: the CreateObject postfix is subscribed by GunplayModule.</summary>
        partial void InitGunplay() { }
    }
}
