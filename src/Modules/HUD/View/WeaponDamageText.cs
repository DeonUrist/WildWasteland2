using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

namespace WildWasteland.Modules.HUD
{
    [HarmonyPatch(typeof(UiTextSetText), "OnEnter")]
    internal static class WeaponDamageText
    {
        static readonly FsmString Damage = new FsmString { Value = "Damage - 50" };
        static readonly FsmString BuyDamage = new FsmString { Value = "Crossbow / Damage - 50" };

        /// <summary>Observes the native UI action and applies this feature only when its exact target matches.</summary>
        static void Prefix(UiTextSetText __instance, out FsmString __state)
        {
            __state = null;
            var guns = WildWasteland.Core.ModApi.Get<WildWasteland.Core.IGunsApi>();
            if (guns == null || !guns.PlayerProjectilesEnabled) return;
            var fsm = __instance.Fsm;
            if (fsm == null || (fsm.Name != "ItemName" && fsm.Name != "Buyable")) return;
            var owner = fsm.GameObject;
            if (owner == null || !owner.name.StartsWith("crossbow", StringComparison.OrdinalIgnoreCase)) return;
            var text = __instance.text;
            if (text == null || text.UseVariable) return;
            if (text.Value == "Damage - 20") { __state = text; __instance.text = Damage; }
            else if (text.Value == "Crossbow / Damage - 20") { __state = text; __instance.text = BuyDamage; }
        }

        /// <summary>Restores the original native action text after the temporary tooltip replacement.</summary>
        static void Finalizer(UiTextSetText __instance, FsmString __state)
        {
            if (__state != null) __instance.text = __state;
        }
    }
}
