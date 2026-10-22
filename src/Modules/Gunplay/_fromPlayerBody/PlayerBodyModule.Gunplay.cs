using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (split from PlayerBody/PlayerBodyModule.cs in Wave A): a blast lance thrown from the driver's seat ignores
    /// the own car. Target: Gunplay (B09).
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        internal static ConfigEntry<bool> LanceIgnoresOwnCar;

        /// <summary>Binds [PlayerBody / Vehicles] LanceIgnoresOwnCar.</summary>
        partial void BindGunplay(ModuleConfig cfg)
        {
            LanceIgnoresOwnCar = cfg.Player("Vehicles", "LanceIgnoresOwnCar", true, "A blast lance thrown from the driver's seat flies out of your own car instead of blowing up on it.");
        }

        /// <summary>Init: the CreateObject postfix that makes the lance ignore the own car's colliders.</summary>
        partial void InitGunplay()
        {
            Hooks.CreateObjectEnter.Postfix(this, Projectiles.AfterCreateObject);
        }
    }
}
