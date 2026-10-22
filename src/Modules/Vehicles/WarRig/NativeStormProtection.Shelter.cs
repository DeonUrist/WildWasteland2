using UnityEngine;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Patrols.WarRig
{
    /// <summary>
    /// Vehicles part of the storm protection (the storm patches are Survival's NativeStormProtection.cs): the player inside a War Rig tank is
    /// sheltered; no rig loaded = nothing sheltered. The geometry is WarRigShelter, which Vehicles also registers with IShelterRegistry.
    /// </summary>
    internal static partial class NativeStormProtection
    {
        /// <summary>True when <paramref name="target"/> is the player (or part of it) and the player is inside a War Rig tank.</summary>
        internal static bool Protected(GameObject target)
        {
            var actor = Player;
            return actor != null && target != null && (target == actor || target.transform.IsChildOf(actor.transform)) && WarRigShelter.Inside(actor);
        }

        /// <summary>True when no War Rig is loaded (first-line filter of every storm hook).</summary>
        private static bool NoRigs { get { return WarRigUnit.All.Count == 0; } }
    }
}
