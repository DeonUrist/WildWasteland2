using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// F7 pages of the old Guns entry shell: only the NPCAI-owned "Bosses &amp; orbs" page (B07 partial) remains here; the Gunplay pages
    /// moved to <see cref="GunplayPages"/>. Wave C deletes this with the Guns entry.
    /// </summary>
    internal static partial class GunsPages
    {
        /// <summary>Adds the NPCAI-owned "Bosses &amp; orbs" page (implemented in NPCAI/Behaviour/_fromGuns/GunsPages.NPCAI.cs).</summary>
        static partial void AddNpcaiPages(GunsModule m, ConfigFile tools, List<ToolPage> pages);

        /// <summary>Pages of the Guns shell entry (NPCAI part only).</summary>
        internal static List<ToolPage> Create(GunsModule m)
        {
            var pages = new List<ToolPage>();
            AddNpcaiPages(m, m.Config.Tools, pages);
            return pages;
        }

        /// <summary>Invariant integer text (used by the NPCAI page part).</summary>
        static string I(int v) { return v.ToString(CultureInfo.InvariantCulture); }
    }

    /// <summary>Old page type name used by the NPCAI page part; same behaviour as <see cref="GunplayPage"/>.</summary>
    [Obsolete("refactor: use GunplayPage")]
    internal sealed class GunsPage : GunplayPage
    {
        /// <summary>Creates the page (see <see cref="GunplayPage"/>).</summary>
        public GunsPage(IModule owner, string title, ConfigFile tools, Action<ToolLayout> live, ConfigEntryBase[] main, params string[] sections)
            : base(owner, title, tools, live, main, sections) { }
    }
}
