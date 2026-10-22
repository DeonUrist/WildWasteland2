using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.Guns
{
    /// <summary>Settings contributions owned by Gunplay.</summary>
    public sealed partial class GunsModule
    {
        /// <summary>Registers the existing feature row at its original page position.</summary>
        public override void Settings(SettingsShell shell) { shell.Custom(this, SettingsShell.Page.Hud, "", DrawSettingsRow, 160); }
        /// <summary>Draws the existing feature setting.</summary>
        private void DrawSettingsRow(ToolLayout l) { SettingsShell.Switch(l, "Toggle Ironsights", ToggleIronsights); }
    }
}
