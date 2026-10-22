using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>F7 pages of the Gunplay module (group Weapons &amp; HUD): live info, the related F6 switches, then the tools.cfg sections.</summary>
    internal static class GunplayPages
    {
        /// <summary>Builds the four Gunplay pages in their original order.</summary>
        internal static List<ToolPage> Create(GunplayModule m)
        {
            var tools = m.Config.Tools;
            var pages = new List<ToolPage>();

            pages.Add(new GunplayPage(m, "Tracers & projectiles", tools, LiveTracers,
                new ConfigEntryBase[] { GunplaySettings.TracersEnabled, GunplaySettings.TracersDraw, GunplaySettings.PlayerGunTracers, GunplaySettings.VehicleDamage, GunplaySettings.WheelPopOff, GunplaySettings.MeleeOnWheels, GunplaySettings.LanceIgnoresOwnCar },
                "Gunplay / Tracers", "Gunplay / Ballistics", "Gunplay / Melee")
            { Intro = "Real bullets for NPC and player guns. Tracer look, bullet cap, hitbox sizes and vehicle damage tuning. Changes apply to the next shot." });

            pages.Add(new GunplayPage(m, "NPC aim", tools, LiveAim,
                new ConfigEntryBase[] { GunplaySettings.NpcAim },
                "Gunplay / NPC aim")
            { Intro = "How NPC gunmen aim their projectiles: body-centre aim and target lead (each NPC rolls a personal skill). Spread grows with distance (from the NPC module when it runs, else the built-in rule)." });

            pages.Add(new GunplayPage(m, "Ammo & magazines", tools, LiveAmmo,
                new ConfigEntryBase[] { GunplaySettings.PerGunMagazines, AmmoSettings.Enabled, AmmoSettings.HoldSeconds, AmmoSettings.FallbackKey },
                "Gunplay / Ammo")
            { Intro = "Per-gun magazines (saved in the game's save file under the key Apocaunloader.Magazines) and hold-Reload-to-unload." });

            pages.Add(new GunplayPage(m, "Armor & overflow", tools, LiveOverflow,
                new ConfigEntryBase[] { AmmoSettings.DropBackpackOverflow, AmmoSettings.DropArmorOverflow },
                new string[0])
            { Intro = "A smaller backpack or body armor no longer deletes what does not fit: the excess ammo drops as an ammo box, the excess armor as one plate per full 50 points. Failed drops keep the ammo/armor and retry every second." });
            return pages;
        }

        /// <summary>Invariant integer text.</summary>
        static string I(int v) { return v.ToString(CultureInfo.InvariantCulture); }
        /// <summary>"on"/"off".</summary>
        static string On(bool v) { return v ? "on" : "off"; }

        /// <summary>Live block of the tracers page.</summary>
        static void LiveTracers(ToolLayout l)
        {
            l.Value("Projectiles", On(GunplaySettings.ProjectilesEnabled));
            l.Value("Bullets in flight", I(Tracers.InFlight) + " / " + I(GunplaySettings.MaxTracers.Value));
            l.Value("Monster projectiles tracked", I(Tracers.OrbCount));
            l.Value("Player guns / NPC shooters known", I(Tracers.PlayerGunCount) + " / " + I(Tracers.ShooterCount));
            var hud = Core.ModApi.Get<Core.IHitFeedback>();
            l.Value("Hit feedback (HUD module)", hud != null ? (hud.ExternalReporter ? "reported by Gunplay" : "HUD observes vanilla hits") : "HUD module off");
        }

        /// <summary>Live block of the NPC aim page.</summary>
        static void LiveAim(ToolLayout l)
        {
            l.Value("Body-centre aim / lead", On(GunplaySettings.AimAtBody) + " / " + On(GunplaySettings.Lead));
            l.Value("Spread source", Core.ModApi.Get<Core.INpcApi>() != null ? "NPC module" : "built-in (+10 % per 5 m beyond 5 m)");
            l.Value("NPC shooters known", I(Tracers.ShooterCount));
        }

        /// <summary>Live block of the ammo page.</summary>
        static void LiveAmmo(ToolLayout l)
        {
            l.Value("Per-gun magazines", GunplaySettings.PerGunMagazines.Value ? PerGunAmmo.State : "off");
            l.Value("Tracked magazines / gun types", I(PerGunAmmo.TrackedItems) + " / " + I(PerGunAmmo.ClaimedTypes));
            l.Value("Gun in hand", PerGunAmmo.Current);
            var u = AmmoRunner.Unloader;
            l.Value("Unloader", u.Busy ? u.PhaseName + " (" + u.WeaponName + ")" : "idle");
        }

        /// <summary>Live block of the overflow page.</summary>
        static void LiveOverflow(ToolLayout l)
        {
            l.Value("Backpack ammo clamps watched", I(BackpackOverflow.Tracked));
            l.Value("Armor clamps watched", I(ArmorOverflow.Tracked));
        }
    }
}
