using System;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class VistasEngineHud
    {
        private static Func<object, object> _active;
        private static Func<object, float> _temperature;
        private static Func<object, bool> _stalled;
        private static object _enabled, _last;
        private static int _state;
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(CleanHudModule owner)
        {
            Clear(); BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("apocalypter.worldamp", out info) || ReferenceEquals(info.Instance, null)) return;
            try
            {
                var asm = info.Instance.GetType().Assembly; var heat = asm.GetType("ApocalpyterWorldAmp.EngineHeat", true);
                _active = OptionalAccess.Read<object>(AccessTools.Field(heat, "Active"));
                _temperature = OptionalAccess.Call<Func<object, float>>(AccessTools.PropertyGetter(heat, "Temperature"), typeof(float), typeof(object));
                _stalled = OptionalAccess.Call<Func<object, bool>>(AccessTools.PropertyGetter(heat, "Stalled"), typeof(bool), typeof(object));
                ConfigEntry<bool> enabled;
                if (!info.Instance.Config.TryGetEntry<bool>(new ConfigDefinition("Vehicles", "EngineHeat"), out enabled)) throw new MissingMemberException("Vehicles.EngineHeat");
                _enabled = enabled;
                var draw = AccessTools.Method(asm.GetType("a.u", true), "A", Type.EmptyTypes);
                if (draw == null || !draw.IsStatic || draw.ReturnType != typeof(void) || !Core.Patches.Patch(owner, draw, prefix: AccessTools.Method(typeof(VistasEngineHud), "Draw"))) throw new MissingMethodException("Engine heat HUD");
                CleanHudModule.Log("World Amp engine warnings use CleanHUD notifications; temperature remains on gauges");
            }
            catch (Exception e) { Clear(); CleanHudModule.Warn("World Amp engine HUD bridge unavailable: " + e.Message); }
        }
        private static bool Draw()
        {
            if (!CleanHudModule.Running || !CleanHudModule.Enabled.Value || _active == null) { _last = null; _state = 0; return true; }
            var active = _active(null) as UnityEngine.Object;
            int state = active != null && ((ConfigEntry<bool>)_enabled).Value ? (_stalled(active) ? 2 : _temperature(active) >= 105f ? 1 : 0) : 0;
            if (!ReferenceEquals(_last, active)) { _last = active; _state = 0; }
            if (!GameRefs.InGame || !MessageHook.Active) return false;
            if (state != _state && state != 0) MessageHook.Push(state == 2 ? "ENGINE SEIZED, LET IT COOL" : "OVERHEATING, POWER CUT");
            _state = state; return false;
        }
        internal static void Clear() { _active = null; _temperature = null; _stalled = null; _enabled = _last = null; _state = 0; }
    }
}
