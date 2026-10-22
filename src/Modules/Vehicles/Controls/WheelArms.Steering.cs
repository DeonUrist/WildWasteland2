using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ES3Internal;
using NWH.VehiclePhysics2;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// WheelArms part (split from PlayerBody/Vehicles/WheelArms.cs in Wave A): the gameplay side of the steering wheel - NWH
    /// Steering.CalculateSteerAngles prefix that zeroes steering input while the removable wheel is missing. Target: Vehicles (B12).
    /// The forced Steering.returnToCenter (in WheelArms.Tick) also belongs here; it moves in Wave B.
    /// </summary>
    internal static partial class WheelArms
    {
        internal static bool MissingWheel { get { return _removableWheel && _mount != null && FittedWheel(_mount) == null; } }
        internal static void Patch(PlayerBodyModule owner)
        {
            Patches.Patch(owner, Patches.Method(typeof(Steering), "CalculateSteerAngles"), prefix: Patches.Method(typeof(WheelArms), "BeforeSteer"));
        }
        private static void BeforeSteer(Steering __instance)
        {
            if (!ReferenceEquals(__instance, _steering) || !MissingWheel) return;
            _vehicle.input.Steering = 0f; // Native steering still owns smoothing and centering, after all keyboard/mouse input writers.
        }
    }
}
