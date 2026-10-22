using HutongGames.PlayMaker;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Native equipment drop transactions shared by inventory presentation surfaces.</summary>
    public static class EquipmentDrop
    {
        /// <summary>Detaches an equipped native item and restores its capacity/light state; false leaves the UI unchanged.</summary>
        public static bool TryDrop(PlayMakerFSM state, PlayMakerFSM take, GameObject item)
        {
            if (state == null || take == null || item == null || item.transform.parent != state.transform) return false;
            bool binocular = state.gameObject.name == "Binocular_Item";
            if (binocular)
            {
                // Binocular TakeItem has no dropCurrent. Use the same detach/ItemDrop/Visibility recipe.
                var camera = state.transform.parent.parent;
                var point = camera.Find("ItemDrop"); var visibility = FsmUtil.Find(item, "Visibility");
                if (point == null || visibility == null) { Log.Warn(null,"[Core] " + "Inventory: binocular drop needs native ItemDrop and Visibility"); return false; }
                if (state.ActiveStateName == "use")
                {
                    var anim = camera.Find("ItemAnim/Binocular Anim");
                    var animation = anim != null ? FsmUtil.Find(anim.gameObject, "Animation") : null;
                    if (animation != null) animation.SendEvent("Deactivate");
                }
                item.transform.SetParent(null, true); item.transform.position = point.position;
                visibility.SendEvent("Activate"); take.Fsm.SetState("off");
            }
            else
            {
                // Cancel dropCurrent's next-frame takeWeapon continuation.
                take.Fsm.SetState("dropCurrent"); take.Fsm.SetState("off");
            }
            if (item.transform.parent == state.transform) { Log.Warn(null,"[Core] " + "Inventory: native equipment drop did not detach the item"); return false; }
            state.Fsm.SetState("empty");
            var capacity = FsmUtil.Find(GameRefs.GameManager, "ArmorBackpack");
            if (state.gameObject.name == "Armor_Item" && capacity != null)
            { var value = capacity.FsmVariables.FindFsmFloat("armor_capacity"); if (value != null) value.Value = 0; }
            if (state.gameObject.name == "Backpack_Item" && capacity != null)
            {
                var value = capacity.FsmVariables.FindFsmFloat("backpack_capacity"); if (value != null) value.Value = 0;
                var backpack = FsmUtil.Find(GameRefs.GameManager, "Backpack");
                if (backpack != null) { var multiply = backpack.FsmVariables.FindFsmFloat("multiply"); if (multiply != null) multiply.Value = 1; backpack.Fsm.SetState("set"); }
            }
            if (state.gameObject.name == "Flashlight_Item")
            { var camera = GameRefs.PlayerCameraHolder != null ? GameRefs.PlayerCameraHolder.transform.Find("PlayerCamera") : null; var light = camera != null ? FsmUtil.Find(camera.gameObject, "Flashlight") : null; if (light != null) { light.Fsm.SetState("off"); light.Fsm.SetState("noFlashlight"); } }
            if (Log.L2) Log.Deep(null, "Inventory: dropped " + item.name + (binocular ? " through native Visibility" : " through native TakeItem"));
            return true;
        }
    }
}
