using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Seated combat part of the Quests follower-vehicle provider (seat combat, can fire, target, shoot distance); NPCAI (Wave A split of Crews/QuestFollowers.cs).</summary>
    internal sealed partial class QuestFollowers
    {
        public float ShootDistance { get { return PatrolsModule.ShootDistance.Value; } }

        public Component SeatCombat(GameObject npc, GameObject car, Transform seat)
        {
            Hired.Add(npc.GetInstanceID()); PassengerGuard.Attach(npc, car, seat);
            var guard = npc.GetComponent<PassengerGuard>(); guard.QuestRanged(); return guard;
        }

        public bool CanFire(GameObject npc)
        { var guard = npc != null ? npc.GetComponent<PassengerGuard>() : null; return guard != null && guard.QuestCanFire; }

        public void Combat(GameObject npc, GameObject target, float blockedUntil)
        {
            if (npc != null) Hired.Add(npc.GetInstanceID());
            var guard = npc != null ? npc.GetComponent<PassengerGuard>() : null;
            if (guard != null) guard.QuestTarget(target, blockedUntil);
        }
    }
}
