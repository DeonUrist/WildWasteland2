using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Seat pose part of the Quests follower-vehicle provider (seat pose, release, animated follower flag); Presentation (Wave A split of Crews/QuestFollowers.cs).</summary>
    internal sealed partial class QuestFollowers
    {
        /// <summary>Seats a quest follower with the seat pose (an unknown human type borrows the arm profile of <paramref name="armLike"/>); returns the pose component.</summary>
        public Component SeatPose(GameObject npc, Transform seat, string name, string armLike)
        {
            ArmPoseProfile profile;
            if (!SeatPoseSettings.HumanArmPoses.ContainsKey(name) && SeatPoseSettings.HumanArmPoses.TryGetValue(armLike, out profile))
                SeatPoseSettings.HumanArmPoses[name] = profile;
            Hired.Add(npc.GetInstanceID());
            return Pose.Apply(npc, seat, name);
        }

        /// <summary>Releases a follower seat pose body.</summary>
        public void ReleasePose(Component component)
        { var pose = component as Pose; if (pose != null) pose.ReleaseBody(); }

        /// <summary>Marks a seated follower as animated (it keeps its own arms).</summary>
        public void Animated(GameObject npc, bool enabled)
        {
            if (npc != null) Hired.Add(npc.GetInstanceID());
            var pose = npc != null ? npc.GetComponent<Pose>() : null;
            if (pose != null) pose.AnimatedFollower = enabled;
        }
    }
}
