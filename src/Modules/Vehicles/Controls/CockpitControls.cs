using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>IVehicleControls provider (ex PlayerBody Car): cockpit state of the player's car and headlight / handbrake commands for any car.</summary>
    internal sealed class CockpitControls : IVehicleControls
    {
        /// <summary>Cockpit state of the player's current car (false for other vehicles: only the player's cockpit is tracked).</summary>
        public bool TryGetCockpit(GameObject vehicle, out CockpitState state) { return Car.TryCockpit(vehicle, out state); }

        /// <summary>True while the module runs and the player sits in a car with its cockpit found.</summary>
        public bool PlayerControlsActive { get { return VehiclesModule.Running && Car.Current != null && PlayerState.InCar && !PlayerState.Dead; } }

        /// <summary>Switches the headlights through the car's light switch / headlight FSMs (same path as the raider cars).</summary>
        public bool SetHeadlights(GameObject vehicle, bool on)
        {
            if (vehicle == null) return false;
            NativeVehicleSwitches.Headlights(vehicle, on);
            return true;
        }

        /// <summary>Moves the handbrake lever FSM (or NWH input.Handbrake when the car has no lever).</summary>
        public bool SetHandbrake(GameObject vehicle, bool on)
        {
            if (vehicle == null) return false;
            NativeVehicleSwitches.Handbrake(vehicle, on);
            return true;
        }
    }
}
