using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using WildWasteland.Core;


namespace WildWasteland.Modules.HUD
{
    /// <summary>One row of the HUD weapon table (key, caliber, gun/melee/thrown).</summary>
    internal sealed class WeaponInfo
    {
        internal string Key, Caliber, Kind; // Kind: gun, melee, thrown
        /// <summary>Row of the HUD weapon table.</summary>
        internal WeaponInfo(string key, string cal, string kind) { Key = key; Caliber = cal; Kind = kind; }
    }

    /// <summary>Weapon caliber/kind table shown by the HUD weapon panel (Wave A split, B09 -> Gunplay weapon catalog / IWeaponStats).</summary>
    internal static partial class Assets
    {
        // Calibers as printed on the game's own item names, e.g. "M16A1 (5.56mm)".
        internal static readonly Dictionary<string, WeaponInfo> Weapons = Table();
        /// <summary>The HUD caliber/kind table, built from the Gunplay weapon catalog's built-in rows (same keys, calibers and kinds).</summary>
        private static Dictionary<string, WeaponInfo> Table()
        {
            var t = new Dictionary<string, WeaponInfo>(StringComparer.OrdinalIgnoreCase);
            foreach(var row in WeaponNames.BuiltIns())
                t[row.Key]=new WeaponInfo(row.Key,row.Caliber,row.Kind==WeaponKind.Melee?"melee":row.Kind==WeaponKind.Thrown?"thrown":"gun");
            return t;
        }

        private static readonly Regex Paren = new Regex(@"\(([^)]+)\)\s*$", RegexOptions.Compiled);
        // Unknown (modded) weapon: caliber from the item name "Name (cal)", kind from the Reload FSM.
        /// <summary>HUD row of an item: the catalog table, else caliber from the item name "(cal)" and gun/melee from its Reload FSM (remembered).</summary>
        internal static WeaponInfo Describe(GameObject item, string itemName)
        {
            var key = KeyOf(item);
            var catalog=ModApi.Get<IWeaponCatalog>();
            Core.WeaponInfo info;
            if(catalog!=null && key!=null && catalog.TryGet(catalog.Resolve(key),out info) && !string.IsNullOrEmpty(info.Caliber))
                return new WeaponInfo(key,info.Caliber,info.Kind==WeaponKind.Melee?"melee":info.Kind==WeaponKind.Thrown?"thrown":"gun");
            WeaponInfo w;
            if (key != null && Weapons.TryGetValue(key, out w)) return w;
            string cal = null;
            if (!string.IsNullOrEmpty(itemName))
            {
                var m = Paren.Match(itemName);
                if (m.Success) cal = m.Groups[1].Value.Trim().ToUpperInvariant();
            }
            bool gun = WildWasteland.Core.FsmUtil.Find(item, "Reload") != null;
            if (cal == null) cal = gun ? "AMMO" : "MELEE";
            w = new WeaponInfo(key ?? "", cal, gun ? "gun" : "melee");
            if (key != null) Weapons[key] = w;
            return w;
        }
    }
}
