using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Storm part owned by Compatibility: the ApocaDustStorm (local.apocalypter.duststorm) reflection bridge; merges with the Patrols/WarRig DustStormBridge in Wave B (B17).</summary>
    internal static partial class Storm
    {
        // (1.9.3) ApocaDustStorm (local.apocalypter.duststorm), only when that mod is loaded: its worldwide dust storms count as sandstorms
        // (the same StormSight / StormHearing) wherever its dust is hazardous (intensity >= 0.35, where its player damage starts). It also
        // switches the game's own storms off but leaves them in ArrayList_Sandstorms, parked and invisible: a storm whose SandPlayer FSM is
        // disabled is skipped, funnels included (no "shoved by a tornado" from a storm that shoves nobody).
        private const string DustGuid = "local.apocalypter.duststorm";
        private const double DustHazard = 0.35;
        private static bool _dustLooked, _dust;
        private static Func<bool> _dustOn, _dustActive;
        private static Func<double, double, double> _dustIntensity;

        private static void LookForDust()
        {
            _dustLooked = true;
            try
            {
                BepInEx.PluginInfo info;
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(DustGuid, out info) || ReferenceEquals(info.Instance, null)) return;
                const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                Assembly asm = info.Instance.GetType().Assembly;
                Type plugin = asm.GetType("ApocaDustStorm.Plugin"), runner = asm.GetType("ApocaDustStorm.StormRunner"), model = asm.GetType("ApocaDustStorm.StormModel");
                PropertyInfo on = plugin != null ? plugin.GetProperty("Active", any) : null;
                FieldInfo field = runner != null ? runner.GetField("Model", any) : null;
                object m = field != null ? field.GetValue(null) : null;
                PropertyInfo active = model != null ? model.GetProperty("Active", any) : null;
                MethodInfo intensity = model != null ? model.GetMethod("Intensity", any, null, new[] { typeof(double), typeof(double) }, null) : null;
                if (on == null || m == null || active == null || intensity == null || intensity.ReturnType != typeof(double))
                { NpcModule.Log.LogWarning("Storm: ApocaDustStorm " + info.Metadata.Version + " found, but not the expected StormRunner.Model; its storms are not sandstorms for NPCs"); return; }
                _dustOn = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), on.GetGetMethod(true));
                _dustActive = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), m, active.GetGetMethod(true));
                _dustIntensity = (Func<double, double, double>)Delegate.CreateDelegate(typeof(Func<double, double, double>), m, intensity);
                NpcModule.Log.LogInfo("Storm: ApocaDustStorm " + info.Metadata.Version + " found: its dust storms dim NPC sight and hearing like the game's sandstorms");
            }
            catch (Exception e) { _dustOn = null; _dustActive = null; _dustIntensity = null; NpcModule.Log.LogWarning("Storm: ApocaDustStorm bridge off: " + e.Message); }
        }

        private static void DustOff(Exception e)
        {
            _dust = false; _dustOn = null; _dustActive = null; _dustIntensity = null;
            NpcModule.Log.LogWarning("Storm: ApocaDustStorm bridge off: " + e.Message);
        }
    }
}
