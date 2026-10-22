using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// Damage part of the Apocaspawner bridge: Apocaspawner's crossbow WeaponDamage result is scaled by the Gunplay crossbow multiplier
    /// while player shots use Gunplay projectiles (IGunsApi.PlayerProjectilesEnabled).
    /// </summary>
    internal static class ApocaspawnerDamage
    {
        static FieldInfo _value;

        /// <summary>Resolves Apocaspawner.Parameters.WeaponDamage and its result's Value field, then postfixes it under <paramref name="scope"/>.</summary>
        internal static void Install(IModule scope, Assembly asm)
        {
            _value = null;
            var type = asm.GetType("Apocaspawner.Parameters");
            var method = type != null ? AccessTools.Method(type, "WeaponDamage") : null;
            _value = method != null ? AccessTools.Field(method.ReturnType, "Value") : null;
            if (_value == null || _value.FieldType != typeof(float)) { _value = null; Log.Warn(scope, "Apocaspawner damage API unavailable."); return; }
            if (Core.Patches.Patch(scope, method, postfix: AccessTools.Method(typeof(ApocaspawnerDamage), nameof(AfterDamage))))
                Log.Main(scope, "Apocaspawner crossbow damage bridge installed.");
        }

        /// <summary>Postfix of Parameters.WeaponDamage (per damage query; early-out unless Gunplay player projectiles and a crossbow).</summary>
        static void AfterDamage(GameObject go, object __result)
        {
            if (_value == null || __result == null || go == null) return;
            var guns = ModApi.Get<IGunsApi>();
            if (guns == null || !guns.PlayerProjectilesEnabled) return;
            if (!go.name.StartsWith("crossbow", StringComparison.OrdinalIgnoreCase)) return;
            _value.SetValue(__result, (float)_value.GetValue(__result) * Tracers.CrossbowDamageMultiplier); // refs-ok: REFACTOR-REQUEST B17-6
        }

        /// <summary>Forgets the reflected field (the scope removes the patch).</summary>
        internal static void Shutdown() { _value = null; }
    }
}
