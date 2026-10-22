using System;
using UnityEngine;
namespace WildWasteland.Modules.NPC
{
    /// <summary>Existing sensing queries forwarding to the shared native vehicle engine service.</summary>
    internal static partial class Senses
    {
        [Obsolete("refactor: use Core.VehicleEngine")]
        private static class Nwh
        {
            /// <summary>Core owns vehicle-handle invalidation on scene changes.</summary>
            internal static void Clear() { }
            /// <summary>Typed compatibility ignition or native engine state.</summary>
            internal static bool EngineRunning(GameObject car) { return Core.VehicleEngine.Running(car); }
            /// <summary>Shared native throttle input.</summary>
            internal static float Throttle(GameObject car) { return Core.VehicleEngine.Throttle(car); }
        }
    }
}
