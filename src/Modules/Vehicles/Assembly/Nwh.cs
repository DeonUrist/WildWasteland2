using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ES3Internal;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// NWH Vehicle Physics 2 access for driven cars: typed where the NWH types are known, reflection as the fallback. The VehicleController
    /// and its input / powertrain / engine / transmission objects are stable for the life of the car, so a Handle per car and a MemberInfo
    /// table per (type, name) are cached. The pilots ask several times per physics step.
    /// </summary>
    internal static class Nwh
    {
        /// <summary>Cached controller objects of one car and its last written input.</summary>
        private class Handle
        {
            public Component Vc;
            public object Input, Engine, Transmission;
            // Perf: typed NWH.VehiclePhysics2 access (same members as the reflection path; null = fall back to reflection)
            public NWH.VehiclePhysics2.Input.VehicleInputHandler TInput; public NWH.VehiclePhysics2.Powertrain.EngineComponent TEngine;
            public float Throttle = float.NaN, Steering = float.NaN, Brakes = float.NaN, WrittenAt = -1f;   // last SetInput, to skip identical writes
        }

        private const float InputRefresh = 0.5f;   // identical inputs are still re-written this often (a stopped or idle car writes 3 boxed floats per step otherwise)

        private static readonly Dictionary<int, Handle> _handles = new Dictionary<int, Handle>();
        private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> _members = new Dictionary<Type, Dictionary<string, MemberInfo>>();
        private static float _nextSweep;

        /// <summary>The cached handle of a car (created on first use; destroyed cars are swept every 30 s); null without a VehicleController.</summary>
        private static Handle Of(GameObject car)
        {
            if (car == null) return null;
            if (Time.unscaledTime >= _nextSweep) { _nextSweep = Time.unscaledTime + 30f; Sweep(); }
            Handle h;
            int id = car.GetInstanceID();
            if (_handles.TryGetValue(id, out h) && h.Vc != null) return h;
            h = new Handle();
            foreach (var c in car.GetComponents<Component>())
                if (c != null && c.GetType().Name == "VehicleController") { h.Vc = c; break; }
            if (h.Vc == null) return null;
            h.Input = Get(h.Vc, "input");
            var pt = Get(h.Vc, "powertrain");
            h.Engine = Get(pt, "engine");
            h.Transmission = Get(pt, "transmission");
            h.TInput = h.Input as NWH.VehiclePhysics2.Input.VehicleInputHandler; h.TEngine = h.Engine as NWH.VehiclePhysics2.Powertrain.EngineComponent;
            _handles[id] = h;
            return h;
        }

        // drop the handles of destroyed cars
        /// <summary>Drops the handles of destroyed cars.</summary>
        private static void Sweep()
        {
            var dead = new List<int>();
            foreach (var kv in _handles) if (kv.Value.Vc == null) dead.Add(kv.Key);
            foreach (var k in dead) _handles.Remove(k);
        }

        /// <summary>Cached field-or-property lookup per (type, name).</summary>
        private static MemberInfo Member(Type t, string name)
        {
            Dictionary<string, MemberInfo> d;
            if (!_members.TryGetValue(t, out d)) _members[t] = d = new Dictionary<string, MemberInfo>();
            MemberInfo m;
            if (d.TryGetValue(name, out m)) return m;
            m = (MemberInfo)t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            d[name] = m;
            return m;
        }

        /// <summary>Reads a field or property by name (null when missing).</summary>
        private static object Get(object o, string name)
        {
            if (o == null) return null;
            var m = Member(o.GetType(), name);
            var f = m as FieldInfo;
            if (f != null) return f.GetValue(o);
            var p = m as PropertyInfo;
            return p != null ? p.GetValue(o, null) : null;
        }

        /// <summary>Writes a field or writable property by name; false when missing.</summary>
        private static bool Set(object o, string name, object value)
        {
            if (o == null) return false;
            var m = Member(o.GetType(), name);
            var f = m as FieldInfo;
            if (f != null) { f.SetValue(o, value); return true; }
            var p = m as PropertyInfo;
            if (p != null && p.CanWrite) { p.SetValue(o, value, null); return true; }
            return false;
        }

        /// <summary>Engine running: the ignition provider first, then the NWH engine.</summary>
        internal static bool EngineRunning(GameObject car)
        {
            bool running; var ignition = ModApi.Get<IVehicleIgnition>(); if (ignition != null && ignition.TryRunning(car, out running)) return running;
            try { var h = Of(car); if (h != null && h.TEngine != null) return h.TEngine.IsRunning; var r = h != null ? Get(h.Engine, "IsRunning") : null; return r is bool && (bool)r; }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "EngineRunning: " + e.Message); return false; }
        }

        /// <summary>Starts the engine: the ignition provider first, then NWH StartEngine().</summary>
        internal static void StartEngine(GameObject car)
        {
            var ignition = ModApi.Get<IVehicleIgnition>(); if (ignition != null && ignition.TryStart(car)) return;
            try
            {
                var h = Of(car);
                var eng = h != null ? h.Engine : null;
                if (eng == null) { Log.Warn(VehiclesModule.Instance, "No powertrain.engine"); return; }
                if (h.TEngine != null) { h.TEngine.StartEngine(); return; }
                var m = eng.GetType().GetMethod("StartEngine", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (m == null) { Log.Warn(VehiclesModule.Instance, "No StartEngine()"); return; }
                m.Invoke(eng, null);
            }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "StartEngine: " + e.Message); }
        }

        /// <summary>Stops the engine: the ignition provider first, then NWH StopEngine().</summary>
        internal static void StopEngine(GameObject car)
        {
            var ignition = ModApi.Get<IVehicleIgnition>(); if (ignition != null && ignition.TryStop(car)) return;
            try
            {
                var h = Of(car);
                var eng = h != null ? h.Engine : null;
                if (eng == null) return;
                if (h.TEngine != null) { h.TEngine.StopEngine(); return; }
                var m = eng.GetType().GetMethod("StopEngine", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (m == null) { Log.Deep(VehiclesModule.Instance, "No StopEngine()"); return; }
                m.Invoke(eng, null);
            }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "StopEngine: " + e.Message); }
        }

        // returns the previous value
        /// <summary>Sets input.autoSetInput; returns the previous value.</summary>
        internal static bool AutoInput(GameObject car, bool on)
        {
            try
            {
                var h = Of(car);
                var input = h != null ? h.Input : null;
                if (h != null && h.TInput != null) { bool old = h.TInput.autoSetInput; h.TInput.autoSetInput = on; return old; }
                var was = Get(input, "autoSetInput");
                if (!Set(input, "autoSetInput", on)) Log.Deep(VehiclesModule.Instance, "no autoSetInput");
                return was is bool ? (bool)was : false;
            }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "AutoInput: " + e.Message); return false; }
        }

        /// <summary>Current transmission gear index (0 when unknown).</summary>
        internal static int GearIndex(GameObject car)
        {
            try { var h = Of(car); var g = h != null ? Get(h.Transmission, "Gear") : null; return g is int ? (int)g : 0; }
            catch (Exception) { return 0; }
        }

        // input.ShiftInto = gear index (-1 R, 0 N, 1 = 1st/D); the vehicle consumes it on its next update
        /// <summary>Requests a gear (input.ShiftInto: -1 R, 0 N, 1 = 1st/D), consumed on the vehicle's next update.</summary>
        internal static void ShiftInto(GameObject car, int gear)
        {
            try
            {
                var h = Of(car);
                if (h == null) return;
                if (h.TInput != null) { h.TInput.ShiftInto = gear; return; }
                if (!Set(h.Input, "ShiftInto", gear)) Set(h.Input, "shiftInto", gear);
            }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "ShiftInto: " + e.Message); }
        }

        // one-line NWH state readback for the log
        /// <summary>One-line NWH state readback for the log.</summary>
        internal static string Diag(GameObject car)
        {
            try
            {
                var h = Of(car);
                if (h == null) return "diag: no VehicleController";
                return "thr=" + Get(h.Input, "Throttle") + " clutch=" + Get(h.Input, "Clutch") + " hb=" + Get(h.Input, "Handbrake")
                     + " rpm=" + F(Get(h.Engine, "RPM") ?? Get(h.Engine, "OutputRPM")) + " trType=" + Get(h.Transmission, "transmissionType")
                     + " active=" + (Get(h.Vc, "IsActive") ?? Get(h.Vc, "Active")) + " enabled=" + ((Behaviour)h.Vc).enabled;
            }
            catch (Exception e) { return "diag: " + e.Message; }
        }

        /// <summary>Formats an rpm value for the log.</summary>
        private static string F(object o) { return o is float ? ((float)o).ToString("0") : (o == null ? "?" : o.ToString()); }

        /// <summary>Gear label for the log.</summary>
        internal static string Gear(GameObject car)
        {
            try { var h = Of(car); var g = h != null ? Get(h.Transmission, "Gear") : null; return g != null ? g.ToString() : "?"; }
            catch (Exception) { return "?"; }
        }

        // NWH steering.maximumSteerAngle (degrees), 0 when unknown
        /// <summary>NWH steering.maximumSteerAngle in degrees (0 when unknown).</summary>
        internal static float MaxSteerAngle(GameObject car)
        {
            try { var h = Of(car); var s = h != null ? Get(h.Vc, "steering") : null; var a = s != null ? Get(s, "maximumSteerAngle") : null; return a is float ? (float)a : 0f; }
            catch (Exception) { return 0f; }
        }

        /// <summary>Writes input.Handbrake.</summary>
        internal static void SetHandbrake(GameObject car, float value)
        {
            try { var h = Of(car); if (h != null && h.TInput != null) { h.TInput.Handbrake = value; return; } if (h == null || !Set(h.Input, "Handbrake", value)) Log.Deep(VehiclesModule.Instance, "no input.Handbrake"); }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "SetHandbrake: " + e.Message); }
        }

        /// <summary>Writes throttle / steering / brakes, skipping identical writes for up to 0.5 s.</summary>
        internal static void SetInput(GameObject car, float throttle, float steering, float brakes)
        {
            try
            {
                var h = Of(car);
                if (h == null || h.Input == null) { Log.Warn(VehiclesModule.Instance, "No VehicleController.input"); return; }
                if (h.Throttle == throttle && h.Steering == steering && h.Brakes == brakes && Time.fixedTime - h.WrittenAt < InputRefresh) return;
                h.Throttle = throttle; h.Steering = steering; h.Brakes = brakes; h.WrittenAt = Time.fixedTime;
                if (h.TInput != null) { h.TInput.Throttle = throttle; h.TInput.Steering = steering; h.TInput.Brakes = brakes; return; }
                Set(h.Input, "Throttle", throttle);
                Set(h.Input, "Steering", steering);
                Set(h.Input, "Brakes", brakes);
            }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "SetInput: " + e.Message); }
        }

        // the next SetInput writes whatever it is given (after the game's INPUT FSMs had the input for a while)
        /// <summary>Makes the next SetInput write whatever it gets (after the game's INPUT FSMs had the input).</summary>
        internal static void ForgetInput(GameObject car)
        {
            var h = Of(car);
            if (h != null) { h.Throttle = h.Steering = h.Brakes = float.NaN; h.WrittenAt = -1f; }
        }
    }
}
