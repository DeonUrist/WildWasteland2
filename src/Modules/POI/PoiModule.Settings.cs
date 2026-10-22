using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.POI
{
    /// <summary>Settings contributions owned by POI.</summary>
    public sealed partial class PoiModule
    {
        /// <summary>Registers the existing feature row at its original page position.</summary>
        public override void Settings(SettingsShell shell) { shell.Custom(this, SettingsShell.Page.World, "", DrawSettingsRow, 20); }
        /// <summary>Draws the existing feature setting.</summary>
        private void DrawSettingsRow(ToolLayout l) { SettingsShell.Switch(l, "New POI", Config.Enabled); }
    }
}
