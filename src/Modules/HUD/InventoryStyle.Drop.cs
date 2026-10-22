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
            if (EquipmentDrop.TryDrop(slot.State, slot.Take, slot.Item.Value)) Refresh();
        }
    }
}
