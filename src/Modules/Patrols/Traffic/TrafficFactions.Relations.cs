using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Faction relations (Coyotes/Scrapyard normalisation, opposed check, player hostility from the game's Coyote relation FSM); Core Factions service in Wave B (Wave A split of Patrols Traffic/TrafficFactions.cs).</summary>
    internal static partial class TrafficFactions
    {
        /// <summary>Preserves the patrol two-faction normalization for roster and native relation queries.</summary>
        internal static string Normalize(string faction)=>string.Equals(faction,"Coyotes",StringComparison.OrdinalIgnoreCase)?"Coyotes":"Scrapyard";
        /// <summary>Tests whether the two normalized patrol factions differ.</summary>
        internal static bool Opposed(string a,string b)=>Normalize(a)!=Normalize(b);
        /// <summary>Reads the shared native player relation after patrol faction normalization.</summary>
        internal static bool PlayerHostile(string faction)
        {
            if (Core.ModApi.Get<Core.IPlayerMentalEffects>()?.ProvokesFaction(Core.Entities.Player,faction)==true) return true;
            return Core.NativeFactionRelations.PlayerHostile(Normalize(faction));
        }
    }
}
