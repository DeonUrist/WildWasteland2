using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Seated combat part of the Quests follower-vehicle provider (seat combat, can fire, target, shoot distance); NPCAI (Wave A split of Crews/QuestFollowers.cs).</summary>
    internal sealed partial class QuestFollowers
    {
        /// <summary>Seated shooting distance ([Patrols / Combat] ShootDistance).</summary>
        public float ShootDistance { get { return PatrolsModule.ShootDistance.Value; } }

        /// <summary>Seats a quest follower for combat (PassengerGuard, ranged, caller-targeted).</summary>
        public Component SeatCombat(GameObject npc, GameObject car, Transform seat)
        {
            Hired.Add(npc.GetInstanceID()); PassengerGuard.Attach(npc, car, seat);
            var guard = npc.GetComponent<PassengerGuard>(); guard.QuestRanged(); return guard;
        }

        /// <summary>True when the seated follower has a clear shot now.</summary>
        public bool CanFire(GameObject npc)
        { var guard = npc != null ? npc.GetComponent<PassengerGuard>() : null; return guard != null && guard.QuestCanFire; }

        /// <summary>Gives the seated follower its target; fire blocked until a time.</summary>
        public void Combat(GameObject npc, GameObject target, float blockedUntil)
        {
            if (npc != null) Hired.Add(npc.GetInstanceID());
            var guard = npc != null ? npc.GetComponent<PassengerGuard>() : null;
            if (guard != null) guard.QuestTarget(target, blockedUntil);
        }
    }
}
