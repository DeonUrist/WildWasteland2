using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrols settings owned by Vehicles (War Rig toggles, NPC ram damage multiplier, exit speed, lift-kit centre of mass); Wave A split of PatrolsSettings.cs.</summary>
    public sealed partial class PatrolsModule
    {
        internal static ConfigEntry<float> NpcRamDamageMultiplier;
        internal static ConfigEntry<bool> SuspensionLiftCenterOfMass;
        internal static ConfigEntry<float> ExitSpeedKmh;

        /// <summary>Binds the War Rig F6 toggles (spawns, ground recovery) in their old position after the Patrols feature toggles.</summary>
        partial void BindVehiclesToggles(ModuleConfig cfg)
        {
            WarRig.S.SpawnEnabled = cfg.Player(null, "War Rig spawns", true,
                "Allow the abandoned War Rig POI and manual War Rig spawns. World discovery chance is in POI / World. Existing rigs are unaffected");
            WarRig.S.GroundRecoveryEnabled = cfg.Player(null, "War Rig ground recovery", false,
                "Rescue a War Rig (with its truck and passengers) after a confirmed fall below the terrain. Off by default");
        }

        /// <summary>Binds the F6 Combat entry NPC ram damage multiplier (applies to every car, Vehicles rule).</summary>
        partial void BindVehiclesCombat(ModuleConfig cfg)
        {
            NpcRamDamageMultiplier = cfg.Player("Combat", "NPC ram damage multiplier", 4f,
                "Damage multiplier when any car hits a creature other than the player, including NPC-driven cars: 4 = four times the game's bumper damage, 1 = vanilla, 0 = no damage", new AcceptableValueRange<float>(0f, 10f));
        }

        /// <summary>Binds the tools.cfg Debug entry ExitSpeedKmh (player exit-speed rule).</summary>
        partial void BindVehiclesDebug(ModuleConfig cfg)
        {
            ExitSpeedKmh = cfg.Tool(TDebug, "ExitSpeedKmh", 30f,
                "You can leave your car below this speed (km/h). The game's own limit is 6 m/s = 21.6 km/h, too low to get out of a car a truck keeps shoving",
                new AcceptableValueRange<float>(5f, 200f));
        }

        /// <summary>Binds the Combat tuning entry SuspensionLiftCenterOfMass (lift-kit centre of mass).</summary>
        partial void BindVehiclesTuning(ModuleConfig cfg)
        {
            SuspensionLiftCenterOfMass = cfg.Tool(TCombat, "SuspensionLiftCenterOfMass", true, "A raider car with a suspension lift kit carries its centre of mass lower (0.3 m, plus 1.5 x any extra lift of the body), so tall cars on big wheels do not flip in turns (Part Adjustment uses the same rule)");
        }
    }
}
