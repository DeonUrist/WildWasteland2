using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Props part (split from PlayerBody/Body/Props.cs in Wave A): the weapon-name classifier (Rifle / Pistol / Melee / Throw) the
    /// player body and the ModAPI characters animate by. Target: Gunplay weapon catalog (B09, IWeaponCatalog); Presentation then reads the
    /// kind from the catalog.
    /// </summary>
    internal static partial class Props
    {
        public enum Kind { None, Rifle, Pistol, Melee, Throw }

        // (WW perf) memo per weapon name (asked per frame; ToLowerInvariant made a string per call). Pure function of the name.
        private static readonly Dictionary<string, Kind> _kinds = new Dictionary<string, Kind>();
        public static Kind KindOf(string weapon)
        {
            if (string.IsNullOrEmpty(weapon) || weapon == "hands") return Kind.None;
            Kind k;
            if (_kinds.TryGetValue(weapon, out k)) return k;
            if (_kinds.Count > 512) _kinds.Clear();
            return _kinds[weapon] = KindOfName(weapon);
        }
        /// <summary>Hold style from the Gunplay weapon catalog rule (same order and text as before; the enum values map 1:1).</summary>
        private static Kind KindOfName(string weapon) { return (Kind)(int)WildWasteland.Modules.Gunplay.WeaponCatalog.HoldOfName(weapon); }
    }
}
