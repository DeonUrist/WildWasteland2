using System;
using System.Collections.Generic;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Shared native NWH engine reads with optional typed ignition overrides.</summary>
    public static class VehicleEngine
    {
        /// <summary>Cached native engine and input handles tied to the exact vehicle object.</summary>
        sealed class Handles { public GameObject Car; public object Engine, Input; }
        static readonly Dictionary<int, Handles> Cars = new Dictionary<int, Handles>();
        static readonly Dictionary<Type, Dictionary<string, Func<object, object>>> Getters = new Dictionary<Type, Dictionary<string, Func<object, object>>>();
        /// <summary>Releases scene-owned vehicle handles; generated accessors remain reusable across scenes.</summary>
        public static void Reset() { Cars.Clear(); }
        /// <summary>Reads a reflected native member through a once-generated Core accessor.</summary>
        static object Read(object instance, string name)
        {
            if (instance == null) return null;
            var type = instance.GetType(); Dictionary<string, Func<object, object>> fields;
            if (!Getters.TryGetValue(type, out fields)) Getters[type] = fields = new Dictionary<string, Func<object, object>>();
            Func<object, object> get;
            if (!fields.TryGetValue(name, out get))
            {
                try { get = Access.Get<object>(type, name); } catch (Exception) { get = null; }
                fields[name] = get;
            }
            return get != null ? get(instance) : null;
        }
        /// <summary>Resolves native engine/input handles once per live vehicle instance.</summary>
        static Handles Of(GameObject car)
        {
            if (car == null) return null;
            Handles handles;
            if (Cars.TryGetValue(car.GetInstanceID(), out handles) && handles.Car == car) return handles;
            foreach (var component in car.GetComponents<Component>())
            {
                if (component == null || component.GetType().Name != "VehicleController") continue;
                var powertrain = Read(component, "powertrain");
                handles = new Handles { Car = car, Engine = Read(powertrain, "engine"), Input = Read(component, "input") };
                Cars[car.GetInstanceID()] = handles; return handles;
            }
            return null;
        }
        /// <summary>Compatibility running state when available, otherwise the native NWH IsRunning property.</summary>
        public static bool Running(GameObject car)
        {
            try
            {
                var ignition = ModApi.Get<IVehicleIgnition>(); bool running;
                if (ignition != null && ignition.TryRunning(car, out running)) return running;
                var handles = Of(car); var value = handles != null ? Read(handles.Engine, "IsRunning") : null;
                return value is bool && (bool)value;
            }
            catch (Exception) { return false; }
        }
        /// <summary>Native throttle input, or zero when no live controller is available.</summary>
        public static float Throttle(GameObject car)
        {
            try { var handles = Of(car); var value = handles != null ? Read(handles.Input, "Throttle") : null; return value is float ? (float)value : 0f; }
            catch (Exception) { return 0f; }
        }
    }
}
