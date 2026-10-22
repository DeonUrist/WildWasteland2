using System;
using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>One Gunplay F7 page (group Weapons &amp; HUD): intro text, live values, the related F6 switches, then tools.cfg sections.</summary>
    internal class GunplayPage : ToolPage
    {
        readonly ConfigPage _cfg;
        readonly Action<ToolLayout> _live;
        readonly ConfigEntryBase[] _main;
        /// <summary>Text shown above the live values.</summary>
        public string Intro;

        /// <summary>Creates a page; <paramref name="sections"/> are the tools.cfg sections drawn below the switches.</summary>
        public GunplayPage(IModule owner, string title, ConfigFile tools, Action<ToolLayout> live, ConfigEntryBase[] main, params string[] sections)
        {
            Owner = owner; Group = ToolGroups.Weapons; Title = title; _live = live; _main = main;
            if (sections != null && sections.Length > 0) _cfg = new ConfigPage(owner, ToolGroups.Weapons, title, tools, sections);
        }

        /// <summary>Draws intro, live block, switches and the config sections.</summary>
        public override void Draw(ToolLayout l)
        {
            if (Intro != null) l.Text(Intro);
            if (_live != null) { l.Heading("Live"); _live(l); }
            if (_main != null && _main.Length > 0)
            {
                l.Heading("Settings");
                for (int i = 0; i < _main.Length; i++) if (_main[i] != null) l.Entry(_main[i]);
            }
            if (_cfg != null) _cfg.Draw(l);
        }
    }
}
