using UnityEngine;
using HutongGames.PlayMaker;
namespace WildWasteland.Core
{
    /// <summary>Patrols-owned traffic rules used by the independent NPC seated-combat engine.</summary>
    public interface IOccupantCombatPolicy
    {
        bool Running { get; }
        bool Ranged { get; }
        float FireArc { get; }
        float ShootDistance { get; }
        float BurstSeconds { get; }
        float IntervalMin { get; }
        float IntervalMax { get; }
        float MaxAimPitch { get; }
        float AimTurnSpeed { get; }
        float BikeArc { get; }
        bool Allowed(GameObject vehicle, bool driver, bool turret);
        bool HasTraffic(GameObject vehicle);
        GameObject Target(GameObject vehicle);
        void PlayerContact(GameObject vehicle);
    }
    /// <summary>Read-only rider pose intent implemented by the actual traffic actor.</summary>
    public interface IRiderPresentationState
    {
        bool Mounted { get; }
        bool AnimatedPose { get; }
        bool StandingPose { get; }
    }
    /// <summary>NPC-owned actual occupant combat operations; callers never access private guard state.</summary>
    public interface IOccupantCombat
    {
        Component Attach(GameObject actor, GameObject car, Transform seat, bool driver=false, bool turret=false, float arc=0f);
        void Prepare(GameObject actor, bool combatAllowed);
        void NeutralizeContact(GameObject actor);
        bool IsRanged(GameObject actor);
        bool IsRangedName(string name);
        bool Engaged(GameObject actor);
        bool CanAnswer(GameObject actor, GameObject enemy);
        void Promote(GameObject actor, Transform seat);
        void Release(GameObject actor);
        void Mute(GameObject actor, bool combat);
        void Mute(PlayMakerFSM[] fsms, bool combat);
        Component Follow(GameObject actor, GameObject car, Transform seat);
    }
}
