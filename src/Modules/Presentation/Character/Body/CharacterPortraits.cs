using System;
using System.IO;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    internal static partial class CharacterPortraits
    {
        internal static readonly string[] Names = { "Female", "Male", "Merchant", "Bodyguard", "Sprokka", "Pistoleer", "Mechanic", "OrganicMechanic", "Gungirl", "Shoota" };
        internal static readonly int[] Order = { 1, 2, 3, 4, 7, 0, 8, 9 };
        // (Wave A) the HUD part's picture caches stay here: their initializers need Names, and the order of static initializers across
        // partial files is unspecified
        static readonly Texture2D[] Pictures = new Texture2D[Names.Length];
        static readonly DateTime[] Modified = new DateTime[Names.Length];
        /// <summary>Maps the legacy Pistoleer / Mechanic choices to Bodyguard / Merchant.</summary>
        internal static int Canonical(int choice) { return choice == 5 ? 3 : choice == 6 ? 2 : choice; }
    }
}
