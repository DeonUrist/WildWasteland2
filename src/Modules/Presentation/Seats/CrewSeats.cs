using System;
using Pose = WildWasteland.Modules.Presentation.Pose;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Patrols;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Creates and controls the existing occupant pose implementation.</summary>
    internal sealed class CrewSeats : ICrewSeats
    {
        /// <summary>Applies an actual seat pose to a native actor.</summary>
        public ICrewSeatPose Apply(GameObject actor,Transform seat,string humanType) {return Pose.Apply(actor,seat,humanType);}
        /// <summary>Reads an existing pose without creating or guessing it.</summary>
        public ICrewSeatPose Find(GameObject actor) {return actor!=null?actor.GetComponent<Pose>():null;}
        /// <summary>Guards the actual anchor after animation while the owning combat system reports it mounted.</summary>
        public void Anchor(GameObject actor,Transform anchor,bool gunner,Func<bool> mounted) {RiderAnchorGuard.Attach(actor,mounted,anchor,gunner);}
        /// <summary>Retains the frozen follower adapter's current component mapping until its consumers leave reflection.</summary>
        public Type LegacyPoseType {get{return typeof(Pose);}}
    }
}
