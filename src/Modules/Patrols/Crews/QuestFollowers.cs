using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    internal sealed partial class QuestFollowers : IFollowerVehicles
    {
        public bool IsSeated(GameObject npc) { return npc != null && npc.transform.parent != null && Core.ModApi.Get<Core.ICrewSeats>()?.Find(npc) != null; }
        public Type ResolveType(string name)
        {
            switch (name)
            {
                case "Apocapatrol.Plugin": return typeof(PatrolsModule);
                case "Apocapatrol.PassengerGuard": return typeof(PassengerGuard);
                case "Apocapatrol.Pose": return Core.ModApi.Get<Core.ICrewSeats>()?.LegacyPoseType;
                default: return null;
            }
        }
        public bool IsCrew(GameObject npc)
        { return npc != null && (npc.GetComponent<SimpleBanditTag>() != null || Core.ModApi.Get<Core.ICrewSeats>()?.Find(npc) != null || (Settled.Count > 0 && Settled.Contains(npc.GetInstanceID()))); }
        // foot patrol walkers that became camp residents (their crew tags dropped): still former crew for the quest restrictions
        private static readonly HashSet<int> Settled = new HashSet<int>();
        internal static void FormerCrew(GameObject npc) { if (npc != null) Settled.Add(npc.GetInstanceID()); }
        // NPCs Quests has driven as hired followers (Animated/Combat calls): CrewReturn and foot patrols leave them alone. Instance ids, never
        // cleared during a session (a hired former crew member stays excluded); a handful of entries at most.
        private static readonly HashSet<int> Hired = new HashSet<int>();
        internal static bool IsHired(GameObject npc) { return npc != null && Hired.Count > 0 && Hired.Contains(npc.GetInstanceID()); }
    }
}
