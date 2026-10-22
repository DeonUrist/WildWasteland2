using System;
using System.Collections.Generic;
using NWH.VehiclePhysics2;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Shared native NWH engine reads with optional typed ignition overrides.</summary>
    public static class VehicleEngine
    {
        /// <summary>Exact native controller identity retained for a live vehicle.</summary>
        sealed class Handles { internal GameObject Car; internal VehicleController Controller; }
        static readonly Dictionary<int, Handles> Cars = new Dictionary<int, Handles>();
        /// <summary>Releases scene-owned controller handles.</summary>
        public static void Reset() { Cars.Clear(); }
        /// <summary>Finds the typed controller and retries absent or destroyed components on the next read.</summary>
        static VehicleController Of(GameObject car)
        {
            if (car == null) return null;
            int id = car.GetInstanceID(); Handles handles;
            if (Cars.TryGetValue(id, out handles) && handles.Car == car && handles.Controller != null) return handles.Controller;
            var controller = car.GetComponent<VehicleController>();
            if (controller != null) Cars[id] = new Handles { Car = car, Controller = controller };
            else Cars.Remove(id);
            return controller;
        }
        /// <summary>Uses the compatibility ignition override first, otherwise reads the current native engine state.</summary>
        public static bool Running(GameObject car)
        {
            try
            {
                var ignition = ModApi.Get<IVehicleIgnition>(); bool running;
                if (ignition != null && ignition.TryRunning(car, out running)) return running;
                var controller = Of(car);
                return controller != null && controller.powertrain != null && controller.powertrain.engine != null && controller.powertrain.engine.IsRunning;
            }
            catch (Exception) { return false; }
        }
        /// <summary>Reads current native input without boxing or retaining an incompletely initialized input object.</summary>
        public static float Throttle(GameObject car)
        {
            try { var controller = Of(car); return controller != null && controller.input != null ? controller.input.Throttle : 0f; }
            catch (Exception) { return 0f; }
        }
    }
}
