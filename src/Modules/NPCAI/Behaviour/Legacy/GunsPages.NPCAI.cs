using System.Collections.Generic;
using BepInEx.Configuration;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Guns
{
    /// <summary>Former NPCAI part of the Guns F7 pages. Empty since B07: the "Bosses" page is an NPCAI page (orb rows dropped, B09-10).</summary>
    internal static partial class GunsPages
    {
        /// <summary>Empty (B07): NPCAIModule adds the "Bosses" page.</summary>
        static partial void AddNpcaiPages(GunsModule m, ConfigFile tools, List<ToolPage> pages) { }
    }
}
