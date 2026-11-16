using System;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    internal static class VehicleRules
    {
        internal static Vector3 MovingBailVelocity(GameObject car, Rigidbody body)
        {
            if (body == null) return Vector3.zero;
            var velocity = Vector3.ProjectOnPlane(body.velocity, Vector3.up);
            if (!Core.VehicleExitRoll.Required(velocity)) return Vector3.zero;
            string prefab = Core.Names.Prefab(car.name);
            return prefab.Equals("Motorcycle", StringComparison.OrdinalIgnoreCase)
                || prefab.Equals("Halfbreed", StringComparison.OrdinalIgnoreCase) ? Vector3.zero : velocity;
        }

        /// <summary>Returns the native clearance distance used when occupants leave this vehicle type.</summary>
        internal static float ExitDistance(string vehicleName)
        {
            string body = Core.Names.Prefab(vehicleName);
            bool large = body.StartsWith("Rustcargo", StringComparison.OrdinalIgnoreCase) || body.StartsWith("Rustchief", StringComparison.OrdinalIgnoreCase) || body.StartsWith("Rustallion", StringComparison.OrdinalIgnoreCase) || body.StartsWith("Rustliner", StringComparison.OrdinalIgnoreCase);
            return PatrolsModule.BailDistance * (large ? 2f : 1f);
        }
    }
}
