using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Captures native engine values once and restores them on release or owner teardown.</summary>
    internal sealed class VehicleEngineHeat : IVehicleEngineHeat
    {
        internal static readonly VehicleEngineHeat Instance = new VehicleEngineHeat();
        private readonly Dictionary<GameObject, Dictionary<FsmFloat,float>> _held = new Dictionary<GameObject, Dictionary<FsmFloat,float>>();
        /// <summary>Applies the caller's traffic policy without retaining traffic actors or settings.</summary>
        public void Hold(GameObject car, bool cool)
        {
            if (car == null) return;
            Dictionary<FsmFloat,float> values;
            if (!cool)
            {
                if (_held.TryGetValue(car,out values)) { Restore(values); _held.Remove(car); }
                return;
            }
            if (!_held.TryGetValue(car,out values)) _held[car] = values = new Dictionary<FsmFloat,float>();
            try
            {
                foreach(var f in car.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if(f==null || f.FsmName!="heatValue") continue;
                    var v=f.FsmVariables.GetFsmFloat("heatValue");
                    if(v==null) continue;
                    if(!values.ContainsKey(v)) values.Add(v,v.Value);
                    v.Value=0f;
                }
            }
            catch(Exception e) { Log.Warn(VehiclesModule.Instance,"Engine heat: "+e.Message); }
        }
        /// <summary>Returns captured values before the engine can pass to player or another owner.</summary>
        private static void Restore(Dictionary<FsmFloat,float> values)
        {
            foreach(var kv in values) if(kv.Key!=null) kv.Key.Value=kv.Value;
        }
        /// <summary>Restores every held engine when Vehicles stops or a scene is discarded.</summary>
        internal static void ReleaseAll()
        {
            foreach(var values in Instance._held.Values) Restore(values);
            Instance._held.Clear();
        }
    }
}
