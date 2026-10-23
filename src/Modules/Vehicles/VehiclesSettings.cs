using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WarRig = WildWasteland.Modules.Vehicles.WarRig;   // refs-ok: Vehicles-owned namespace until the Wave C rename

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// Every config entry of the Vehicles module (D12 fresh start: new `[Vehicles / ...]` sections, no migration from the old
    /// `[Patrols]`, `[Patrols / Combat]`, `[Patrols / Debug]`, `[Patrols / Combat tuning]` and `[PlayerBody / Vehicles]` keys).
    /// The cassette hotkeys keep their `[Keys]` names. Also the shared settings-page rows.
    /// </summary>
    internal static class VehiclesSettings
    {
        /// <summary>[Vehicles / War Rig] Spawns: abandoned War Rig POI and manual War Rig spawns.</summary>
        internal static ConfigEntry<bool> WarRigSpawns;
        /// <summary>[Vehicles / War Rig] Ground recovery: rescue a War Rig that fell below the terrain.</summary>
        internal static ConfigEntry<bool> WarRigGroundRecovery;
        /// <summary>[Vehicles / Combat] NPC ram damage multiplier for every car hitting a creature other than the player.</summary>
        internal static ConfigEntry<float> NpcRamDamageMultiplier;
        /// <summary>[Vehicles / Controls] Ignition key: the "Ignition" row on the game's Controls screen.</summary>
        internal static ConfigEntry<bool> IgnitionKey;
        /// <summary>[Vehicles / Controls] ExitSpeedKmh (tools.cfg): the player may leave a car below this speed.</summary>
        internal static ConfigEntry<float> ExitSpeedKmh;
        /// <summary>[Vehicles / Suspension] LiftKitCenterOfMass (tools.cfg): lower centre of mass on cars with a lift kit.</summary>
        internal static ConfigEntry<bool> LiftKitCenterOfMass;
        /// <summary>[Keys] Cassette start/stop.</summary>
        internal static ConfigEntry<Key> CassetteKey;
        /// <summary>[Keys] Cassette volume down.</summary>
        internal static ConfigEntry<Key> VolumeDownKey;
        /// <summary>[Keys] Cassette volume up.</summary>
        internal static ConfigEntry<Key> VolumeUpKey;

        /// <summary>The cassette key as a legacy KeyCode (Car polls Input.GetKeyDown).</summary>
        internal static KeyCode CassetteKeyCode { get { return KeyMap.Code(CassetteKey.Value); } }
        /// <summary>The volume-down key as a legacy KeyCode.</summary>
        internal static KeyCode VolumeDownKeyCode { get { return KeyMap.Code(VolumeDownKey.Value); } }
        /// <summary>The volume-up key as a legacy KeyCode.</summary>
        internal static KeyCode VolumeUpKeyCode { get { return KeyMap.Code(VolumeUpKey.Value); } }

        /// <summary>Binds every Vehicles entry (called for the module whether it is enabled or not, before any Init).</summary>
        internal static void Bind(ModuleConfig cfg)
        {
            WarRigSpawns = cfg.Player("War Rig", "Spawns", true,
                "Allow manual War Rig spawns. Natural abandoned War Rig encounters follow New POI in World. Existing rigs are unaffected");
            WarRigGroundRecovery = cfg.Player("War Rig", "Ground recovery", false,
                "Rescue a War Rig (with its truck and passengers) after a confirmed fall below the terrain. Off by default");
            NpcRamDamageMultiplier = cfg.Player("Combat", "NPC ram damage multiplier", 4f,
                "Damage multiplier when any car hits a creature other than the player, including NPC-driven cars: 4 = four times the game's bumper damage, 1 = vanilla, 0 = no damage", new AcceptableValueRange<float>(0f, 10f));
            IgnitionKey = cfg.Player("Controls", "IgnitionKey", true,
                "Adds an \"Ignition\" row to the game's Controls screen (default I): each press performs one ignition/start panel click (ignition on, crank, then stop on standard cars).");
            ExitSpeedKmh = cfg.Tool("Controls", "ExitSpeedKmh", 30f,
                "You can leave your car below this speed (km/h). The game's own limit is 6 m/s = 21.6 km/h, too low to get out of a car a truck keeps shoving",
                new AcceptableValueRange<float>(5f, 200f));
            LiftKitCenterOfMass = cfg.Tool("Suspension", "LiftKitCenterOfMass", true,
                "A raider car with a suspension lift kit carries its centre of mass lower (0.3 m, plus 1.5 x any extra lift of the body), so tall cars on big wheels do not flip in turns (Part Adjustment uses the same rule)");
            CassetteKey = cfg.Hotkey("Cassette start/stop", Key.Z, "Driving: start / stop the cassette player. Starting at zero volume sets 0.5. None = no hotkey.");
            VolumeDownKey = cfg.Hotkey("Cassette volume down", Key.Minus, "Driving: cassette volume -0.1 (also keypad minus). None = no hotkey.");
            VolumeUpKey = cfg.Hotkey("Cassette volume up", Key.Equals, "Driving: cassette volume +0.1 (+ on the main keyboard, also keypad plus). None = no hotkey.");
            WarRig.S.SpawnEnabled = WarRigSpawns;
            WarRig.S.GroundRecoveryEnabled = WarRigGroundRecovery;
        }

        /// <summary>Adds the Vehicles rows to the shared PLAYER / WORLD / Debug settings pages.</summary>
        internal static void Rows(IModule owner, SettingsShell shell)
        {
            shell.Row(owner, SettingsShell.Page.Player, "Vehicles", IgnitionKey, "Ignition key on the Controls screen");
            shell.Row(owner, SettingsShell.Page.Player, "Vehicles", ExitSpeedKmh, "Exit speed (km/h)");
            shell.Custom(owner, SettingsShell.Page.Debug, "Tuning", DrawTuning, 420);
            shell.Resettable(owner, SettingsShell.Page.Player, IgnitionKey);
            shell.Resettable(owner, SettingsShell.Page.Debug, NpcRamDamageMultiplier);
        }
        /// <summary>Vehicle mechanics tuning at the end of Debug, outside player-facing World options.</summary>
        private static void DrawTuning(ToolLayout l)
        {
            l.Heading("Vehicles");
            l.Toggle(WarRigGroundRecovery, "War Rig ground recovery"); l.Hint(WarRigGroundRecovery.Description.Description);
            l.Toggle(LiftKitCenterOfMass, "Lift-kit centre of mass"); l.Hint(LiftKitCenterOfMass.Description.Description);
            l.Entry(NpcRamDamageMultiplier);
        }
    }
}
