using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Old PlayerBody entry part for the vehicle controls (Wave A split of PlayerBodyModule.cs). Wave B: the Vehicles module binds the
    /// ignition switch ([Vehicles / Controls] IgnitionKey) and the cassette keys ([Keys], same names), loads the ignition row, patches the
    /// ignition click and the steering, and runs the jobs. The partial methods stay empty and the statics forward until Wave C.
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        /// <summary>Forwarder of [Vehicles / Controls] IgnitionKey (read by the HUD driving hints).</summary>
        [Obsolete("refactor: Vehicles owns this setting (VehiclesSettings.IgnitionKey)")]
        internal static ConfigEntry<bool> IgnitionControlOn { get { return VehiclesSettings.IgnitionKey; } }
        /// <summary>Forwarder of the [Keys] cassette start/stop entry (Vehicles).</summary>
        [Obsolete("refactor: Vehicles owns this key (VehiclesSettings.CassetteKey)")]
        internal static ConfigEntry<Key> CassetteKey { get { return VehiclesSettings.CassetteKey; } }
        /// <summary>Forwarder of the [Keys] cassette volume-down entry (Vehicles).</summary>
        [Obsolete("refactor: Vehicles owns this key (VehiclesSettings.VolumeDownKey)")]
        internal static ConfigEntry<Key> VolumeDownKey { get { return VehiclesSettings.VolumeDownKey; } }
        /// <summary>Forwarder of the [Keys] cassette volume-up entry (Vehicles).</summary>
        [Obsolete("refactor: Vehicles owns this key (VehiclesSettings.VolumeUpKey)")]
        internal static ConfigEntry<Key> VolumeUpKey { get { return VehiclesSettings.VolumeUpKey; } }
        /// <summary>The cassette key as a legacy KeyCode.</summary>
        [Obsolete("refactor: use VehiclesSettings.CassetteKeyCode or IVehicleControls")]
        internal static KeyCode CassetteKeyCode { get { return VehiclesSettings.CassetteKeyCode; } }
        /// <summary>The volume-down key as a legacy KeyCode.</summary>
        [Obsolete("refactor: use VehiclesSettings.VolumeDownKeyCode or IVehicleControls")]
        internal static KeyCode VolumeDownKeyCode { get { return VehiclesSettings.VolumeDownKeyCode; } }
        /// <summary>The volume-up key as a legacy KeyCode.</summary>
        [Obsolete("refactor: use VehiclesSettings.VolumeUpKeyCode or IVehicleControls")]
        internal static KeyCode VolumeUpKeyCode { get { return VehiclesSettings.VolumeUpKeyCode; } }
        /// <summary>The ignition key mirrored from the game's Controls "Ignition" row (Vehicles IgnitionControl).</summary>
        [Obsolete("refactor: use IgnitionControl.KeyCode (Vehicles)")]
        internal static KeyCode IgnitionKeyCode { get { return IgnitionControl.KeyCode; } }

        /// <summary>Empty since Wave B: VehiclesModule binds [Vehicles / Controls] IgnitionKey.</summary>
        partial void BindVehicles(ModuleConfig cfg) { }

        /// <summary>Empty since Wave B: VehiclesModule binds the cassette keys (same [Keys] names).</summary>
        partial void BindVehiclesKeys(ModuleConfig cfg) { }

        /// <summary>Empty since Wave B: VehiclesModule loads the ignition row, patches the ignition click and the steering and runs the jobs.</summary>
        partial void InitVehicles() { }

        /// <summary>Empty since Wave B: VehiclesModule removes the ignition row on its own shutdown.</summary>
        partial void ShutdownVehicles() { }
    }
}
