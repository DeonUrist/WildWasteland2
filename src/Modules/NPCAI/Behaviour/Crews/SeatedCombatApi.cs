using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// ISeatedCombat provider (README 6.2/6.5, ex Patrols PassengerGuard driving): a passenger at a vehicle seat fights from it with its
    /// own vanilla Attack FSM - ranged only, gated by the seat's arc and the shooting distance, target given by the caller. Same rules as
    /// the quest-follower seat combat (QuestFollowers.SeatCombat). Provided by NPCAIModule.ProvideApis.
    /// </summary>
    internal sealed class SeatedCombatApi : ISeatedCombat
    {
        /// <summary>Attaches the seated-combat guard at the seat and switches it to caller-given targets; false without NPC / vehicle.</summary>
        public bool Engage(GameObject npc, GameObject vehicle, Transform seat)
        {
            if (npc == null || vehicle == null || PassengerGuard.Policy==null) return false;
            PassengerGuard.Attach(npc, vehicle, seat);
            var guard = npc.GetComponent<PassengerGuard>();
            if (guard == null) return false;
            guard.QuestRanged();
            return true;
        }
        /// <summary>Removes the seated-combat guard.</summary>
        public void Release(GameObject npc) { var guard = npc != null ? npc.GetComponent<PassengerGuard>() : null; if (guard != null) Object.Destroy(guard); }
        /// <summary>True while the NPC has a seated-combat guard.</summary>
        public bool IsEngaged(GameObject npc) { return npc != null && npc.GetComponent<PassengerGuard>() != null; }
        /// <summary>True when the target is in the seat's arc and range and fire is not blocked.</summary>
        public bool CanFire(GameObject npc) { var guard = npc != null ? npc.GetComponent<PassengerGuard>() : null; return guard != null && guard.QuestCanFire; }
        /// <summary>Assigns the target; fire is blocked until <paramref name="blockedUntil"/> (Time.time).</summary>
        public void SetTarget(GameObject npc, GameObject target, float blockedUntil) { var guard = npc != null ? npc.GetComponent<PassengerGuard>() : null; if (guard != null) guard.QuestTarget(target, blockedUntil); }
        /// <summary>Maximum seated shooting distance ([Patrols / Combat] ShootDistance).</summary>
        public float ShootDistance { get { return PassengerGuard.Policy?.ShootDistance ?? 60f; } }
    }
}
