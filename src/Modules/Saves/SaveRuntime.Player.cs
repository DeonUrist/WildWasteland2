using WildWasteland.Core;
using UI = WildWasteland.Core.UI;
using Events = WildWasteland.Core.Events;
using System;
using UnityEngine;
namespace WildWasteland.Modules.Saves
{
    /// <summary>Existing Saves entry points forwarding to canonical Core player/standing services until B04 converts callers.</summary>
    internal static partial class SaveRuntime
    {
        /// <summary>Native player in-vehicle transition state.</summary>
        internal static bool InCar { get { return PlayerState.InVehicleTransition; } }
        /// <summary>Current player health from the shared cached native state.</summary>
        internal static float Health() { return PlayerState.Health; }
    }
}
