using System;
using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// Old Patrols entry part for the settings that moved to Vehicles (Wave A split of PatrolsSettings.cs). Wave B: the Vehicles module
    /// binds them in `[Vehicles / ...]` (VehiclesSettings, D12 no migration); the partial methods stay empty and the statics forward until
    /// Wave C deletes this file.
    /// </summary>
    public sealed partial class PatrolsModule
    {
        /// <summary>Forwarder of [Vehicles / Combat] NPC ram damage multiplier.</summary>
        [Obsolete("refactor: Vehicles owns this setting (VehiclesSettings.NpcRamDamageMultiplier)")]
        internal static ConfigEntry<float> NpcRamDamageMultiplier { get { return VehiclesSettings.NpcRamDamageMultiplier; } }
        /// <summary>Forwarder of [Vehicles / Suspension] LiftKitCenterOfMass.</summary>
        [Obsolete("refactor: Vehicles owns this setting (VehiclesSettings.LiftKitCenterOfMass)")]
        internal static ConfigEntry<bool> SuspensionLiftCenterOfMass { get { return VehiclesSettings.LiftKitCenterOfMass; } }
        /// <summary>Forwarder of [Vehicles / Controls] ExitSpeedKmh.</summary>
        [Obsolete("refactor: Vehicles owns this setting (VehiclesSettings.ExitSpeedKmh)")]
        internal static ConfigEntry<float> ExitSpeedKmh { get { return VehiclesSettings.ExitSpeedKmh; } }

        /// <summary>Empty since Wave B: VehiclesModule binds the War Rig toggles in [Vehicles / War Rig].</summary>
        partial void BindVehiclesToggles(ModuleConfig cfg) { }

        /// <summary>Empty since Wave B: VehiclesModule binds the NPC ram damage multiplier in [Vehicles / Combat].</summary>
        partial void BindVehiclesCombat(ModuleConfig cfg) { }

        /// <summary>Empty since Wave B: VehiclesModule binds ExitSpeedKmh in [Vehicles / Controls] (tools.cfg).</summary>
        partial void BindVehiclesDebug(ModuleConfig cfg) { }

        /// <summary>Empty since Wave B: VehiclesModule binds LiftKitCenterOfMass in [Vehicles / Suspension] (tools.cfg).</summary>
        partial void BindVehiclesTuning(ModuleConfig cfg) { }
    }
}
