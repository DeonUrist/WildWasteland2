using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using NWH.WheelController3D;
using UnityEngine;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Part Adjustment bridge of the suspension tune: resolves PartAdjustment.SuspensionApi Set/Get by reflection once; Compatibility part (Wave A split of Patrols Vehicles/Suspension.cs).</summary>
    internal sealed class ExternalSuspension : Core.IExternalSuspension
    {
        /// <summary>Reports whether the optional suspension setter resolved.</summary>
        public bool CanSet { get { LookForApi();return _apiSet!=null; } }
        /// <summary>Reports whether the optional suspension getter resolved.</summary>
        public bool CanGet { get { LookForApi();return _apiGet!=null; } }
        /// <summary>Delegates actual adjustment to the external API; caller retains its fallback on an invocation failure.</summary>
        public bool Set(GameObject car,float width,float height) { return (bool)_apiSet.Invoke(null,new object[]{car,width,height}); }
        /// <summary>Reads actual external adjustments into a detached numeric pair.</summary>
        public bool Get(GameObject car,out float width,out float height)
        {
            var args=new object[]{car,1f,0f};bool ok=(bool)_apiGet.Invoke(null,args);width=(float)args[1];height=(float)args[2];return ok;
        }
        private bool _apiLooked; private MethodInfo _apiSet, _apiGet;

        /// <summary>Resolves PartAdjustment.SuspensionApi Set(car, width, height) / Get(car, out width, out height) once (assembly scan).</summary>
        private void LookForApi()
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
                if (Core.Log.L1) Core.Log.Main(CompatibilityModule.Instance, "Suspension: Part Adjustment " + (asm == null ? "not loaded - own geometry" : _apiSet != null ? "API found" : "loaded without SuspensionApi (older than 1.2.0) - own geometry"));
            }
            catch (Exception e) { Core.Log.Warn(CompatibilityModule.Instance, "Suspension: Part Adjustment API lookup: " + e.Message); }
        }
    }
}
