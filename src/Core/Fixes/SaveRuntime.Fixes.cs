using System;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Existing Saves entry points forwarding to canonical Core player/standing services until B04 converts callers.</summary>
    internal static partial class SaveRuntime
    {
        /// <summary>Installs the Core supply fix at its original native save-integration point.</summary>
        static partial void InstallFixes() { InventorySupplies.Install(); }
        /// <summary>Native player in-vehicle transition state.</summary>
        internal static bool InCar { get { return PlayerState.InVehicleTransition; } }
        /// <summary>Current player health from the shared cached native state.</summary>
        internal static float Health() { return PlayerState.Health; }
        /// <summary>Finds clear main-terrain standing ground using the canonical Core probe.</summary>
        internal static bool Surface(Vector3 center, Transform player, out Vector3 position) { return StandingSurface.TryFind(center, player, out position); }
    }
}
