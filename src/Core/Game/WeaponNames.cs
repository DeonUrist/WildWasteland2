using System;
namespace WildWasteland.Core
{
    /// <summary>Native weapon animation hold styles.</summary>
    public enum WeaponHold { None, Rifle, Pistol, Melee, Throw }
    /// <summary>Canonical native name rules and shipped caliber metadata, independent of optional weapon simulation.</summary>
    public static class WeaponNames
    {
        /// <summary>Enumerates detached native caliber rows in authored order.</summary>
        public static System.Collections.Generic.IEnumerable<WeaponInfo> BuiltIns() { return new WeaponInfo[] {
            new WeaponInfo { Key="folk_17", Caliber="9MM", Kind=WeaponKind.Other }, new WeaponInfo { Key="9mm_borz_smg", Caliber="9MM", Kind=WeaponKind.Other },
            new WeaponInfo { Key="22_pipe_pistol", Caliber=".22LR", Kind=WeaponKind.Other }, new WeaponInfo { Key="22_pipe_revolver", Caliber=".22LR", Kind=WeaponKind.Other }, new WeaponInfo { Key="22_pipe_smg", Caliber=".22LR", Kind=WeaponKind.Other },
            new WeaponInfo { Key="akms", Caliber="7.62MM", Kind=WeaponKind.Other }, new WeaponInfo { Key="akm_trash", Caliber="7.62MM", Kind=WeaponKind.Other }, new WeaponInfo { Key="akm_drum", Caliber="7.62MM", Kind=WeaponKind.Other },
            new WeaponInfo { Key="m16a1", Caliber="5.56MM", Kind=WeaponKind.Other },
            new WeaponInfo { Key="redmark_m11", Caliber=".30-06", Kind=WeaponKind.Other }, new WeaponInfo { Key="redmark_m11_scoped", Caliber=".30-06", Kind=WeaponKind.Other },
            new WeaponInfo { Key="rochester_m24", Caliber="12GA", Kind=WeaponKind.Other }, new WeaponInfo { Key="rochester_m24_chopped", Caliber="12GA", Kind=WeaponKind.Other },
            new WeaponInfo { Key="slamberg_500", Caliber="12GA", Kind=WeaponKind.Other }, new WeaponInfo { Key="slamberg_500_chopped", Caliber="12GA", Kind=WeaponKind.Other },
            new WeaponInfo { Key="slamfire_shotgun", Caliber="20GA", Kind=WeaponKind.Other },
            new WeaponInfo { Key="crossbow", Caliber="ARROW", Kind=WeaponKind.Other },
            new WeaponInfo { Key="blastlance_1", Caliber="THROWN", Kind=WeaponKind.Thrown }, new WeaponInfo { Key="blastlance_2", Caliber="THROWN", Kind=WeaponKind.Thrown },
            new WeaponInfo { Key="machete", Caliber="MELEE", Kind=WeaponKind.Melee }, new WeaponInfo { Key="old_knife", Caliber="MELEE", Kind=WeaponKind.Melee }, new WeaponInfo { Key="shiv", Caliber="MELEE", Kind=WeaponKind.Melee }, new WeaponInfo { Key="pipe_wrench", Caliber="MELEE", Kind=WeaponKind.Melee },
        }; }
        /// <summary>Classifies native gun names with the canonical unchanged text predicates.</summary>
        public static WeaponKind GunKindOfName(string weapon)
        {
            string n = weapon.ToLowerInvariant();
            if (n.Contains("crossbow")) return WeaponKind.Crossbow;
            if (n.Contains("shotgun") || n.Contains("slamfire") || n.Contains("slamberg") || n.Contains("rochester")) return WeaponKind.Shotgun;
            if (n.Contains("scoped") || n.Contains("sniper") || n.Contains("redmark")) return WeaponKind.Sniper;
            if (n.Contains("smg") || n.Contains("borz")) return WeaponKind.Smg;
            if (n.Contains("pistol") || n.Contains("revolver") || n.Contains("folk_17")) return WeaponKind.Pistol;
            return WeaponKind.Rifle;
        }

        /// <summary>Animation hold style of a weapon name - the former PlayerBody Props.KindOfName text, unchanged.</summary>
        public static WeaponHold HoldOfName(string weapon)
        {
            if (string.IsNullOrEmpty(weapon) || weapon == "hands") return WeaponHold.None;
            string w = weapon.ToLowerInvariant();
            if (w.Contains("blastlance") || w.Contains("grenade")) return WeaponHold.Throw;
            if (w.Contains("shiv") || w.Contains("knife") || w.Contains("machete") || w.Contains("wrench") || w.Contains("pipe_wrench")) return WeaponHold.Melee;
            // SMGs (borz_smg, 22_pipe_smg) are held and animated like pistols (Pistol* clips, PistolReload)
            if (w.Contains("pistol") || w.Contains("revolver") || w.Contains("folk_17") || w.Contains("smg") || w.Contains("borz")) return WeaponHold.Pistol;
            return WeaponHold.Rifle;
        }

    }
}
