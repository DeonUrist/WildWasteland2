using System;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Commands for a Presentation-owned occupant pose.</summary>
    public interface ICrewSeatPose
    {
        void SetAim(Vector3 point,bool active);
        void SetShooting(bool enabled);
        void ConfigureTurret(bool standing,bool bike,bool warboy=false);
        void ConfigureBusPassenger();
        void ConfigurePassengerAim();
        void Reseat(Transform anchor);
        void ReleaseBody();
    }
    /// <summary>Creates and queries real Presentation-owned seat poses without exposing component implementations.</summary>
    public interface ICrewSeats
    {
        Vector3 DriverOffset { get; }
        bool Enabled { get; }
        ICrewSeatPose Apply(GameObject actor,Transform seat,string humanType);
        ICrewSeatPose Find(GameObject actor);
        void Anchor(GameObject actor,Transform anchor,bool gunner,Func<bool> mounted);
        Type LegacyPoseType { get; }
    }
}
