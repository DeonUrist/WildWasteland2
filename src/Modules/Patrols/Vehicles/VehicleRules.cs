using System;

namespace WildWasteland.Modules.Patrols
{
    internal static class VehicleRules
    {
        /// <summary>Returns the native clearance distance used when occupants leave this vehicle type.</summary>
        internal static float ExitDistance(string vehicleName)
        {
            string body = Core.Names.Prefab(vehicleName);
            bool large = body.StartsWith("Rustcargo", StringComparison.OrdinalIgnoreCase) || body.StartsWith("Rustchief", StringComparison.OrdinalIgnoreCase) || body.StartsWith("Rustallion", StringComparison.OrdinalIgnoreCase) || body.StartsWith("Rustliner", StringComparison.OrdinalIgnoreCase);
            return PatrolsModule.BailDistance * (large ? 2f : 1f);
        }
    }
}
