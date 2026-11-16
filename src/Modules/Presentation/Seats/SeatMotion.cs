using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    // A visual lease only. Native player and crew code still own the seat, movement and collisions.
    internal sealed class SeatMotion : IDisposable
    {
        private const float BlendSeconds = .15f;
        private const float SeatPlayback = 1.8f; // WWW3 enterCarSpeed and exitCarSpeed
        private static readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
        private static bool _loadTried;
        private readonly Animator _animator;
        private readonly Transform _hips;
        private readonly Transform[] _bones = new Transform[(int)HumanBodyBones.LastBone];
        private readonly Quaternion[] _rest = new Quaternion[(int)HumanBodyBones.LastBone];
        private readonly float _duration;
        private readonly float _rate;
        private PlayableGraph _graph;
        private AnimationPlayableOutput _output;
        private AnimationClipPlayable _clip;
        private bool _ownsGraph, _rootMotion;
        private Vector3 _first, _last;
        private float _elapsed;
        internal bool Finished => _elapsed >= _duration;

        private SeatMotion(Animator animator, Transform hips, AnimationClip clip, float rate)
        {
            _animator = animator; _hips = hips; _duration = clip.length;
            _rate = rate;
            for (int i = 0; i < _bones.Length; i++) _bones[i] = animator.GetBoneTransform((HumanBodyBones)i);
        }

        internal static Quaternion Facing(Transform car, Vector3 velocity)
        {
            var forward = Vector3.ProjectOnPlane(velocity.sqrMagnitude > .01f ? velocity : car.forward, Vector3.up);
            return forward.sqrMagnitude > .01f ? Quaternion.LookRotation(forward) : Quaternion.Euler(0f, car.eulerAngles.y, 0f);
        }

        private static AnimationClip FindClip(string name)
        {
            if (!_loadTried)
            {
                _loadTried = true;
                var bundle = AssetBundle.LoadFromFile(DiskResources.FilePath("Presentation.Models.vehicle-seat-transitions.bundle"));
                if (bundle == null) return null;
                foreach (var clip in bundle.LoadAllAssets<AnimationClip>())
                    if (clip.humanMotion) Clips[clip.name] = clip;
                bundle.Unload(false);
            }
            AnimationClip found;
            return Clips.TryGetValue(name, out found) ? found : null;
        }

        internal static SeatMotion Start(Animator animator, bool enter, bool passenger, bool roll, PlayableGraph graph = default, float rollRate = 1f)
        {
            if (animator == null || !animator.isHuman) return null;
            SeatMotion motion = null;
            try
            {
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var clip = FindClip(roll ? "JumpOutRoll" : enter
                    ? (passenger ? "Anim_CarEnterPassenger03" : "Anim_CarEnterDriver03")
                    : (passenger ? "Anim_CarExitPassenger03" : "Anim_CarExitDriver03"));
                if (hips == null || clip == null) return null;
                motion = new SeatMotion(animator, hips, clip, roll ? rollRate : SeatPlayback);
                motion._ownsGraph = !graph.IsValid();
                if (motion._ownsGraph)
                {
                    motion._rootMotion = animator.applyRootMotion;
                    if (motion._rootMotion) animator.applyRootMotion = false;
                    graph = PlayableGraph.Create("WW.VehicleSeat");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                }
                motion._graph = graph;
                motion._output = motion._ownsGraph ? AnimationPlayableOutput.Create(graph, "seat", animator)
                    : (AnimationPlayableOutput)graph.GetOutput(0);
                motion._clip = AnimationClipPlayable.Create(graph, clip);
                motion._clip.SetSpeed(0);
                motion._clip.SetApplyFootIK(false);
                motion._clip.SetApplyPlayableIK(false);
                if (motion._ownsGraph) graph.Play();
                var normalHips = hips.localPosition;
                motion.CaptureRotations();
                try
                {
                    motion._first = motion.Sample(0f);
                    motion._last = motion.Sample(Mathf.Max(0f, clip.length - .001f));
                }
                finally
                {
                    hips.localPosition = normalHips;
                    motion.BlendRotations(0f);
                }
                if (Log.L1) Log.Main(PresentationModule.Instance, "Vehicle seat animation: " + animator.name + " -> " + clip.name);
                return motion;
            }
            catch (Exception e)
            {
                motion?.Dispose();
                Log.Warn(PresentationModule.Instance, "Vehicle seat animation: " + e.Message);
                return null;
            }
        }

        private Vector3 Sample(float seconds)
        {
            var previous = _output.GetSourcePlayable();
            bool enabled = _animator.enabled;
            if (!enabled) _animator.enabled = true;
            _output.SetSourcePlayable(_clip);
            try
            {
                _clip.SetTime(seconds);
                _graph.Evaluate(0f);
                return _animator.transform.InverseTransformPoint(_hips.position);
            }
            finally
            {
                _output.SetSourcePlayable(previous);
                if (!enabled) _animator.enabled = false;
            }
        }

        internal bool Step(Vector3? from, Vector3 to, Quaternion facing, bool blendOut = true)
        {
            if (_animator == null || !_animator.gameObject.activeInHierarchy || !_graph.IsValid() || _elapsed >= _duration) return false;
            float phase = _elapsed / _duration;
            float blend = blendOut ? Mathf.Clamp01((_duration - _elapsed) / (_rate * BlendSeconds)) : 1f;
            if (blend < 1f) CaptureRotations();
            var hips = Sample(_elapsed);
            var rig = _animator.transform;
            var offset = facing * Vector3.Scale(hips - Vector3.Lerp(_first, _last, phase), rig.lossyScale);
            var start = from ?? to + facing * Vector3.Scale(_first - _last, rig.lossyScale);
            _hips.position = Vector3.Lerp(to, Vector3.Lerp(start, to, phase) + offset, blend);
            _hips.rotation = facing * Quaternion.Inverse(rig.rotation) * _hips.rotation;
            if (!blendOut && phase > .8f)
            {
                var left = _bones[(int)HumanBodyBones.LeftFoot];
                var right = _bones[(int)HumanBodyBones.RightFoot];
                if (left != null && right != null)
                {
                    var across = Vector3.ProjectOnPlane(right.position - left.position, Vector3.up);
                    var shift = across.normalized * (Mathf.Min(.025f, across.magnitude * .1f) * Mathf.InverseLerp(.8f, 1f, phase));
                    BringFootIn(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, shift);
                    BringFootIn(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, -shift);
                }
            }
            if (blend < 1f) BlendRotations(blend);
            _elapsed += Time.deltaTime * _rate;
            return true;
        }

        private void BringFootIn(HumanBodyBones thigh, HumanBodyBones knee, HumanBodyBones ankle, Vector3 offset)
        {
            var upper = _bones[(int)thigh]; var lower = _bones[(int)knee]; var foot = _bones[(int)ankle];
            if (upper == null || lower == null || foot == null) return;
            var rotation = foot.rotation;
            Body.TwoBone(upper, lower, foot, foot.position + offset);
            foot.rotation = rotation;
        }

        private void CaptureRotations()
        {
            for (int i = 0; i < _bones.Length; i++)
                if (_bones[i] != null) _rest[i] = _bones[i].localRotation;
        }

        private void BlendRotations(float weight)
        {
            for (int i = 0; i < _bones.Length; i++)
                if (_bones[i] != null) _bones[i].localRotation = Quaternion.Slerp(_rest[i], _bones[i].localRotation, weight);
        }

        public void Dispose()
        {
            if (_ownsGraph)
            {
                if (_graph.IsValid()) _graph.Destroy();
                if (_animator != null && _rootMotion) _animator.applyRootMotion = true;
            }
            else if (_graph.IsValid() && _clip.IsValid()) _graph.DestroyPlayable(_clip);
            _graph = default;
        }
    }
}
