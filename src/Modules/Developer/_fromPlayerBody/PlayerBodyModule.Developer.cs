using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (split from PlayerBody/PlayerBodyModule.cs in Wave A): the weapon pose editor switch and its key bindings.
    /// Target: Developer (B16).
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        internal static ConfigEntry<bool> WeaponAdjust;

        /// <summary>Binds the weapon pose editor keys (tools.cfg "Pose key: ..." entries).</summary>
        partial void BindDeveloperKeys(ModuleConfig cfg)
        {
            GunPose.BindEditorKeys(cfg);
        }

        /// <summary>Binds [PlayerBody / Editors] WeaponAdjustment (tools.cfg).</summary>
        partial void BindDeveloperTools(ModuleConfig cfg)
        {
            WeaponAdjust = cfg.Tool("Editors", "WeaponAdjustment", false, "Weapon pose editor (third person, weapon drawn): numpad 8/2 6/4 7/1 move the weapon in the hand, 5 move/rotate, 9/3 pick the animation, - / * delete/copy/paste. Aiming: Page Up/Down lift the hands, Home/End tilt the head, Insert/Delete push the hands forward. Saved to config/WildWasteland/PlayerBody/weapon-poses.txt and aim-lift.txt. These keys do nothing while this is off.");
        }
    }
}
