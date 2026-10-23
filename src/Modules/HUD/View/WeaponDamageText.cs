using System;
using System.Globalization;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    [HarmonyPatch(typeof(UiTextSetText), "OnEnter")]
    internal static class WeaponDamageText
    {
        static readonly FsmString Damage = new FsmString();

        /// <summary>Observes the native UI action and applies this feature only when its exact target matches.</summary>
        static void Prefix(UiTextSetText __instance, out FsmString __state)
        {
            __state = null;
            var fsm = __instance.Fsm;
            if (fsm == null || (fsm.Name != "ItemName" && fsm.Name != "Buyable")) return;
            var owner = fsm.GameObject;
            var text = __instance.text;
            if (owner == null || text == null || text.UseVariable || string.IsNullOrEmpty(text.Value)) return;
            int start = text.Value.IndexOf("Damage - ", StringComparison.Ordinal);
            float value;
            if (start < 0 || !float.TryParse(text.Value.Substring(start + 9), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return;
            var catalog = ModApi.Get<IWeaponCatalog>(); int key;
            if (catalog == null || !catalog.TryResolve(owner, out key)) return;
            var kind = catalog.KindOf(key);
            float original = value, native; int pellets;
            if (NativeDamage(owner, catalog, out native, out pellets)) value = native; else pellets = 1;
            float factor = (ModApi.Get<IPlayerSkills>()?.DamageMultiplier(kind, false) ?? 1f)
                * (ModApi.Get<IPlayerPerkEffects>()?.DamageMultiplier(kind, PlayerState.Health * .01f) ?? 1f);
            if ((ModApi.Get<IGunsApi>()?.PlayerProjectilesEnabled ?? false) && kind != WeaponKind.Melee && kind != WeaponKind.Thrown && kind != WeaponKind.Other)
            {
                factor *= ModApi.Get<IWeaponStats>()?.Get(key, owner.name, WeaponStat.DamageMultiplier) ?? 1f;
                float count = ModApi.Get<IWeaponStats>()?.Get(key, owner.name, WeaponStat.Pellets) ?? 0;
                if (pellets > 1 && count > 0) pellets = Mathf.Clamp(Mathf.RoundToInt(count), 1, 32);
            }
            if (factor == 1f && value == original && pellets == 1) return;
            Damage.Value = text.Value.Substring(0, start + 9) + (value * factor).ToString("0.##", CultureInfo.InvariantCulture)
                + (pellets > 1 ? " x " + pellets + " pellets" : "");
            __state = text; __instance.text = Damage;
        }

        static bool NativeDamage(GameObject owner, IWeaponCatalog catalog, out float damage, out int pellets)
        {
            damage = 0; pellets = 1;
            var attack = FsmUtil.Find(owner, "Attack");
            if (attack == null)
            {
                var typeFsm = FsmUtil.Find(owner, "weaponType");
                var type = typeFsm != null ? typeFsm.FsmVariables.FindFsmString("weaponType") : null;
                var parent = PlayerState.WeaponsParent;
                var model = parent != null && type != null && !string.IsNullOrEmpty(type.Value) ? parent.Find(catalog.NormalizeKey(type.Value)) : null;
                attack = model != null ? FsmUtil.Find(model.gameObject, "Attack") : null;
            }
            if (attack == null || attack.Fsm == null) return false;
            if (!attack.Fsm.Initialized) attack.Fsm.InitData();
            var hit = attack.Fsm.GetState("hit");
            if (hit == null || hit.Actions == null) return false;
            foreach (var action in hit.Actions)
            {
                var set = action as SetFsmFloat;
                if (set != null && set.fsmName != null && set.fsmName.Value == "Bodypart" && set.variableName != null && set.variableName.Value == "Damage" && set.setValue != null) damage = Mathf.Abs(set.setValue.Value);
                var count = action as IntCompare;
                if (count != null && count.integer1 != null && count.integer1.Name == "pellets" && count.integer2 != null) pellets = Mathf.Clamp(count.integer2.Value + 1, 1, 32);
            }
            return damage > 0;
        }

        /// <summary>Restores the original native action text after the temporary tooltip replacement.</summary>
        static void Finalizer(UiTextSetText __instance, FsmString __state)
        {
            if (__state != null) __instance.text = __state;
        }
    }
}
