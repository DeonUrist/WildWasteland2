using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using NWH.WheelController3D;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Part Adjustment bridge of the suspension tune: resolves PartAdjustment.SuspensionApi Set/Get by reflection once; Compatibility part (Wave A split of Patrols Vehicles/Suspension.cs).</summary>
    internal static partial class Suspension
    {
        private static bool _apiLooked; private static MethodInfo _apiSet, _apiGet;

        /// <summary>Resolves PartAdjustment.SuspensionApi Set(car, width, height) / Get(car, out width, out height) once (assembly scan).</summary>
        private static void LookForApi()
        {
            if (_apiLooked) return;
            _apiLooked = true;
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "PartAdjustment");
                var t = asm != null ? asm.GetType("PartAdjustment.SuspensionApi") : null;
                if (t != null)
                {
                    _apiSet = t.GetMethod("Set", new[] { typeof(GameObject), typeof(float), typeof(float) });
                    _apiGet = t.GetMethod("Get", new[] { typeof(GameObject), typeof(float).MakeByRefType(), typeof(float).MakeByRefType() });
                }
                PatrolsModule.Verbose("Suspension: Part Adjustment " + (asm == null ? "not loaded - own geometry" : _apiSet != null ? "API found" : "loaded without SuspensionApi (older than 1.2.0) - own geometry"));
            }
            catch (Exception e) { Core.Log.Warn("[Patrols] " + "Suspension: API lookup: " + e.Message); }
        }
    }
}
