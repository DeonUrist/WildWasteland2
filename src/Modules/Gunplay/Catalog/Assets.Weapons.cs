using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace WildWasteland.Modules.CleanHUD
{
    internal sealed class WeaponInfo
    {
        internal string Key, Caliber, Kind; // Kind: gun, melee, thrown
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
            var cat = WildWasteland.Modules.Gunplay.WeaponCatalog.Instance;
            foreach (var key in cat.BuiltInKeys)
            {
                var e = cat.EntryOf(cat.Resolve(key));
                if (e == null) continue;
                var k = e.Info.Kind;
                t[key] = new WeaponInfo(key, e.Info.Caliber, k == WildWasteland.Core.WeaponKind.Melee ? "melee" : k == WildWasteland.Core.WeaponKind.Thrown ? "thrown" : "gun");
            }
            return t;
        }

        private static readonly Regex Paren = new Regex(@"\(([^)]+)\)\s*$", RegexOptions.Compiled);
        // Unknown (modded) weapon: caliber from the item name "Name (cal)", kind from the Reload FSM.
        internal static WeaponInfo Describe(GameObject item, string itemName)
        {
            var key = KeyOf(item);
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
