using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    internal sealed class QuestFollowers : IFollowerVehicles
    {
        public bool IsSeated(GameObject npc) { return npc != null && npc.transform.parent != null && npc.GetComponent<Pose>() != null; }
        public float ShootDistance { get { return PatrolsModule.ShootDistance.Value; } }
        public Component SeatPose(GameObject npc, Transform seat, string name, string armLike)
        {
            ArmPoseProfile profile;
            if (!PatrolsModule.HumanArmPoses.ContainsKey(name) && PatrolsModule.HumanArmPoses.TryGetValue(armLike, out profile))
                PatrolsModule.HumanArmPoses[name] = profile;
            Hired.Add(npc.GetInstanceID());
            return Pose.Apply(npc, seat, name);
        }
        public Component SeatCombat(GameObject npc, GameObject car, Transform seat)
        {
            Hired.Add(npc.GetInstanceID()); PassengerGuard.Attach(npc, car, seat);
            var guard = npc.GetComponent<PassengerGuard>(); guard.QuestRanged(); return guard;
        }
        public void ReleasePose(Component component)
        { var pose = component as Pose; if (pose != null) pose.ReleaseBody(); }
        public bool CanFire(GameObject npc)
        { var guard = npc != null ? npc.GetComponent<PassengerGuard>() : null; return guard != null && guard.QuestCanFire; }
        public Type ResolveType(string name)
        {
            switch (name)
            {
                case "Apocapatrol.Plugin": return typeof(PatrolsModule);
                case "Apocapatrol.PassengerGuard": return typeof(PassengerGuard);
                case "Apocapatrol.Pose": return typeof(Pose);
                default: return null;
            }
        }
        public bool IsCrew(GameObject npc)
        { return npc != null && (npc.GetComponent<SimpleBanditTag>() != null || npc.GetComponent<Pose>() != null || (Settled.Count > 0 && Settled.Contains(npc.GetInstanceID()))); }
        // foot patrol walkers that became camp residents (their crew tags dropped): still former crew for the quest restrictions
        private static readonly HashSet<int> Settled = new HashSet<int>();
        internal static void FormerCrew(GameObject npc) { if (npc != null) Settled.Add(npc.GetInstanceID()); }
        // NPCs Quests has driven as hired followers (Animated/Combat calls): CrewReturn and foot patrols leave them alone. Instance ids, never
        // cleared during a session (a hired former crew member stays excluded); a handful of entries at most.
        private static readonly HashSet<int> Hired = new HashSet<int>();
        internal static bool IsHired(GameObject npc) { return npc != null && Hired.Count > 0 && Hired.Contains(npc.GetInstanceID()); }
        public void Animated(GameObject npc, bool enabled)
        {
            if (npc != null) Hired.Add(npc.GetInstanceID());
            var pose = npc != null ? npc.GetComponent<Pose>() : null;
            if (pose != null) pose.AnimatedFollower = enabled;
        }
        public void Combat(GameObject npc, GameObject target, float blockedUntil)
        {
            if (npc != null) Hired.Add(npc.GetInstanceID());
            var guard = npc != null ? npc.GetComponent<PassengerGuard>() : null;
            if (guard != null) guard.QuestTarget(target, blockedUntil);
        }
    }
}
