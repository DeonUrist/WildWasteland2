using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Senses part owned by Core: NWH vehicle engine reflection (running / throttle), to be replaced by Services.VehicleIgnition in Wave B.</summary>
    internal static partial class Senses
    {
        private static class Nwh
        {
            private static readonly Dictionary<int, object[]> _h = new Dictionary<int, object[]>();   // car id -> { engine, input }
            /// <summary>Forgets the cached NWH handles (scene load).</summary>
            internal static void Clear() { _h.Clear(); }
            /// <summary>Cached [controller, engine, input] handles of a car (reflection, looked up once per car).</summary>
            private static object[] Of(GameObject car)
            {
                object[] h;
                if (_h.TryGetValue(car.GetInstanceID(), out h)) return h;
                object vc = null;
                foreach (var c in car.GetComponents<Component>()) if (c != null && c.GetType().Name == "VehicleController") { vc = c; break; }
                if (vc == null) return null;
                object pt = Read(vc, "powertrain");
                h = new object[] { Read(pt, "engine"), Read(vc, "input") };
                _h[car.GetInstanceID()] = h;
                return h;
            }
            /// <summary>Reads a field or property by name (reflection).</summary>
            private static object Read(object o, string name)
            {
                if (o == null) return null;
                var t = o.GetType();
                var f = Field(t, name); if (f != null) return f.GetValue(o);
                for (var c = t; c != null; c = c.BaseType) { var p = c.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); if (p != null) return p.GetValue(o, null); }
                return null;
            }
            /// <summary>True when the car's NWH engine is running.</summary>
            internal static bool EngineRunning(GameObject car)
            {
                try { var h = Of(car); var r = h != null ? Read(h[0], "IsRunning") : null; return r is bool && (bool)r; } catch (Exception) { return false; }
            }
            /// <summary>The car's NWH throttle input (0..1).</summary>
            internal static float Throttle(GameObject car)
            {
                try { var h = Of(car); var r = h != null ? Read(h[1], "Throttle") : null; return r is float ? (float)r : 0f; } catch (Exception) { return 0f; }
            }
        }
    }
}
