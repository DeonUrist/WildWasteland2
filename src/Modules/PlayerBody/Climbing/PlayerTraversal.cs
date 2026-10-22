using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace WildWasteland.Modules.PlayerBody
{
    // A lease on the player's animation only. The caller owns collision detection,
    // physical movement and temporarily suspending the game's movement FSMs.
    public sealed class PlayerTraversal : IDisposable
    {
        internal readonly Body Body;
        internal float Seconds;
        internal Quaternion Facing;
        internal PlayerTraversal(Body body, Quaternion facing) { Body = body; Facing = facing; }
        public bool Active { get { return Body != null && Body.Alive && BodyRunner.PlayerBody == Body && Body.Traversal == this; } }
        public bool StowWeapon { get; set; }
        public bool FollowHeadInFirstPerson { get; set; }
        // (WW ladders) a looping pose animation instead of a clip: Seconds is unused, Phase (cycles, wrapping) picks the pose
        internal PoseClip Pose;
        internal float Phase, Age;
        internal float EyeForward = .06f, NeckBack;   // (ladders) the first-person eye towards the ladder, the neck tilted back, degrees
        internal float[] Rungs;                       // (ladders) rung heights (world, ascending): planted feet are bent / stretched onto them
        public Transform Camera { get { return Active ? Game.PlayerCamera : null; } }
        internal bool TryEye(out Vector3 eye)
        {
            eye = Vector3.zero;
            Transform head;
            if (!Active || !FollowHeadInFirstPerson || !Body.Bones.TryGetValue("mixamorig:Head", out head) || head == null) return false;
            // Follow position without the animation's roll/pitch: mouse look stays
            // under the player's control. The small eye offset keeps the lens forward
            // of the skull pivot when the character leans over the ledge.
            Vector3 target = head.position + Facing * new Vector3(0, .075f, EyeForward);
            eye = Game.PlayerCamera != null ? Vector3.Lerp(Game.PlayerCamera.position, target, Mathf.Clamp01(Seconds / .08f)) : target;
            return true;
        }
        public bool Sample(float seconds, Quaternion facing)
        {
            if (!Active) return false;
            Seconds = Mathf.Max(0, seconds); Facing = facing;
            return true;
        }
        public void Dispose() { if (Body != null && Body.Traversal == this) Body.EndTraversal(); }
    }

    public static partial class ModAPI
    {
        public static GameObject PlayerRoot { get { return Game.Ready ? Game.Player : null; } }
        public static bool PlayerCanTraverse
        {
            get { return PlayerBodyModule.Running && Game.Ready && !Game.Dead && !Game.Paused && !Game.InCar
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

    internal sealed partial class Body
    {
        internal PlayerTraversal Traversal;
        private AnimationMixerPlayable _traversalMix;
        private AnimationClipPlayable _traversalClip;
        private AnimationPlayableOutput _traversalOutput;
        private float _traversalLength;
        private HumanPoseHandler _poseHandler;
        private HumanPose _humanPose;

        // (WW ladders) the ordinary layers keep playing underneath; LateTraversal writes the pose animation over them
        internal PlayerTraversal BeginPoseTraversal(PoseClip pose, Quaternion facing)
        {
            if (!_graph.IsValid() || _animator == null || !_animator.isHuman || _animator.avatar == null) return null;
            try { _poseHandler = new HumanPoseHandler(_animator.avatar, _anim); }
            catch (Exception e) { PlayerBodyModule.Error("Pose traversal: " + e.Message); _poseHandler = null; return null; }
            _sway = Vector3.zero;
            Traversal = new PlayerTraversal(this, facing) { Pose = pose };
            return Traversal;
        }

        internal PlayerTraversal BeginTraversal(AnimationClip clip, Quaternion facing)
        {
            if (!_graph.IsValid() || _animator == null || !_animator.isHuman) return null;
            _traversalOutput = (AnimationPlayableOutput)_graph.GetOutput(0);
            _traversalMix = AnimationMixerPlayable.Create(_graph, 2);
            _traversalClip = AnimationClipPlayable.Create(_graph, clip);
            _traversalClip.SetApplyFootIK(false);
            _traversalClip.SetApplyPlayableIK(false);
            _traversalClip.SetSpeed(0);
            _graph.Connect(_layers, 0, _traversalMix, 0);
            _graph.Connect(_traversalClip, 0, _traversalMix, 1);
            _traversalMix.SetInputWeight(0, 1);
            _traversalMix.SetInputWeight(1, 0);
            _traversalOutput.SetSourcePlayable(_traversalMix);
            _traversalLength = clip.length;
            _sway = Vector3.zero;
            Traversal = new PlayerTraversal(this, facing);
            return Traversal;
        }

        private bool LateTraversal(View view)
        {
            if (Traversal == null) return false;
            if (Game.Paused) return true;
            if (!ModAPI.PlayerCanTraverse) { EndTraversal(); return false; }
            var player = Game.Player.transform;
            if (Traversal.Pose != null)
            {
                _animator.enabled = true;
                Root.transform.SetPositionAndRotation(player.position - Vector3.up * (Game.StandingHeight * .5f), Traversal.Facing);
                Root.transform.localScale = Vector3.one;
                _anim.localPosition = Vector3.zero;
                _anim.localRotation = Quaternion.identity;
                _graph.Evaluate(0);
                if (_rootMotion != null) _rootMotion.Take();
                if (_poseHandler != null)
                {
                    Traversal.Age += Time.deltaTime;
                    _poseHandler.GetHumanPose(ref _humanPose);
                    Traversal.Pose.Blend(Traversal.Phase, Traversal.Age / .15f, ref _humanPose);
                    _poseHandler.SetHumanPose(ref _humanPose);
                    PoseExtras();
                }
                ShowProp(false);
                _lastView = view;
                CapturePose(null);
                return true;
            }
            float seconds = Mathf.Clamp(Traversal.Seconds, 0, _traversalLength);
            float weight = Mathf.Min(Mathf.Clamp01(seconds / .1f), Mathf.Clamp01((_traversalLength - seconds) / .12f));
            _traversalMix.SetInputWeight(0, 1 - weight);
            _traversalMix.SetInputWeight(1, weight);
            _traversalClip.SetTime(seconds);
            _animator.enabled = true;
            Root.transform.SetPositionAndRotation(player.position - Vector3.up * (Game.StandingHeight * .5f), Traversal.Facing);
            Root.transform.localScale = Vector3.one;
            _anim.localPosition = Vector3.zero;
            _anim.localRotation = Quaternion.identity;
            _graph.Evaluate(0);
            if (_rootMotion != null) _rootMotion.Take();
            ShowProp(false);
            _lastView = view;
            CapturePose(null);
            return true;
        }

        // after the pose: the neck tilted back (first person sees forward, not its own neck) and planted feet onto the real rungs
        private void PoseExtras()
        {
            var tr = Traversal;
            float w = Mathf.Clamp01(tr.Age / .15f);
            Transform neck;
            if (tr.NeckBack != 0f && Bones.TryGetValue("mixamorig:Neck", out neck) && neck != null)
                neck.rotation = Quaternion.AngleAxis(-tr.NeckBack * w, tr.Facing * Vector3.right) * neck.rotation;
            if (tr.Rungs == null || tr.Rungs.Length == 0 || !tr.Pose.HasContact) return;
            FootOnRung("Left", tr.Pose.Contact(tr.Phase, 0) * w, tr.Rungs);
            FootOnRung("Right", tr.Pose.Contact(tr.Phase, 1) * w, tr.Rungs);
        }

        private const float ToeAboveSole = .03f, RungReach = .3f;
        private void FootOnRung(string side, float weight, float[] rungs)
        {
            if (weight <= .01f) return;
            Transform up, knee, foot, toe;
            if (!Bones.TryGetValue("mixamorig:" + side + "UpLeg", out up) || !Bones.TryGetValue("mixamorig:" + side + "Leg", out knee)
                || !Bones.TryGetValue("mixamorig:" + side + "Foot", out foot) || !Bones.TryGetValue("mixamorig:" + side + "ToeBase", out toe)
                || up == null || knee == null || foot == null || toe == null) return;
            float sole = toe.position.y - ToeAboveSole, best = float.MaxValue;
            for (int i = 0; i < rungs.Length; i++) { float d = rungs[i] - sole; if (Mathf.Abs(d) < Mathf.Abs(best)) best = d; }
            if (Mathf.Abs(best) > RungReach) return;
            var keep = foot.rotation;
            TwoBone(up, knee, foot, foot.position + Vector3.up * (best * weight));
            foot.rotation = keep;
        }

        // two-bone IK: bend b so a..c spans the target distance (in the existing bend plane), then aim a at the target
        private static void TwoBone(Transform a, Transform b, Transform c, Vector3 target)
        {
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            float ab = (pb - pa).magnitude, bc = (pc - pb).magnitude;
            if (ab < 1e-4f || bc < 1e-4f) return;
            float d = Mathf.Clamp((target - pa).magnitude, Mathf.Abs(ab - bc) + 1e-3f, ab + bc - 1e-3f);
            float want = Mathf.Acos(Mathf.Clamp((ab * ab + bc * bc - d * d) / (2f * ab * bc), -1f, 1f)) * Mathf.Rad2Deg;
            float now = Vector3.Angle(pa - pb, pc - pb);
            Vector3 axis = Vector3.Cross(pa - pb, pc - pb);
            if (axis.sqrMagnitude < 1e-8f) axis = a.rotation * Vector3.right;
            axis.Normalize();
            // whichever turn direction actually reaches the wanted knee angle
            var r1 = Quaternion.AngleAxis(want - now, axis); var r2 = Quaternion.AngleAxis(now - want, axis);
            float e1 = Mathf.Abs(Vector3.Angle(pa - pb, r1 * (pc - pb)) - want), e2 = Mathf.Abs(Vector3.Angle(pa - pb, r2 * (pc - pb)) - want);
            b.rotation = (e1 <= e2 ? r1 : r2) * b.rotation;
            a.rotation = Quaternion.FromToRotation(c.position - pa, target - pa) * a.rotation;
        }

        internal void EndTraversal()
        {
            bool pose = Traversal != null && Traversal.Pose != null;
            Traversal = null;
            if (_poseHandler != null) { _poseHandler.Dispose(); _poseHandler = null; }
            if (pose) { _snap = true; StartPoseFade(.15f, false); return; }
            if (_graph.IsValid() && _traversalMix.IsValid())
            {
                _traversalOutput.SetSourcePlayable(_layers);
                _graph.Disconnect(_traversalMix, 0);
                _graph.Disconnect(_traversalMix, 1);
                _traversalMix.Destroy();
                if (_traversalClip.IsValid()) _traversalClip.Destroy();
                _snap = true;
                StartPoseFade(.08f, false);
            }
        }
    }
}
