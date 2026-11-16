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

    /// <summary>Starts an exit pose while gameplay still owns the seated actor and its eventual transfer.</summary>
    public interface ICrewSeatExit
    {
        Func<bool> StepOut(GameObject actor, Transform car, Vector3 localGround, bool passenger);
    }

    public static class VehicleExitRoll
    {
        public const float Threshold = 12f / 3.6f;
        public const float Carry = .8f;
        public static bool Required(Vector3 velocity) => velocity.sqrMagnitude > Threshold * Threshold;
        public static float Rate(Vector3 velocity) => Mathf.Sqrt(velocity.magnitude / Threshold);
        public static float Damage(Vector3 velocity) => velocity.magnitude * 1.8f; // km/h divided by two
    }
}
