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
        public override IEnumerable<ToolPage> ToolPages()
        {
            var v = ToolGroups.Vehicles; var p = ToolGroups.Poses; var m = ToolGroups.Merchants;
            return new ToolPage[]
            {
                new ActionPage(this, v, "Patrol editor", PatrolEditorPage) { IsTool = true },
                new ActionPage(this, v, "Template recorder", RecorderPage) { IsTool = true },
                new ActionPage(this, p, "Turret spot editor", TurretSpotPage) { IsTool = true },
                new ActionPage(this, p, "Bus passenger pose editor", BusPosePage) { IsTool = true },
                new ActionPage(this, m, "Merchant item placement", MerchantSpotPage) { IsTool = true },
                new ActionPage(this, v, "War Rig", WarRigPage) { IsTool = true },
                new ActionPage(this, v, "AI overlay & debug spawns", DebugPage) { IsTool = true },
                new ActionPage(this, v, "World traffic", WorldTrafficPage),
                new ActionPage(this, v, "Spawning & cleanup", SpawningPage),
                new ActionPage(this, m, "Merchants", MerchantsPage),
            };
        }

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
        static void Section(ToolLayout l, ConfigFile file, string section)
        {
            foreach (var k in file.Keys)
                if (k.Section == section) { var e = file[k]; if (e != null) l.Entry(e); }
        }

        static string KeyName(ConfigEntry<Key> k) { return k == null || k.Value == Key.None ? "not bound" : k.Value.ToString(); }

        // ---------------------------------------------------------------- Patrols & vehicles

        void PatrolEditorPage(ToolLayout l)
        {
            l.Text("The patrol / convoy / merchant / loot editor (the \"ledger\"): patrol and convoy groups with their minimum distance, boss kills, chance, faction and roster; the car and truck templates (driver, passenger, turrets, loot, explode chance); merchant shop types; car and truck loot profiles. It pauses the game while open and saves PatrolsAndLoot.json and the template files.");
            l.Heading("Spawn controls");
            l.Text("With AllowDebugSpawns on, the editor shows: \"Spawn template\" (the selected template parked 8 m ahead of you, setup only), an \"S\" button per group and SPAWN in the footer (a real departure of that group from a nearby camp - these two need New arrival or Fury road in Game > World > Enemy Vehicles).");
            l.Toggle(AllowDebugSpawns, "Show spawn controls (AllowDebugSpawns)");
            l.Heading("Templates");
            l.Value("Loaded", CarTemplates.Summary);
            l.Value("Data folder", DataDir);
            if (l.Button("Reload templates from disk", Running, false, 320)) CarTemplates.Refresh(true);
        }

        void RecorderPage(ToolLayout l)
        {
            l.Text("The template recorder (ex Apocatemplater): sit in a vehicle you built, open it, choose name, faction, driver, passenger, loot and turrets, and the vehicle - frame, parts, hinge offsets, trailer, suspension - is saved as a new template in Patrols\\PlayerTemplates. Only while you sit in a vehicle.");
            bool seated = Running && TemplateExporter.Seated;
            if (seated)
            {
                if (l.Button("Record the vehicle I sit in", InPlay, true, 320))
                    ToolWindow.CloseThen(() => { if (Instance != null) Instance.OpenRecorder(); });
            }
            else l.Text("You are not seated in a vehicle: get in one, then open this page again (or use the hotkey).", Ledger.Danger);
            l.Heading("Hotkey");
            l.Value("Template recorder key", KeyName(TemplateDumpKey));
            l.Entry(TemplateDumpKey);
            l.Value("Saves to", CarTemplates.PlayerFolder ?? Path.Combine(DataDir, "PlayerTemplates"));
        }

        void WorldTrafficPage(ToolLayout l)
        {
            l.Text("Persistent world traffic: patrols, convoys, Coyote roamers, merchants and faction battles travel between known camps on road-influenced routes and become real cars near you. The game's debug map can show the routes.");
            if (Running) l.Value("Status", ConvoyEnabled.Value ? WorldTraffic.OverlayLine() : "off - Loner (Game > World > Enemy Vehicles)");
            l.Value("Road cache", TrafficRoads.Status);
            l.Heading("Debug map");
            l.Text("Open the game's debug map: routes, group labels, cleared camps, battle skulls and merchants are drawn on it.");
            l.Toggle(TrafficDebugMap, "Draw routes on the debug map (DebugMap)");
            l.Toggle(RealPatrolNames, "Real patrol names");
            l.Toggle(HideRouteType, "Hide route type (all gray, \"Distant traffic\")");
            l.Heading("Values");
            Section(l, Cfg.Main, Cfg.Section("World traffic"));
            l.Entry(TacticalDriving); l.Entry(FactionBattles);
        }

        void DebugPage(ToolLayout l)
        {
            l.Text("On-screen diagnostics and the editor's test spawns.");
            l.Toggle(AiOverlay, "AI overlay (one line per AI car, or the world traffic state)");
            if (Running)
            {
                l.Value("Driving AI cars", Pilot.All.Count.ToString());
                l.Value("Raider cars", PatrolMarker.All.Count.ToString());
                l.Value("Automatic raiders", Convoy.OverlayLine());
            }
            l.Toggle(AllowDebugSpawns, "Spawn controls in the patrol editor (AllowDebugSpawns)");
            l.Heading("Other");
            l.Entry(HideFsmWarnings);
            l.Text("Verbose logging of spawns, builds, crews and AI state changes is the shared [Debug] VerboseLog switch (Debug > Other settings).");
        }

        void SpawningPage(ToolLayout l)
        {
            l.Text("Automatic raiders come from the world traffic (Game > World > Enemy Vehicles). Caps of the groups near you and the cleanup of cars you left behind.");
            l.Toggle(CleanupEnabled, "Cleanup");
            l.Heading("Caps");
            Section(l, Cfg.Main, Cfg.Section("Spawning"));
        }

        static string _rigStatus = "";
        void WarRigPage(ToolLayout l)
        {
            l.Text("The War Rig: a six-wheel tanker semi-trailer for the Rustchief. The POI module places abandoned trucks with tankers, worn wheels, no engine and nearby zombies. World chance is in Points of interest. Temporary map markers are yellow Xs. The tanker shelters the player from sandstorms and remains available in truck templates and manual spawns.");
            l.Toggle(WarRig.S.SpawnEnabled, "War Rig spawns");
            l.Toggle(WarRig.S.GroundRecoveryEnabled, "Ground recovery");
            l.Heading("Status");
            l.Value("Model", File.Exists(Path.Combine(WarRig.WarRigFeature.Dir, WarRig.S.ModelFile)) ? WarRig.S.ModelFile + " found" : WarRig.S.ModelFile + " MISSING in " + WarRig.WarRigFeature.Dir);
            l.Value("Template", WarRig.Factory.Cached != null ? "built" : "not built yet (first spawn / save load)");
            l.Value("Rigs loaded", WarRig.WarRigUnit.All.Count.ToString());
            WarRig.WarRigUnit near = Nearest();
            if (near != null) l.Value("Nearest rig", near.name + "  (truck in reach: " + (string.IsNullOrEmpty(near.NearestTruck) ? "none" : near.NearestTruck) + ")");
            l.Heading("Tools");
            if (l.Button("Spawn a War Rig 14 m ahead", Running && InPlay && WarRig.S.SpawnEnabled.Value, true, 320))
                ToolWindow.CloseThen(() => { try { WarRig.WarRigFeature.SpawnNearPlayer(WarRig.S.EquipWheels); _rigStatus = "spawned"; } catch (Exception e) { _rigStatus = e.Message; WarRig.WarRigFeature.Say("War Rig: " + e.Message); } });
            int hit = l.Buttons("Re-apply layout (nearest)", "Rebind visuals (nearest)");
            if (hit >= 0 && near != null)
            {
                try { if (hit == 0) { WarRig.Layout.Apply(near.gameObject, true); _rigStatus = "layout re-applied, leg " + WarRig.Layout.LastLegLength.ToString("0.00") + " m"; } else { near.Rebind(); _rigStatus = "visuals rebound"; } }
                catch (Exception e) { _rigStatus = e.Message; }
            }
            if (_rigStatus.Length > 0) l.Value("Last action", _rigStatus);
        }
        static WarRig.WarRigUnit Nearest()
        {
            var cam = GameRefs.MainCamera; WarRig.WarRigUnit best = null; float bd = float.MaxValue;
            foreach (var r in WarRig.WarRigUnit.All)
            {
                if (r == null) continue;
                float d = cam != null ? (r.transform.position - cam.transform.position).sqrMagnitude : 0f;
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        // ---------------------------------------------------------------- Seat & turret poses

        void TurretSpotPage(ToolLayout l)
        {
            l.Text("Glowing markers on the human turret spots of the nearest raider car (within 80 m): yellow = the selected spot (the feet), orange = the other spots, green = its forward. Move it with the numpad in the game (F7 closed) and save it into the car's template file (riderPos / riderPos2 ...).");
            l.Toggle(TurretSpotEditor, "Turret spot editor on");
            l.Heading("Keys (in the game, F7 closed)");
            l.Text("Numpad 8 / 2 forward / back    4 / 6 left / right    7 / 9 down / up    Shift = x5\nNumpad 0 next turret    5 save the spot    . markers on / off");
            var s = Instance != null ? Instance.Spotter : null;
            l.Heading("Selection");
            if (!TurretSpotEditor.Value || s == null) { l.Text("Switch the editor on and stand near a raider car with turret spots."); return; }
            if (BusPassengerPoseEditor.Active) l.Text("The bus passenger pose editor is on: it owns the numpad, the turret spot editor waits.", Ledger.Danger);
            if (s.Car == null) { l.Text("No raider car with a turret spot within 80 m."); if (l.Button("Search again", true, false, 220)) s.Refind(); return; }
            l.Value("Nearest car", s.Car.name + "  [" + RiderApi.TemplateName(s.Car) + "]");
            var a = s.SelectedAnchor;
            l.Value("Selected spot", s.SlotName + " (" + s.SelectedRider + ")");
            if (a != null) l.Value("Local position", a.localPosition.ToString("F3"));
            s.Markers = l.Toggle("Markers", s.Markers);
            int hit = l.Buttons("Next turret", "SAVE spot");
            if (hit == 0) s.NextSlot(); else if (hit == 1) s.SaveSpot();
            if (s.Status.Length > 0) l.Value("Status", s.Status);
        }

        void BusPosePage(ToolLayout l)
        {
            l.Text("Live arm, leg and seat adjustment for seated bus passengers (merchant buses, the War rig bus): the nearest one within 80 m is edited, the saved pose applies to every bus passenger and merchant type (BusPassengerPose.json).");
            l.Toggle(BusPassengerPoseEditorEnabled, "Bus passenger pose editor on");
            l.Value("Toggle key", KeyName(BusPassengerPoseEditorKey));
            l.Entry(BusPassengerPoseEditorKey);
            l.Heading("Keys (in the game, F7 closed)");
            l.Text("Num0 next joint    Num/ both (mirrored) / left / right    Shift faster\nNum8 / 2 bend    Num4 / 6 sideways    Num7 / 9 twist\nSeat position: Num8 / 2 forward / back    Num4 / 6 left / right    Num7 / 9 down / up\nNum5 save    Num. reset to defaults\nDisable the other numpad editors (turret spots, merchant items) while using this one.");
            var b = Instance != null ? Instance.BusPose : null;
            l.Heading("Selection");
            if (!BusPassengerPoseEditor.Active || b == null) { l.Text("Switch the editor on and stand near a seated bus passenger."); return; }
            l.Value("Passenger", BusPassengerPoseEditor.Target != null ? BusPassengerPoseEditor.Target.name : "none within 80 m (spawn a bus template with a passenger)");
            l.Value("Joint / side", b.JointName + " / " + b.SideName);
            l.Value("Value", b.ValueText);
            int hit = l.Buttons("Next joint", "Next side", "SAVE", "RESET");
            if (hit == 0) b.NextJoint(); else if (hit == 1) b.NextSide(); else if (hit == 2) b.SavePose(); else if (hit == 3) b.ResetPose();
            if (b.Status.Length > 0) l.Value("Status", b.Status);
        }

        // ---------------------------------------------------------------- Merchants

        void MerchantSpotPage(ToolLayout l)
        {
            l.Text("Placement of the shop objects of the nearest merchant bus (within 80 m): the box, board, name and price labels and the sale items. The bus is held still while you edit; saving writes the layout to MerchantLayouts.json, shared by every merchant type of that template. (Absorbed debug MerchantSpotter.)");
            l.Toggle(MerchantEditorEnabled, "Merchant item editor on");
            l.Value("Toggle key", KeyName(MerchantEditorKey));
            l.Entry(MerchantEditorKey);
            l.Heading("Keys (in the game, F7 closed)");
            l.Text("Num/ object type    Num0 next object    Num5 save\nNum8 / 2 forward / back    Num4 / 6 sideways    Num7 / 9 down / up    Shift = x5    Num+ / Num- step size\nCtrl + Num4 / 6 yaw    Ctrl + Num7 / 9 pitch    Ctrl + Num8 / 2 roll\nDisable the turret spot editor while using this one.");
            var e = Instance != null ? Instance.MerchantSpot : null;
            l.Heading("Selection");
            if (!MerchantEditor.Enabled || e == null) { l.Text("Switch the editor on and stand near a merchant bus (spawn one from the patrol editor's MERCHANTS tab, not as an ordinary vehicle)."); return; }
            if (e.Car == null) { l.Text("No merchant bus within 80 m."); return; }
            l.Value("Bus", e.Car.name);
            l.Value("Object type", e.Kind);
            l.Value("Selected", e.Selected.Length > 0 ? e.Selected : "waiting for shop objects");
            l.Value("Step", e.StepSize.ToString("0.000") + " m");
            int hit = l.Buttons("Next type", "Next object", "Hold bus still", "SAVE layout");
            if (hit == 0) e.NextKind(); else if (hit == 1) e.NextObject(); else if (hit == 2) e.Freeze(); else if (hit == 3) e.SaveLayout();
            if (e.Status.Length > 0) l.Value("Status", e.Status);
        }

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
