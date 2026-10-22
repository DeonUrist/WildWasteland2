using WildWasteland.Modules.Presentation;
using Pose = WildWasteland.Modules.Presentation.Pose;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Owns profile fallback and actual animated follower seat resources.</summary>
    internal sealed class FollowerSeatPresentation : IFollowerSeatPresentation
    {
        /// <summary>Applies the native follower pose, borrowing the selected arm profile for unknown humans.</summary>
        public Component Apply(GameObject actor,Transform seat,string name,string armLike)
        {ArmPoseProfile profile;if(!SeatPoseSettings.HumanArmPoses.ContainsKey(name)&&SeatPoseSettings.HumanArmPoses.TryGetValue(armLike,out profile))SeatPoseSettings.HumanArmPoses[name]=profile;return Pose.Apply(actor,seat,name);}
        /// <summary>Releases the actual pose body before the caller removes its component.</summary>
        public void Release(Component component){var pose=component as Pose;if(pose!=null)pose.ReleaseBody();}
        /// <summary>Allows a follower to keep its current arm animation while seated.</summary>
        public void Animated(GameObject actor,bool enabled){var pose=actor!=null?actor.GetComponent<Pose>():null;if(pose!=null)pose.AnimatedFollower=enabled;}
    }
}
