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
        public float ShootDistance { get { return Core.ModApi.Get<Core.ISeatedCombat>()?.ShootDistance ?? 60f; } }

        /// <summary>Seats a quest follower for combat (PassengerGuard, ranged, caller-targeted).</summary>
        public Component SeatCombat(GameObject npc, GameObject car, Transform seat)
        {
            Hired.Add(npc.GetInstanceID());return ModApi.Get<IOccupantCombat>()?.Follow(npc,car,seat);
        }

        /// <summary>True when the seated follower has a clear shot now.</summary>
        public bool CanFire(GameObject npc)
        { return ModApi.Get<ISeatedCombat>()?.CanFire(npc)==true; }

        /// <summary>Gives the seated follower its target; fire blocked until a time.</summary>
        public void Combat(GameObject npc, GameObject target, float blockedUntil)
        {
            if (npc != null) Hired.Add(npc.GetInstanceID());
            ModApi.Get<ISeatedCombat>()?.SetTarget(npc,target,blockedUntil);
        }
    }
}
