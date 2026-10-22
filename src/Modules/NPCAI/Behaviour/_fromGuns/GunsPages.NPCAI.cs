using System.Collections.Generic;
using BepInEx.Configuration;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Guns
{
    /// <summary>NPCAI part of the Guns F7 pages (Wave A split, B07): the "Bosses &amp; orbs" page with the human boss health cap.</summary>
    internal static partial class GunsPages
    {
        static readonly List<string> _bosses = new List<string>();

        /// <summary>Adds the "Bosses &amp; orbs" page after the Gunplay pages (same position and content as before the split).</summary>
        static partial void AddNpcaiPages(GunsModule m, ConfigFile tools, List<ToolPage> pages)
        {
            pages.Add(new GunsPage(m, "Bosses & orbs", tools, LiveBosses,
                new ConfigEntryBase[] { GunsModule.BossHealth, GunsModule.BossHpPercent, GunsModule.OrbsHitWalls },
                new string[0])
            { Intro = "Human boss health in % of the game's own (a cap, safe across saves) and monster projectiles (orbs) that stop at walls and vehicles." });
        }

        static void LiveBosses(ToolLayout l)
        {
            l.Value("Bosses tracked", I(Bosses.Count));
            Bosses.Describe(_bosses);
            for (int i = 0; i < _bosses.Count; i++) l.Text(_bosses[i]);
            l.Value("Monster projectiles in flight", I(Tracers.OrbCount));
        }
    }
}
