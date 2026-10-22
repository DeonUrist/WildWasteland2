using System;
using System.Collections.Generic;
using System.Globalization;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.CleanHUD
{
    /// <summary>Equipment drop through the native TakeItem/Visibility recipe plus the armor/backpack capacity resets (Wave A split; B01 -> a Core inventory service IInventory.DropEquipment, overlapping Gunplay BackpackOverflow).</summary>
    internal static partial class InventoryStyle
    {
        private static void Drop(Slot slot)
        {
            if (!Session.Open || !Application.isFocused || !slot.CanDrop || slot.Take == null || slot.State == null || slot.Item == null) return;
            var item = slot.Item.Value; if (item == null || item.transform.parent != slot.State.transform) return;
            bool binocular = slot.State.gameObject.name == "Binocular_Item";
            if (binocular)
            {
                // Binocular TakeItem has no dropCurrent. Use the same detach/ItemDrop/Visibility recipe.
                var camera = slot.State.transform.parent.parent;
                var point = camera.Find("ItemDrop"); var visibility = FsmUtil.Find(item, "Visibility");
                if (point == null || visibility == null) { CleanHudModule.Warn("Inventory: binocular drop needs native ItemDrop and Visibility"); return; }
                if (slot.State.ActiveStateName == "use")
                {
                    var anim = camera.Find("ItemAnim/Binocular Anim");
                    var animation = anim != null ? FsmUtil.Find(anim.gameObject, "Animation") : null;
                    if (animation != null) animation.SendEvent("Deactivate");
                }
                item.transform.SetParent(null, true); item.transform.position = point.position;
                visibility.SendEvent("Activate"); slot.Take.Fsm.SetState("off");
            }
            else
            {
                // Cancel dropCurrent's next-frame takeWeapon continuation.
                slot.Take.Fsm.SetState("dropCurrent"); slot.Take.Fsm.SetState("off");
            }
            if (item.transform.parent == slot.State.transform) { CleanHudModule.Warn("Inventory: native equipment drop did not detach the item"); return; }
            slot.State.Fsm.SetState("empty");
            var capacity = FsmUtil.Find(GameRefs.GameManager, "ArmorBackpack");
            if (slot.State.gameObject.name == "Armor_Item" && capacity != null)
            { var value = capacity.FsmVariables.FindFsmFloat("armor_capacity"); if (value != null) value.Value = 0; }
            if (slot.State.gameObject.name == "Backpack_Item" && capacity != null)
            {
                var value = capacity.FsmVariables.FindFsmFloat("backpack_capacity"); if (value != null) value.Value = 0;
                var backpack = FsmUtil.Find(GameRefs.GameManager, "Backpack");
                if (backpack != null) { var multiply = backpack.FsmVariables.FindFsmFloat("multiply"); if (multiply != null) multiply.Value = 1; backpack.Fsm.SetState("set"); }
            }
            if (slot.State.gameObject.name == "Flashlight_Item")
            { var camera = GameRefs.PlayerCameraHolder != null ? GameRefs.PlayerCameraHolder.transform.Find("PlayerCamera") : null; var light = camera != null ? FsmUtil.Find(camera.gameObject, "Flashlight") : null; if (light != null) { light.Fsm.SetState("off"); light.Fsm.SetState("noFlashlight"); } }
            Refresh();
            if (Core.Log.Verbose) Core.Log.Info("[CleanHUD] Inventory: dropped " + item.name + (binocular ? " through native Visibility" : " through native TakeItem"));
        }
    }
}
