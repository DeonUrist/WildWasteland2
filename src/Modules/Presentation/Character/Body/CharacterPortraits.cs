using System;
using System.IO;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    internal static partial class CharacterPortraits
    {
        internal static readonly string[] Names = { "Female", "Male", "Merchant", "Bodyguard", "Sprokka", "Pistoleer", "Mechanic", "OrganicMechanic", "Gungirl", "Shoota" };
        internal static readonly int[] Order = { 1, 2, 3, 4, 7, 0, 8, 9 };
        /// <summary>Requests the HUD-owned portrait bank for player settings.</summary>
        internal static Texture Picture(int choice) { return WildWasteland.Core.ModApi.Get<WildWasteland.Core.ICharacterPortraits>()?.Picture(Canonical(choice)); }
        /// <summary>Maps the legacy Pistoleer / Mechanic choices to Bodyguard / Merchant.</summary>
        internal static int Canonical(int choice) { return choice == 5 ? 3 : choice == 6 ? 2 : choice; }
    }
}
