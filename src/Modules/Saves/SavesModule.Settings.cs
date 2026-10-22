using System.Collections.Generic;
using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.Saves
{
    /// <summary>Saves-owned settings and full-surface save library registration.</summary>
    public sealed partial class SavesModule
    {
        /// <summary>Contributes system/save controls to the shared settings page.</summary>
        public override void Settings(SettingsShell shell) { shell.Custom(this, SettingsShell.Page.System, "", SaveLibraryPage.DrawSystem); }
        /// <summary>Exposes the library as an ordinary authored page with its own full-surface renderer.</summary>
        public override IEnumerable<ToolPage> ToolPages() { yield return SaveLibraryPage.Instance; }
    }
}
