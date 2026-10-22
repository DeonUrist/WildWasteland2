using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>NPC storm-senses part of the ApocaDustStorm bridge (partial of the NPC/Survival Storm, which calls <see cref="LookForDust"/>
    /// and reads the delegates): ApocaDustStorm's dust storms count as sandstorms for NPC sight and hearing. Uses the bridge's shared
    /// detection, so it is off while [Compatibility / ApocaDustStorm] Enabled is off.</summary>
    internal static partial class Storm
    {
        // (1.9.3) ApocaDustStorm (local.apocalypter.duststorm), only when that mod is loaded: its worldwide dust storms count as sandstorms
        // (the same StormSight / StormHearing) wherever its dust is hazardous (intensity >= 0.35, where its player damage starts). It also
        // switches the game's own storms off but leaves them in ArrayList_Sandstorms, parked and invisible: a storm whose SandPlayer FSM is
        // disabled is skipped, funnels included (no "shoved by a tornado" from a storm that shoves nobody).
        private const double DustHazard = 0.35;
        private static bool _dustLooked, _dust;
        private static Func<bool> _dustOn, _dustActive;
        private static Func<double, double, double> _dustIntensity;

        /// <summary>Resolves ApocaDustStorm's API by reflection once (only when that mod is loaded).</summary>
        private static void LookForDust()
        {
            _dustLooked = Compatibility.ApocaDustStormBridge.Enabled;   // retried while the bridge is off (a dictionary lookup per refresh)
            if (!_dustLooked) return;
            try
            {
                var info = Compatibility.ApocaDustStormBridge.Plugin;
                if (info == null) return;
                const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                Assembly asm = info.Instance.GetType().Assembly;
                Type plugin = asm.GetType("ApocaDustStorm.Plugin"), runner = asm.GetType("ApocaDustStorm.StormRunner"), model = asm.GetType("ApocaDustStorm.StormModel");
                PropertyInfo on = plugin != null ? plugin.GetProperty("Active", any) : null;
                FieldInfo field = runner != null ? runner.GetField("Model", any) : null;
                object m = field != null ? field.GetValue(null) : null;
                PropertyInfo active = model != null ? model.GetProperty("Active", any) : null;
                MethodInfo intensity = model != null ? model.GetMethod("Intensity", any, null, new[] { typeof(double), typeof(double) }, null) : null;
                if (on == null || m == null || active == null || intensity == null || intensity.ReturnType != typeof(double))
                { Core.Log.Warn((Core.IModule)null, "Storm: ApocaDustStorm " + info.Metadata.Version + " found, but not the expected StormRunner.Model; its storms are not sandstorms for NPCs"); return; }
                _dustOn = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), on.GetGetMethod(true));
                _dustActive = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), m, active.GetGetMethod(true));
                _dustIntensity = (Func<double, double, double>)Delegate.CreateDelegate(typeof(Func<double, double, double>), m, intensity);
                Core.Log.Main((Core.IModule)null, "Storm: ApocaDustStorm " + info.Metadata.Version + " found: its dust storms dim NPC sight and hearing like the game's sandstorms");
            }
            catch (Exception e) { _dustOn = null; _dustActive = null; _dustIntensity = null; Core.Log.Warn((Core.IModule)null, "Storm: ApocaDustStorm bridge off: " + e.Message); }
        }

        /// <summary>The ApocaDustStorm bridge stopped: dust no longer counts as a sandstorm; it re-resolves when switched on.</summary>
        internal static void DustBridgeOff() { _dust = false; _dustOn = null; _dustActive = null; _dustIntensity = null; _dustLooked = false; }

        /// <summary>Switches the dust bridge off after a reflection failure (logged once).</summary>
        private static void DustOff(Exception e)
        {
            _dust = false; _dustOn = null; _dustActive = null; _dustIntensity = null;
            Core.Log.Warn((Core.IModule)null, "Storm: ApocaDustStorm bridge off: " + e.Message);
        }
    }
}
