using System;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Compatibility access for editors awaiting their owning package conversion.</summary>
    public sealed partial class PatrolsModule
    {
        /// <summary>Requests the shared game font with Core's bounded retry policy.</summary>
        [Obsolete("refactor: use Core.UI.Skin.EnsureGameFont")]
        internal static void EnsureGameFont() { Skin.EnsureGameFont(); }
    }
}
