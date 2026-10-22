using System;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Old PlayerBody entry part (Wave A split) for the driver's seat. Wave B (B06): the entries are bound by the Presentation module
    /// (<see cref="CarSeatSettings"/>, <c>[Presentation / Car seat]</c>) and the steering-wheel arms job is registered there; the partial methods
    /// are empty and the old statics forward until Wave C deletes them (BodyRunner reads them).
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        /// <summary>Forwarder to <see cref="CarSeatSettings.CarViewBase"/>.</summary>
        [Obsolete("refactor: use CarSeatSettings")] internal static Vector3 CarViewBase { get { return CarSeatSettings.CarViewBase; } }
        /// <summary>Forwarder to <see cref="CarSeatSettings.FpCarCamX"/>.</summary>
        [Obsolete("refactor: use CarSeatSettings")] internal static ConfigEntry<float> FpCarCamX { get { return CarSeatSettings.FpCarCamX; } }
        /// <summary>Forwarder to <see cref="CarSeatSettings.FpCarCamY"/>.</summary>
        [Obsolete("refactor: use CarSeatSettings")] internal static ConfigEntry<float> FpCarCamY { get { return CarSeatSettings.FpCarCamY; } }
        /// <summary>Forwarder to <see cref="CarSeatSettings.FpCarCamZ"/>.</summary>
        [Obsolete("refactor: use CarSeatSettings")] internal static ConfigEntry<float> FpCarCamZ { get { return CarSeatSettings.FpCarCamZ; } }
        /// <summary>Forwarder to <see cref="CarSeatSettings.CarCameraForward"/>.</summary>
        [Obsolete("refactor: use CarSeatSettings")] internal static ConfigEntry<float> CarCameraForward { get { return CarSeatSettings.CarCameraForward; } }
        /// <summary>Forwarder to <see cref="CarSeatSettings.SeatDrop"/>.</summary>
        [Obsolete("refactor: use CarSeatSettings")] internal static ConfigEntry<float> SeatDrop { get { return CarSeatSettings.SeatDrop; } }

        /// <summary>Old bind step ([PlayerBody / Car seat]); empty, Presentation binds <see cref="CarSeatSettings"/>.</summary>
        partial void BindSeatsTools(ModuleConfig cfg) { }
        /// <summary>Old init step (steering-wheel arms job); empty, Presentation registers it.</summary>
        partial void InitSeatsJobs() { }
    }
}
