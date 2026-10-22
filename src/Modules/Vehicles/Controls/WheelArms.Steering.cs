using NWH.VehiclePhysics2;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Vehicles part of WheelArms (the seat/arms solver is Presentation's): the gameplay rule of the removable steering wheel. While the
    /// wheel is missing from the player's car, NWH Steering.CalculateSteerAngles sees zero steering input. The forced returnToCenter of the
    /// player's car is applied by the Vehicles cockpit tick (Car.HoldSteering).
    /// </summary>
    internal static partial class WheelArms
    {
        /// <summary>True while the player's car has a removable steering wheel and none is fitted on its mount.</summary>
        internal static bool MissingWheel { get { return _removableWheel && _mount != null && FittedWheel(_mount) == null; } }

        /// <summary>Installs the Steering.CalculateSteerAngles prefix (module patch owned by <paramref name="owner"/>).</summary>
        internal static void PatchSteering(IModule owner)
        {
            Patches.Patch(owner, Patches.Method(typeof(Steering), "CalculateSteerAngles"), prefix: Patches.Method(typeof(WheelArms), "BeforeSteer"));
        }

        /// <summary>
        /// Prefix of NWH Steering.CalculateSteerAngles (every vehicle, every physics step): zeroes the steering input of the player's car while its
        /// removable wheel is missing. Early-out: not the player's steering (reference compare) or the wheel is fitted.
        /// </summary>
        private static void BeforeSteer(Steering __instance)
        {
            if (!ReferenceEquals(__instance, _steering) || !MissingWheel) return;
            _vehicle.input.Steering = 0f; // Native steering still owns smoothing and centering, after all keyboard/mouse input writers.
        }
    }
}
