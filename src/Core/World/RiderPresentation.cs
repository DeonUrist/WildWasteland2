using System;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Presentation owns a rider's animation lifetime, hidden native props and held lance visual.</summary>
    public interface IRiderVisual : IDisposable
    {
        bool Active { get; } bool Animated { get; } float ThrowSeconds { get; } Transform Lance { get; }
        void Play(int clip);void Blend(int target,float seconds);void PauseThrow();void HideWeapons();void Offset();
    }
    /// <summary>Owns real rider visual leases, facing and voice playback.</summary>
    public interface IRiderVisuals { IDisposable Jump(GameObject actor); float Scream(Transform at); IRiderVisual Attach(GameObject actor,GameObject vehicle,IRiderPresentationState state,bool bike); void Face(GameObject actor,GameObject vehicle,bool bike,Vector3 direction); }
    /// <summary>Gunplay owns actual lance creation, ballistics, collision fuse and explosive damage.</summary>
    public interface IRiderWeapons { void Throw(Vector3 from,Vector3 velocity,Vector3 target,Vector3 targetVelocity,float speed,Collider[] platform);void Blast(Vector3 at,string who,float radius,float damage); }
}
