using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Faction relations (Coyotes/Scrapyard normalisation, opposed check, player hostility from the game's Coyote relation FSM); Core Factions service in Wave B (Wave A split of Patrols Traffic/TrafficFactions.cs).</summary>
    internal static partial class TrafficFactions
    {
        internal static string Normalize(string faction)=>string.Equals(faction,"Coyotes",StringComparison.OrdinalIgnoreCase)?"Coyotes":"Scrapyard";
        internal static bool Opposed(string a,string b)=>Normalize(a)!=Normalize(b);
        internal static bool PlayerHostile(string faction)
        {
            return Core.NativeFactionRelations.PlayerHostile(Normalize(faction));
        }
    }
}
