using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    public static partial class ModAPI
    {
        public static GameObject PlayerRoot { get { return Game.Ready ? Game.Player : null; } }
        public static bool PlayerCanTraverse
        {
            get { return PresentationModule.WorldRunning && Game.Ready && !Game.Dead && !Game.Paused && !Game.InCar
                && !Crawling.Active && Game.StandingHeight >= 1.4f && BodyRunner.PlayerBody != null && BodyRunner.PlayerBody.Alive && !BodyRunner.PlayerBody.IsRagdoll; }
        }
        public static bool PlayerTraversalActive { get { return BodyRunner.PlayerBody != null && BodyRunner.PlayerBody.Traversal != null; } }
        internal static bool PlayerWeaponStowed { get { return PlayerTraversalActive && BodyRunner.PlayerBody.Traversal.StowWeapon; } }
        internal static bool PlayerTraversalCameraActive { get { return PlayerTraversalActive && BodyRunner.PlayerBody.Traversal.FollowHeadInFirstPerson && !Game.InCar; } }
        internal static bool TryPlayerTraversalEye(out Vector3 eye)
        {
            eye = Vector3.zero;
            return PlayerTraversalCameraActive && BodyRunner.PlayerBody.Traversal.TryEye(out eye);
        }
        public static PlayerTraversal BeginPlayerTraversal(AnimationClip clip, Quaternion facing)
        {
            if (!PlayerCanTraverse || PlayerTraversalActive || clip == null || !clip.humanMotion) return null;
            return BodyRunner.PlayerBody.BeginTraversal(clip, facing);
        }
        internal static PlayerTraversal BeginPlayerPoseTraversal(PoseClip pose, Quaternion facing)
        {
            if (!PlayerCanTraverse || PlayerTraversalActive || pose == null || !pose.Ready) return null;
            return BodyRunner.PlayerBody.BeginPoseTraversal(pose, facing);
        }
    }
}
