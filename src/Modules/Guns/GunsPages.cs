using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Guns
{
    /// <summary>F7 pages of the Guns module (group Weapons &amp; HUD): live info, the related F6 switches, then the tools.cfg sections.</summary>
    internal static class GunsPages
    {
        static readonly List<string> _bosses = new List<string>();

        internal static List<ToolPage> Create(GunsModule m)
        {
            var tools = m.Config.Tools;
            var pages = new List<ToolPage>();

            pages.Add(new GunsPage(m, "Tracers & projectiles", tools, LiveTracers,
                new ConfigEntryBase[] { GunsModule.TracersEnabled, GunsModule.TracersDraw, GunsModule.PlayerGunTracers, GunsModule.VehicleDamage, GunsModule.WheelPopOff, GunsModule.MeleeOnWheels },
                "Guns / Tracers", "Guns / Ballistics", "Guns / Melee")
            { Intro = "Real bullets for NPC and player guns (ex Gunplay). Tracer look, bullet cap, hitbox sizes and vehicle damage tuning. Changes apply to the next shot." });

            pages.Add(new GunsPage(m, "NPC aim", tools, LiveAim,
                new ConfigEntryBase[] { GunsModule.NpcAim },
                "Guns / NPC aim")
            { Intro = "How NPC gunmen aim their projectiles: body-centre aim and target lead (each NPC rolls a personal skill). Spread grows with distance (from the NPC module when it runs, else the built-in rule)." });

            pages.Add(new GunsPage(m, "Ammo & magazines", tools, LiveAmmo,
                new ConfigEntryBase[] { GunsModule.PerGunMagazines, AmmoSettings.Enabled, AmmoSettings.HoldSeconds, AmmoSettings.FallbackKey },
                "Guns / Ammo")
            { Intro = "Per-gun magazines (saved in the game's save file under the key Apocaunloader.Magazines) and hold-Reload-to-unload." });

            pages.Add(new GunsPage(m, "Armor & overflow", tools, LiveOverflow,
                new ConfigEntryBase[] { AmmoSettings.DropBackpackOverflow, AmmoSettings.DropArmorOverflow },
                new string[0])
            { Intro = "A smaller backpack or body armor no longer deletes what does not fit: the excess ammo drops as an ammo box, the excess armor as one plate per full 50 points. Failed drops keep the ammo/armor and retry every second." });

            pages.Add(new GunsPage(m, "Bosses & orbs", tools, LiveBosses,
                new ConfigEntryBase[] { GunsModule.BossHealth, GunsModule.BossHpPercent, GunsModule.OrbsHitWalls },
                new string[0])
            { Intro = "Human boss health in % of the game's own (a cap, safe across saves) and monster projectiles (orbs) that stop at walls and vehicles." });
            return pages;
        }

        static string I(int v) { return v.ToString(CultureInfo.InvariantCulture); }
        static string On(bool v) { return v ? "on" : "off"; }

        static void LiveTracers(ToolLayout l)
        {
            l.Value("Projectiles", On(GunsModule.ProjectilesEnabled));
            l.Value("Bullets in flight", I(Tracers.InFlight) + " / " + I(GunsModule.MaxTracers.Value));
            l.Value("Monster projectiles tracked", I(Tracers.OrbCount));
            l.Value("Player guns / NPC shooters known", I(Tracers.PlayerGunCount) + " / " + I(Tracers.ShooterCount));
            l.Value("Hit feedback (HUD module)", Services.Hud != null ? (Services.Hud.ExternalReporter ? "reported by Guns" : "HUD observes vanilla hits") : "HUD module off");
        }

        static void LiveAim(ToolLayout l)
        {
            l.Value("Body-centre aim / lead", On(GunsModule.AimAtBody) + " / " + On(GunsModule.Lead));
            l.Value("Spread source", Services.Npc != null ? "NPC module" : "built-in (+10 % per 5 m beyond 5 m)");
            l.Value("NPC shooters known", I(Tracers.ShooterCount));
        }

        static void LiveAmmo(ToolLayout l)
        {
            l.Value("Per-gun magazines", GunsModule.PerGunMagazines.Value ? PerGunAmmo.State : "off");
            l.Value("Tracked magazines / gun types", I(PerGunAmmo.TrackedItems) + " / " + I(PerGunAmmo.ClaimedTypes));
            l.Value("Gun in hand", PerGunAmmo.Current);
            var u = AmmoRunner.Unloader;
            l.Value("Unloader", u.Busy ? u.PhaseName + " (" + u.WeaponName + ")" : "idle");
        }

        static void LiveOverflow(ToolLayout l)
        {
            l.Value("Backpack ammo clamps watched", I(BackpackOverflow.Tracked));
            l.Value("Armor clamps watched", I(ArmorOverflow.Tracked));
        }

        static void LiveBosses(ToolLayout l)
        {
            l.Value("Bosses tracked", I(Bosses.Count));
            Bosses.Describe(_bosses);
            for (int i = 0; i < _bosses.Count; i++) l.Text(_bosses[i]);
            l.Value("Monster projectiles in flight", I(Tracers.OrbCount));
        }
    }

    internal sealed class GunsPage : ToolPage
    {
        readonly ConfigPage _cfg;
        readonly Action<ToolLayout> _live;
        readonly ConfigEntryBase[] _main;
        public string Intro;

        public GunsPage(IModule owner, string title, ConfigFile tools, Action<ToolLayout> live, ConfigEntryBase[] main, params string[] sections)
        {
            Owner = owner; Group = ToolGroups.Weapons; Title = title; _live = live; _main = main;
            if (sections != null && sections.Length > 0) _cfg = new ConfigPage(owner, ToolGroups.Weapons, title, tools, sections);
        }

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
