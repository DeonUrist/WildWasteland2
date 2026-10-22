using System;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Vistas World Gen part serving the HUD: the mod's engine-heat overlay is replaced by HUD notifications ("OVERHEATING, POWER CUT",
    /// "ENGINE SEIZED, LET IT COOL") while the HUD (CleanHUD) runs; temperature stays on the gauges. Prefix of the mod's heat HUD draw.
    /// </summary>
    internal static class VistasEngineHud
    {
        private static Func<object, object> _active;
        private static Func<object, float> _temperature;
        private static Func<object, bool> _stalled;
        private static object _enabled, _last;
        private static int _state;
        /// <summary>Resolves the mod's EngineHeat state and patches its heat HUD draw under <paramref name="owner"/>.</summary>
        internal static void Install(IModule owner)
        {
            Clear(); BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("apocalypter.worldamp", out info) || ReferenceEquals(info.Instance, null)) return;
            try
            {
                var asm = info.Instance.GetType().Assembly; var heat = asm.GetType("ApocalpyterWorldAmp.EngineHeat", true);
                _active = Access.Read<object>(AccessTools.Field(heat, "Active"));
                _temperature = Access.Call<Func<object, float>>(AccessTools.PropertyGetter(heat, "Temperature"), typeof(float), typeof(object));
                _stalled = Access.Call<Func<object, bool>>(AccessTools.PropertyGetter(heat, "Stalled"), typeof(bool), typeof(object));
                ConfigEntry<bool> enabled;
                if (!info.Instance.Config.TryGetEntry<bool>(new ConfigDefinition("Vehicles", "EngineHeat"), out enabled)) throw new MissingMemberException("Vehicles.EngineHeat");
                _enabled = enabled;
                var draw = AccessTools.Method(asm.GetType("a.u", true), "A", Type.EmptyTypes);
                if (draw == null || !draw.IsStatic || draw.ReturnType != typeof(void) || !Core.Patches.Patch(owner, draw, prefix: AccessTools.Method(typeof(VistasEngineHud), "Draw"))) throw new MissingMethodException("Engine heat HUD");
                Log.Main(owner, "World Amp engine warnings use HUD notifications; temperature remains on gauges");
            }
            catch (Exception e) { Clear(); Log.Warn(owner, "World Amp engine HUD bridge unavailable: " + e.Message); }
        }
        /// <summary>Prefix of the mod's engine-heat HUD draw (every GUI frame): pushes a HUD notice on a new warning state and skips the original.</summary>
        private static bool Draw()
        {
            if (ModApi.Get<INotifications>()?.Enabled!=true || _active == null) { _last = null; _state = 0; return true; } // refs-ok: REFACTOR-REQUEST B17-16
            var active = _active(null) as UnityEngine.Object;
            int state = active != null && ((ConfigEntry<bool>)_enabled).Value ? (_stalled(active) ? 2 : _temperature(active) >= 105f ? 1 : 0) : 0;
            if (!ReferenceEquals(_last, active)) { _last = active; _state = 0; }
            if (!GameRefs.InGame) return false; // refs-ok: REFACTOR-REQUEST B17-16
            if (state != _state && state != 0 && ModApi.Get<INotifications>()?.Push( // refs-ok: REFACTOR-REQUEST B17-16
                state == 2 ? "ENGINE SEIZED, LET IT COOL" : "OVERHEATING, POWER CUT",NotificationKind.Warning,0f)!=true)return false;
            _state = state; return false;
        }
        /// <summary>Forgets the mod's members (the owner removes the patch).</summary>
        internal static void Clear() { _active = null; _temperature = null; _stalled = null; _enabled = _last = null; _state = 0; }
    }
}
