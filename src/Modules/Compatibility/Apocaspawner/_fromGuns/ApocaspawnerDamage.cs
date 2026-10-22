using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    internal static class ApocaspawnerDamage
    {
        static FieldInfo _value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(IModule owner)
        {
            _value = null;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.denis.apocalypter.apocaspawner", out info) || ReferenceEquals(info.Instance, null)) return;
            var type = info.Instance.GetType().Assembly.GetType("Apocaspawner.Parameters");
            var method = type != null ? AccessTools.Method(type, "WeaponDamage") : null;
            _value = method != null ? AccessTools.Field(method.ReturnType, "Value") : null;
            if (_value == null || _value.FieldType != typeof(float)) { Core.Log.Warn("[Guns] Apocaspawner damage API unavailable."); return; }
            if (Core.Patches.Patch(owner, method, postfix: AccessTools.Method(typeof(ApocaspawnerDamage), nameof(AfterDamage))))
                Core.Log.Always("[Guns] Apocaspawner crossbow damage bridge installed.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void AfterDamage(GameObject go, object __result)
        {
            if (!GunsModule.ProjectilesEnabled || !GunsModule.PlayerTracers.Value || __result == null || go == null) return;
            if (!go.name.StartsWith("crossbow", StringComparison.OrdinalIgnoreCase)) return;
            _value.SetValue(__result, (float)_value.GetValue(__result) * Tracers.CrossbowDamageMultiplier);
        }
    }
}
