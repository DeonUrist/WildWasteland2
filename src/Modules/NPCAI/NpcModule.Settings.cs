using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.NPC
{
    /// <summary>Settings contributions owned by NPCAI.</summary>
    public sealed partial class NpcModule
    {
        /// <summary>Registers the existing feature row at its original page position.</summary>
        public override void Settings(SettingsShell shell) { shell.Custom(this, SettingsShell.Page.Hud, "", DrawSettingsRow, 170); }
        /// <summary>Draws the existing feature setting.</summary>
        private void DrawSettingsRow(ToolLayout l) { l.KeyPair("Shout", ShoutKey, "Shout modifier", ShoutModifier); }
    }
}
