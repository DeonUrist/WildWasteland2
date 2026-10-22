using System;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Compatibility facade for Patrols consumers of the shared Core player state; B11 replaces remaining callers.</summary>
    [Obsolete("refactor: use Core.PlayerState")]
    internal static class PlayerRef
    {
        /// <summary>Clears canonical world references and the Patrols-specific POI cache.</summary>
        internal static void Reset() { PlayerState.Reset(); PatrolPoi.Reset(); }
        /// <summary>Forgets Patrols-specific POI state after a world-origin shift; Core player velocity is already world-relative.</summary>
        internal static void OriginShift(Vector3 shift) { PatrolPoi.Reset(); }
        /// <summary>The canonical player root transform.</summary>
        internal static Transform Player { get { return PlayerState.Transform; } }
        /// <summary>The canonical native player vehicle.</summary>
        internal static GameObject PlayerCar { get { return PlayerState.Car; } }
        /// <summary>Whether this transform owns a native vehicle controller.</summary>
        internal static bool HasVehicleController(Transform transform) { return PlayerState.HasVehicleController(transform); }
        /// <summary>The native player god-mode state.</summary>
        internal static bool GodMode { get { return PlayerState.GodMode; } }
        /// <summary>Canonical player motion sampled once per physics tick.</summary>
        internal static bool Target(out Vector3 position, out Vector3 velocity, out GameObject car) { return PlayerState.Target(out position, out velocity, out car); }
        /// <summary>Classifies player rig and its current vehicle.</summary>
        internal static int Kind(Transform transform) { return PlayerState.Kind(transform); }
    }
}
