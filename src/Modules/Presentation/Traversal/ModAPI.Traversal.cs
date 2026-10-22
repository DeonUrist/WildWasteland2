using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    public static partial class ModAPI
    {
        /// <summary>The player root object while the game is ready, else null.</summary>
        public static GameObject PlayerRoot { get { return Game.Ready ? Core.PlayerState.Player : null; } }
        public static bool PlayerCanTraverse
        {
            get { return PresentationModule.WorldRunning && Game.Ready && !Core.PlayerState.Dead && !Core.PlayerState.Paused && !Core.PlayerState.InCar
                && !Crawling.Active && Core.PlayerState.StandingHeight >= 1.4f && BodyRunner.PlayerBody != null && BodyRunner.PlayerBody.Alive && !BodyRunner.PlayerBody.IsRagdoll; }
        }
        public static bool PlayerTraversalActive { get { return BodyRunner.PlayerBody != null && BodyRunner.PlayerBody.Traversal != null; } }
        /// <summary>True while a traversal stows the weapon.</summary>
        internal static bool PlayerWeaponStowed { get { return PlayerTraversalActive && BodyRunner.PlayerBody.Traversal.StowWeapon; } }
        /// <summary>True while a traversal moves the first-person eye with the head (on foot).</summary>
        internal static bool PlayerTraversalCameraActive { get { return PlayerTraversalActive && BodyRunner.PlayerBody.Traversal.FollowHeadInFirstPerson && !Core.PlayerState.InCar; } }
        /// <summary>The first-person eye during a traversal.</summary>
        internal static bool TryPlayerTraversalEye(out Vector3 eye)
        {
            eye = Vector3.zero;
            return PlayerTraversalCameraActive && BodyRunner.PlayerBody.Traversal.TryEye(out eye);
        }
        /// <summary>Starts a clip traversal on the player body; null when not allowed or busy.</summary>
        public static PlayerTraversal BeginPlayerTraversal(AnimationClip clip, Quaternion facing)
        {
            if (!PlayerCanTraverse || PlayerTraversalActive || clip == null || !clip.humanMotion) return null;
            return BodyRunner.PlayerBody.BeginTraversal(clip, facing);
        }
        /// <summary>Starts a pose-clip traversal on the player body; null when not allowed or busy.</summary>
        internal static PlayerTraversal BeginPlayerPoseTraversal(PoseClip pose, Quaternion facing)
        {
            if (!PlayerCanTraverse || PlayerTraversalActive || pose == null || !pose.Ready) return null;
            return BodyRunner.PlayerBody.BeginPoseTraversal(pose, facing);
        }
    }
}
