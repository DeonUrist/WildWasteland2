using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (Presentation, B05): the shell's settings-window rows. The PLAYER page moved to PresentationModule
    /// (CharacterPages.DrawPlayer); the two HUD rows below draw the HUD part's vehicle hint switches (owner B10) at their old position.
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        /// <summary>Contributes the vehicle hint/icon switches of the HUD part to the HUD page.</summary>
        public override void Settings(SettingsShell shell)
        {
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawVehicleHints, 220);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawVehicleIcons, 240);
        }
        /// <summary>Vehicle hint switch at its original HUD position.</summary>
        private static void DrawVehicleHints(ToolLayout l) { SettingsShell.Switch(l, "Vehicle Hints", VehicleHotkeyHint); }
        /// <summary>Vehicle icon switch at its original HUD position.</summary>
        private static void DrawVehicleIcons(ToolLayout l) { SettingsShell.Switch(l, "Vehicle Icons", VehicleStatusHint); }
    }
}
