using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WildWasteland.Core;

using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    // (WW) Core.ModApi.Get<Core.IAnimationApi>().BeginNpcTraversal: ladder and ledge-climb animations on humanoid NPC rigs, for the NPC module's POI
    // navigation links. The NPC module owns the body's movement; this only animates.
    //   Ladder: the shared ClimbingLadder PoseClip written through a HumanPoseHandler after the Animator evaluated (LateUpdateLast).
    //   ClimbWaist / ClimbHigh: the player's climbing clips on a temporary PlayableGraph (the Rider.JumpClip precedent), time set by Phase.
    // A ModAPI character on that Animator is suspended for the lease (native controller underneath) and restored on Dispose.
    internal sealed class NpcTraversal : INpcTraversalAnimation
    {
        private static readonly List<NpcTraversal> _live = new List<NpcTraversal>();
        private readonly Animator _animator;
        private readonly ModAPI.Character _character;
        private readonly bool _wasSuspended, _rootMotion;
        private PoseClip _pose;
        private HumanPoseHandler _handler;
        private HumanPose _humanPose;
        private PlayableGraph _graph;
        private AnimationClipPlayable _clip;
        private float _length, _age;
        private bool _disposed;

        public bool Valid { get { return !_disposed && _animator != null; } }
        public float Phase { get; set; }
        /// <summary>Natural length of one cycle / of the climb clip, seconds.</summary>
        public float Length { get { return _length; } }

        /// <summary>Suspends the ModAPI character on the animator for the lease.</summary>
        private NpcTraversal(Animator animator)
        {
            _animator = animator;
            _rootMotion = animator.applyRootMotion;
            _character = ModAPI.Find(animator);
            if (_character != null) { _wasSuspended = _character.Suspended; _character.Suspended = true; }
        }

        /// <summary>Starts a ladder pose or climb clip lease on a humanoid NPC animator; null when not possible.</summary>
        internal static INpcTraversalAnimation Begin(Animator animator, string kind)
        {
            if (!PresentationModule.WorldRunning || animator == null || animator.avatar == null || !animator.avatar.isHuman) return null;
            try
            {
                if (kind == "Ladder")
                {
                    var pose = PoseClip.Shared("ClimbingLadder");
                    if (pose == null || !pose.Ready) return null;
                    var t = new NpcTraversal(animator) { _pose = pose, _length = pose.Seconds };
                    t._handler = new HumanPoseHandler(animator.avatar, animator.transform);
                    _live.Add(t);
                    return t;
                }
                var clip = ClimbingController.Clip(kind == "ClimbHigh");
                if (clip == null) return null;
                var c = new NpcTraversal(animator) { _length = clip.length };
                c._graph = PlayableGraph.Create("WW.NpcClimb");
                c._graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                var output = AnimationPlayableOutput.Create(c._graph, "climb", animator);
                c._clip = AnimationClipPlayable.Create(c._graph, clip);
                c._clip.SetApplyFootIK(false); c._clip.SetSpeed(0);
                output.SetSourcePlayable(c._clip);
                animator.applyRootMotion = false;
                c._graph.Play();
                _live.Add(c);
                return c;
            }
            catch (Exception e) { Log.Error(PresentationModule.WorldOwner, "NPC traversal animation: " + e.Message); return null; }
        }

        // LateUpdateLast: the ladder pose over whatever the Animator evaluated; climb clips follow Phase
        /// <summary>Job Presentation.NPC traversal poses (LateUpdateLast): writes every live lease pose; dead animators end their lease.</summary>
        internal static void LateTick()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var t = _live[i];
                if (t._disposed || t._animator == null || !t._animator.gameObject.activeInHierarchy) { t.Dispose(); continue; }
                try { t.Late(); }
                catch (Exception e) { Log.Error(PresentationModule.WorldOwner, "NPC traversal animation: " + e.Message); t.Dispose(); }
            }
        }

        /// <summary>One lease late update: ladder pose blend or climb clip time from Phase.</summary>
        private void Late()
        {
            if (Time.timeScale < .01f) return;
            _age += Time.deltaTime;
            if (_pose != null)
            {
                _handler.GetHumanPose(ref _humanPose);
                _pose.Blend(Phase, _age / .2f, ref _humanPose);
                _handler.SetHumanPose(ref _humanPose);
            }
            else if (_clip.IsValid()) _clip.SetTime(Mathf.Clamp01(Phase) * _length);
        }

        /// <summary>Ends the lease: pose handler / graph destroyed, root motion and the ModAPI character restored.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _live.Remove(this);
            if (_handler != null) { _handler.Dispose(); _handler = null; }
            if (_graph.IsValid()) _graph.Destroy();
            if (_animator != null) _animator.applyRootMotion = _rootMotion;
            if (_character != null && _character.Valid && !_wasSuspended) _character.Suspended = false;
        }

        /// <summary>Ends every lease.</summary>
        internal static void Clear() { for (int i = _live.Count - 1; i >= 0; i--) _live[i].Dispose(); _live.Clear(); }
    }
}
