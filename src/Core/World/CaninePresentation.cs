using System.Collections.Generic;
using UnityEngine;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
namespace WildWasteland.Core
{
    /// <summary>Live actor decisions projected by NPCAI; Presentation never reads a private Agent.</summary>
    public interface IAnimalActor
    {
        GameObject Owner { get; } Transform T { get; } Collider Col { get; } GameObject Target { get; }
        bool Combat { get; } bool Quiet { get; } bool HasGhost { get; } bool Blown { get; } bool Travel { get; set; }
    }
    /// <summary>Optional companion's actual movement/combat operations supplied by Compatibility.</summary>
    public interface ICanineCompanion
    {
        bool Active { get; } bool Riding { get; } bool Sneaking { get; } float Speed { get; } float RunSpeed { get; }
        Vector3 Feet { get; } float PawHeight { get; } float NativeNextBite { get; }
        bool TargetAlive(GameObject target);void Halt(bool held);void Contact(GameObject target);void SetCooldown(float until);
        void Dash(Vector3 direction,float speed,float dt,GameObject target);
    }
    /// <summary>Canine animation states shared by the decision and rig providers.</summary>
    public enum CaninePose { Locomotion, LieDown, Rest, GetUp, Bite, RunningBite, JumpAttack, SitDown, Sitting }
    /// <summary>Explicit per-canine animation/combat handoff, never a module-private partial record.</summary>
        public sealed class CanineState
        {
            public IAnimalActor Agent; public Animator Animator; public Rigidbody Body;
            public ICanineCompanion Companion;
            public Dictionary<string, AnimationClip> Clips;
            public Transform[] Bones; public Vector3[] NativePosition, FadePosition;
            public Quaternion[] NativeRotation, FadeRotation;
            public Vector3[] DisplayPosition; public Quaternion[] DisplayRotation; public bool HasDisplay;
            public Transform[] SupportPaws; public Transform SupportMarker; public Vector3 AnimatorPosition; public bool Aligned;
            public float SoleOffset, FadeSoleOffset;
            public Behaviour[] Ik; public bool[] IkEnabled;
            public AnimatorCullingMode Culling;
            public SkinnedMeshRenderer[] Meshes; public bool[] MeshOffscreen; public Bounds[] MeshBounds;
            public CaninePose Pose; public string Playing; public float Time, Fade, NextBite;
            public float Reach = 1f, Drag, JumpSince; public bool Applied, Held, Prepared, Jumping, Committing, HitSent;
            public bool Boss, WasQuiet, Dashing;
            public float NextIdleChange, AttackRate = 1f, RecoveryUntil, DashSpeed;
            public Vector3 DashDirection;
            public PlayMakerFSM DamageComponent; public float NextDamageLook;
            // (WW perf, D10) the target's collider (looked up once per target, again when it is destroyed or after 2 s) and this frame's
            // closest point / line of reach to it (TargetPoint and InReach ran the lookup, ClosestPoint and the Linecast up to 4 times a frame)
            public GameObject ColOf; public Collider TargetCol; public float ColAt;
            public int ReachFrame = -1; public GameObject ReachOf; public Vector3 ReachOrigin, ReachPoint; public sbyte ReachLine;
            public readonly Dictionary<Wait, float> OriginalWaits = new Dictionary<Wait, float>();
            public GameObject BiteTarget; public Fsm DamageFsm;
            public Transform[] LegTop, LegPaw;      // (1.9.2) the two front legs: shoulder bone and paw (deepest skinned bone)
            public Transform[] HindTop; public Transform FrontRoot; public bool LevelLogged; public float ShoulderOverHip;   // (1.9.3) hind leg tops; the bone that carries the front half (chest, front legs, neck)
        }

    /// <summary>Actual animal rig poses and bounded sand effects owned by Presentation.</summary>
    public interface IAnimalVisuals { void Sink(Transform rig,Transform actor,Vector3 origin,float depth); void Pitch(Transform rig,Quaternion origin,float pitch); void Bite(Transform actor,Transform rig,Vector3 origin,Transform[] bones,float angle,float push); void Sand(Vector3 at,float scale,float volume,float pitch,float range,bool hunter); }
    /// <summary>Optional companion threat and diagnostics supplied by Compatibility.</summary>
    public interface ICompanionThreat { GameObject CloseThreat(GameObject enemy,float range); int Count { get; } }
    public interface ICanineRestPolicy { bool Resting { get; } }
    /// <summary>Actual NPC decisions and native damage gates for a driven canine.</summary>
    public interface ICanineBehaviour
    {
        bool Enabled { get; } bool Controls(GameObject actor); float MaxDistance { get; }
        void Tick(CanineState dog,float dt);void Travel(GameObject actor,bool on);void Halt(GameObject actor,bool held);float RunSpeed(GameObject actor);
    }
    /// <summary>Presentation-owned canine rig and reversible visual operations.</summary>
    public interface ICaninePresentation
    {
        bool Enabled { get; }
        int DogCount { get; } int ClipCount { get; } bool BundleLoaded { get; } void Clear();
        CanineState Find(GameObject actor);bool Register(IAnimalActor actor,string prefab,ICanineCompanion companion);
        bool Owns(GameObject actor);void Remove(GameObject actor);void Tick(GameObject actor);void Late(GameObject actor);void Restore(GameObject actor);bool Dashing(GameObject actor);
        bool Eligible(CanineState dog);void Prepare(CanineState dog);void Release(CanineState dog);void Switch(CanineState dog,CaninePose pose,string clip);void Restore(CanineState dog);void ReleaseJump(CanineState dog);void ReleaseDash(CanineState dog);
        bool Grounded(CanineState dog);
    }
}
