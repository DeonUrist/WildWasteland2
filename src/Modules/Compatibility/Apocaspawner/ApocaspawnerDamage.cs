using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Damage part of the Apocaspawner bridge: weapon stats include current player skill/perk effects and enabled projectile multipliers.
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
                Log.Main(scope, "Apocaspawner weapon damage bridge installed.");
        }

        /// <summary>Scales only the outer weapon query so recursive explosion/prefab reads cannot compound bonuses.</summary>
        static void AfterDamage(GameObject go, int depth, object __result)
        {
            if (_value == null || __result == null || go == null || depth != 0) return;
            var catalog = ModApi.Get<IWeaponCatalog>(); int key;
            if (catalog == null || !catalog.TryResolve(go, out key)) return;
            var kind = catalog.KindOf(key);
            float factor = (ModApi.Get<IPlayerSkills>()?.DamageMultiplier(kind, false) ?? 1f)
                * (ModApi.Get<IPlayerPerkEffects>()?.DamageMultiplier(kind, PlayerState.Health * .01f) ?? 1f);
            if ((ModApi.Get<IGunsApi>()?.PlayerProjectilesEnabled ?? false) && kind != WeaponKind.Melee && kind != WeaponKind.Thrown && kind != WeaponKind.Other)
                factor *= ModApi.Get<IWeaponStats>()?.Get(key, go.name, WeaponStat.DamageMultiplier) ?? 1f;
            if (factor != 1f) _value.SetValue(__result, (float)_value.GetValue(__result) * factor); // refs-ok: REFACTOR-REQUEST B17-6
        }

        /// <summary>Forgets the reflected field (the scope removes the patch).</summary>
        internal static void Shutdown() { _value = null; }
    }
}
