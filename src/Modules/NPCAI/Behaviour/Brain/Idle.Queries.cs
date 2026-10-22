using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Idle part behind the NPCAI ModAPI: the travel-order query used for the published behaviour mode.</summary>
    internal static partial class Idle
    {
        /// <summary>True while a travel order (INpcApi.Travel) walks this NPC (TravelState 1).</summary>
        internal static bool Travelling(GameObject npc) { return TravelState(npc) == 1; }
    }
}
