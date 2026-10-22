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
        private static Dictionary<string, WeaponInfo> Table()
        {
            var t = new Dictionary<string, WeaponInfo>(StringComparer.OrdinalIgnoreCase);
            Action<string, string, string> a = (k, c, kind) => t[k] = new WeaponInfo(k, c, kind);
            a("folk_17", "9MM", "gun"); a("9mm_borz_smg", "9MM", "gun");
            a("22_pipe_pistol", ".22LR", "gun"); a("22_pipe_revolver", ".22LR", "gun"); a("22_pipe_smg", ".22LR", "gun");
            a("akms", "7.62MM", "gun"); a("akm_trash", "7.62MM", "gun"); a("akm_drum", "7.62MM", "gun");
            a("m16a1", "5.56MM", "gun");
            a("redmark_m11", ".30-06", "gun"); a("redmark_m11_scoped", ".30-06", "gun");
            a("rochester_m24", "12GA", "gun"); a("rochester_m24_chopped", "12GA", "gun");
            a("slamberg_500", "12GA", "gun"); a("slamberg_500_chopped", "12GA", "gun");
            a("slamfire_shotgun", "20GA", "gun");
            a("crossbow", "ARROW", "gun");
            a("blastlance_1", "THROWN", "thrown"); a("blastlance_2", "THROWN", "thrown");
            a("machete", "MELEE", "melee"); a("old_knife", "MELEE", "melee"); a("shiv", "MELEE", "melee"); a("pipe_wrench", "MELEE", "melee");
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
            bool gun = Bindings.Fsm(item, "Reload") != null;
            if (cal == null) cal = gun ? "AMMO" : "MELEE";
            w = new WeaponInfo(key ?? "", cal, gun ? "gun" : "melee");
            if (key != null) Weapons[key] = w;
            return w;
        }
    }
}
