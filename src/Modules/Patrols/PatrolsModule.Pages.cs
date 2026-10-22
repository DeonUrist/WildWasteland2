using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    // F7 tool window pages (PORTING.md 7). Editors that open their own ledger window close F7 first (ToolWindow.CloseThen); the
    // numpad editors (turret spots, bus passenger pose, merchant items) keep running in-world after F7 closes - their pages show the
    // on/off switch, the key list, the live selection and the buttons for what the old editors did with keys.
    public sealed partial class PatrolsModule
    {
        /// <summary>Returns current owner-contributed patrol settings pages.</summary>
        public override IEnumerable<ToolPage> ToolPages()
        {
            var v = ToolGroups.Vehicles; var m = ToolGroups.Merchants;
            return new ToolPage[]
            {
                new ActionPage(this, v, "World traffic", WorldTrafficPage),
                new ActionPage(this, v, "Spawning & cleanup", SpawningPage),
                new ActionPage(this, m, "Merchants", MerchantsPage),
            };
        }

        /// <summary>Resolves the current module configuration section label.</summary>
        static string Sec(string sub) { return Cfg.Section(sub); }

        // "in play" while F7 itself pauses the game (GameRefs.InGame is false at timeScale 0): Menu "play" and SaveLoadGame "isPlay"
        static bool InPlay
        {
            get
            {
                var menu = GameRefs.Menu; var sl = GameRefs.SaveLoadGame;
                return menu != null && menu.Fsm.Initialized && menu.ActiveStateName == "play" && (sl == null || !sl.Fsm.Initialized || sl.ActiveStateName == "isPlay");
            }
        }

        // all entries of one tools.cfg / main cfg section as rows
        /// <summary>all entries of one tools.cfg / main cfg section as rows</summary>
        static void Section(ToolLayout l, ConfigFile file, string section)
        {
            foreach (var k in file.Keys)
                if (k.Section == section) { var e = file[k]; if (e != null) l.Entry(e); }
        }

        /// <summary>Formats the configured key label for the current settings UI.</summary>
        static string KeyName(ConfigEntry<Key> k) { return k == null || k.Value == Key.None ? "not bound" : k.Value.ToString(); }

        // ---------------------------------------------------------------- Patrols & vehicles

        /// <summary>Contributes current world-traffic controls to the owner settings page.</summary>
        void WorldTrafficPage(ToolLayout l)
        {
            l.Text("Persistent world traffic: patrols, convoys, Coyote roamers, merchants and faction battles travel between known camps on road-influenced routes and become real cars near you. The game's debug map can show the routes.");
            if (Running) l.Value("Status", ConvoyEnabled.Value ? WorldTraffic.OverlayLine() : "off - Loner (Game > World > Enemy Vehicles)");
            l.Value("Road cache", TrafficRoads.Status);
            l.Heading("Debug map");
            l.Text("Open the game's debug map: routes, group labels, cleared camps, battle skulls and merchants are drawn on it.");
            l.Text("Traffic route diagnostics are configured in Debug.");
            l.Toggle(RealPatrolNames, "Real patrol names");
            l.Toggle(HideRouteType, "Hide route type (all gray, \"Distant traffic\")");
            l.Heading("Values");
            Section(l, Cfg.Main, Cfg.Section("World traffic"));
            l.Entry(TacticalDriving); l.Entry(FactionBattles);
        }

        /// <summary>Materializes ing page for the current patrols module.</summary>
        void SpawningPage(ToolLayout l)
        {
            l.Text("Automatic raiders come from the world traffic (Game > World > Enemy Vehicles). Caps of the groups near you and the cleanup of cars you left behind.");
            l.Toggle(CleanupEnabled, "Cleanup");
            l.Heading("Caps");
            Section(l, Cfg.Main, Cfg.Section("Spawning"));
        }

        // ---------------------------------------------------------------- Merchants

        /// <summary>Contributes current merchant controls to the owner settings page.</summary>
        void MerchantsPage(ToolLayout l)
        {
            l.Text("Coyote trader buses (General / Mechanic / Organic shops) of the world traffic: parked setup buses and travelling traders with stock, prices and wanted lists that are saved. Stop a loaded merchant near you with Alt+Q (30 m) or the car horn (100 m). Shop types and rosters are edited in the patrol editor's MERCHANTS tab.");
            l.Toggle(Merchants, "Merchants");
            if (!ConvoyEnabled.Value) l.Text("Loner disables automatic merchants. Select New arrival or Fury road in Game > World > Enemy Vehicles.", Ledger.Danger);
            Section(l, Cfg.Main, Cfg.Section("Merchants"));
            if (Running) l.Value("Merchant buses loaded", MerchantHost.All.Count.ToString());
        }
    }
}
