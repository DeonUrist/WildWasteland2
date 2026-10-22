using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Seat pose part of the Quests follower-vehicle provider (seat pose, release, animated follower flag); Presentation (Wave A split of Crews/QuestFollowers.cs).</summary>
    internal sealed partial class QuestFollowers
    {
        public Component SeatPose(GameObject npc, Transform seat, string name, string armLike)
        {
            ArmPoseProfile profile;
            if (!SeatPoseSettings.HumanArmPoses.ContainsKey(name) && SeatPoseSettings.HumanArmPoses.TryGetValue(armLike, out profile))
                SeatPoseSettings.HumanArmPoses[name] = profile;
            Hired.Add(npc.GetInstanceID());
            return Pose.Apply(npc, seat, name);
        }

        public void ReleasePose(Component component)
        { var pose = component as Pose; if (pose != null) pose.ReleaseBody(); }

        public void Animated(GameObject npc, bool enabled)
        {
            if (npc != null) Hired.Add(npc.GetInstanceID());
            var pose = npc != null ? npc.GetComponent<Pose>() : null;
            if (pose != null) pose.AnimatedFollower = enabled;
        }
    }
}
