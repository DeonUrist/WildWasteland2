using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    internal sealed partial class QuestFollowers : IFollowerVehicles
    {
        /// <summary>Checks whether the current value represents seated for the current quest followers.</summary>
        public bool IsSeated(GameObject npc) { return npc != null && npc.transform.parent != null && Core.ModApi.Get<Core.ICrewSeats>()?.Find(npc) != null; }
        /// <summary>Checks whether the current value represents crew for the current quest followers.</summary>
        public bool IsCrew(GameObject npc)
        { return npc != null && (npc.GetComponent<SimpleBanditTag>() != null || Core.ModApi.Get<Core.ICrewSeats>()?.Find(npc) != null || (Settled.Count > 0 && Settled.Contains(npc.GetInstanceID()))); }
        // foot patrol walkers that became camp residents (their crew tags dropped): still former crew for the quest restrictions
        private static readonly HashSet<int> Settled = new HashSet<int>();
        /// <summary>Retains former crew membership so follower restrictions survive dismount.</summary>
        internal static void FormerCrew(GameObject npc) { if (npc != null) Settled.Add(npc.GetInstanceID()); }
        // NPCs Quests has driven as hired followers (Animated/Combat calls): CrewReturn and foot patrols leave them alone. Instance ids, never
        // cleared during a session (a hired former crew member stays excluded); a handful of entries at most.
        private static readonly HashSet<int> Hired = new HashSet<int>();
        /// <summary>Checks whether the current value represents hired for the current quest followers.</summary>
        internal static bool IsHired(GameObject npc) { return npc != null && Hired.Count > 0 && Hired.Contains(npc.GetInstanceID()); }
    }
}
