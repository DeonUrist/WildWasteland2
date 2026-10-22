using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace WildWasteland.Modules.Presentation
{
    // A lease on the player's animation only. The caller owns collision detection,
    // physical movement and temporarily suspending the game's movement FSMs.
    public sealed class PlayerTraversal : IDisposable
    {
        internal readonly Body Body;
        internal float Seconds;
        internal Quaternion Facing;
        /// <summary>Lease of the body with its facing.</summary>
        internal PlayerTraversal(Body body, Quaternion facing) { Body = body; Facing = facing; }
        public bool Active { get { return Body != null && Body.Alive && BodyRunner.PlayerBody == Body && Body.Traversal == this; } }
        public bool StowWeapon { get; set; }
        public bool FollowHeadInFirstPerson { get; set; }
        // (WW ladders) a looping pose animation instead of a clip: Seconds is unused, Phase (cycles, wrapping) picks the pose
        internal PoseClip Pose;
        internal float Phase, Age;
        internal float EyeForward = .06f, NeckBack;   // (ladders) the first-person eye towards the ladder, the neck tilted back, degrees
        internal float[] Rungs;                       // (ladders) rung heights (world, ascending): planted feet are bent / stretched onto them
        /// <summary>The player camera while the lease is active.</summary>
        public Transform Camera { get { return Active ? Core.PlayerState.PlayerCamera : null; } }
        /// <summary>The first-person eye following the head (no roll / pitch) while FollowHeadInFirstPerson.</summary>
        internal bool TryEye(out Vector3 eye)
        {
            eye = Vector3.zero;
            Transform head;
            if (!Active || !FollowHeadInFirstPerson || !Body.Bones.TryGetValue("mixamorig:Head", out head) || head == null) return false;
            // Follow position without the animation's roll/pitch: mouse look stays
            // under the player's control. The small eye offset keeps the lens forward
            // of the skull pivot when the character leans over the ledge.
            Vector3 target = head.position + Facing * new Vector3(0, .075f, EyeForward);
            eye = Core.PlayerState.PlayerCamera != null ? Vector3.Lerp(Core.PlayerState.PlayerCamera.position, target, Mathf.Clamp01(Seconds / .08f)) : target;
            return true;
        }
        /// <summary>Sets the clip time and facing; false when the lease ended.</summary>
        public bool Sample(float seconds, Quaternion facing)
        {
            if (!Active) return false;
            Seconds = Mathf.Max(0, seconds); Facing = facing;
            return true;
        }
        /// <summary>Ends the lease on the body.</summary>
        public void Dispose() { if (Body != null && Body.Traversal == this) Body.EndTraversal(); }
    }
}
