using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // MapMagic's root is saved by the game and moves with every NWH origin jump. Its local world frame does not.
    // TotalOffset alone is not sufficient: a loaded game restores MapMagic's position in a fresh FloatingOrigin instance.
    // WW: the anchor and the jump hook are Core.Origin; PatrolsModule.OnOriginShift calls OriginShift(delta).
    internal static class TrafficSpace
    {
        internal static Vector3 Origin { get { return Core.Origin.Anchor; } }
        internal static Vector3 World(Vector3 scene) { return Core.Origin.World(scene); }
        internal static Vector3 Scene(Vector3 world) { return Core.Origin.Scene(world); }
        internal static void Reset() { }   // Core.Origin resets its anchor on scene load

        // delta = oldTotalOffset - newTotalOffset (the old FloatingOrigin.Jump postfix's "shift")
        internal static void OriginShift(Vector3 shift)
        {
            // Routes and detection memories are stable-world coordinates. Only legacy physics caches need translation.
            foreach (var pilot in Pilot.All) if (pilot != null) pilot.OriginShift(shift);
            PlayerRef.OriginShift(shift);
        }
    }
}
