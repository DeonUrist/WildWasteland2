using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Visual part of a human turret: PlayableGraph/animation-service clips (crouch, throw, jump, stand), lance prop offset, own weapons hidden, body facing; Presentation (Wave A split of Crews/Rider.cs).</summary>
    internal sealed partial class RiderGuard
    {
        private void BuildGraph()
        {
            // 2.6.1: with Apocaplayer's ModAPI the player's own logic animates him: crouched (CrouchIdle), the lance thrown with ThrowRight on the
            // upper body over the crouched legs, standing up before a motorcycle leap, the player's Jump in the air
            if (_anim != null && _anim.isHuman && PlayerAnims.Available)
            {
                _body = PlayerAnims.Attach(_anim);
                if (_body != null)
                {
                    _throwLen = PlayerAnims.ThrowSeconds();
                    PlayerAnims.Set(_body, !StandingPose, false, false, 0f);
                    if (Log.L2) Log.Deep(PresentationModule.WorldOwner, "Rider: " + name + " animated by Apocaplayer's ModAPI ("+(StandingPose?"standing":"crouched")+", throw " + _throwLen.ToString("0.00") + " s)");
                    return;
                }
            }
            try
            {
                var idle = _standing ? Rider.Clip("Idle") ?? Rider.Clip("RifleIdle") : Rider.Clip("CrouchIdle") ?? Rider.Clip("RifleCrouchIdle") ?? Rider.Clip("Idle");
                var thr = Rider.Clip("Throw");
                var jump = Rider.Clip("Jump");
                var stand = Rider.Clip("Idle");
                if (_anim == null || idle == null) { if (_anim == null && Log.L2) Log.Deep(PresentationModule.WorldOwner, "Rider: no Animator on " + name); return; }
                if (!_anim.isHuman) { if (Log.L2) Log.Deep(PresentationModule.WorldOwner, "Rider: " + name + "'s Animator is not humanoid - Apocaplayer clips cannot drive it"); return; }
                _anim.applyRootMotion = false;
                _graph = PlayableGraph.Create("Apocapatrol.Rider");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                var output = AnimationPlayableOutput.Create(_graph, "rider", _anim);
                _mixer = AnimationMixerPlayable.Create(_graph, 4);
                _idle = AnimationClipPlayable.Create(_graph, idle);
                _throw = AnimationClipPlayable.Create(_graph, thr ?? idle);
                _jump = AnimationClipPlayable.Create(_graph, jump ?? idle);
                _stand = AnimationClipPlayable.Create(_graph, stand ?? idle);
                _throwLen = thr != null ? Mathf.Max(0.4f, thr.length) : 1.2f;
                _jumpLen = jump != null ? Mathf.Max(0.3f, jump.length) : 1f;
                _graph.Connect(_idle, 0, _mixer, 0); _graph.Connect(_throw, 0, _mixer, 1); _graph.Connect(_jump, 0, _mixer, 2); _graph.Connect(_stand, 0, _mixer, 3);
                _w[0] = 1f; _mixer.SetInputWeight(0, 1f); _mixer.SetInputWeight(1, 0f); _mixer.SetInputWeight(2, 0f); _mixer.SetInputWeight(3, 0f);
                _throw.Pause(); _jump.Pause();
                output.SetSourcePlayable(_mixer);
                _graph.Play();
                _graphOk = true;
                if (Log.L2) Log.Deep(PresentationModule.WorldOwner, "Rider: " + name + " animated with " + idle.name + (thr != null ? " + " + thr.name : " (no Throw)") + (jump != null ? " + " + jump.name : " (no Jump)"));
            }
            catch (Exception e) { Log.Warn(PresentationModule.WorldOwner, "Rider: animation graph: " + e.Message); _graphOk = false; }
        }

        // 0 = crouch idle, 1 = throw, 2 = jump (one-shots restart from 0), 3 = standing idle
        private void Play(int clip)
        {
            if (_body != null) { if (clip == 1) PlayerAnims.Throw(_body); else if (clip == 2) PlayerAnims.Jump(_body); return; }
            if (!_graphOk) return;
            if (clip == 1) { _throw.SetTime(0.0); _throw.Play(); }
            if (clip == 2) { _jump.SetTime(0.0); _jump.Play(); }
        }

        private void Blend(int target, float seconds)
        {
            // ModAPI: crouched while riding / throwing (the throw is on the upper body), standing (3), in the air (2) - its own blends
            if (_body != null) { PlayerAnims.Set(_body, !StandingPose&&(target==0||target==1), false, target == 2, 0f); return; }
            if (!_graphOk) return;
            float step = Time.deltaTime / Mathf.Max(0.01f, seconds);
            for (int i = 0; i < 4; i++) { float w = Mathf.MoveTowards(_w[i], i == target ? 1f : 0f, step); if (w == _w[i]) continue; _w[i] = w; _mixer.SetInputWeight(i, w); }   // compare-before-write
        }

        internal void ApplyLanceOffset()
        {
            if (Lance == null) return;
            Lance.localPosition = Rider.ParseV3(SeatPoseSettings.RiderLanceOffset.Value, Vector3.zero);
            Lance.localRotation = Quaternion.Euler(Rider.ParseV3(SeatPoseSettings.RiderLanceRotation.Value, Vector3.zero));
        }

        private void HideOwnWeapons()
        {
            foreach (var r in _ownWeapons) if (r != null && r.enabled && (Lance == null || !r.transform.IsChildOf(Lance))) r.enabled = false;
        }

        private void Face(Vector3 dir)
        {
            var up = _car != null ? _car.transform.up : Vector3.up;
            dir = Vector3.ProjectOnPlane(dir, up);
            if (dir.sqrMagnitude < 1e-4f) return;
            if (_bike && _car != null)
            {
                // on a motorcycle: at most BikeArc degrees off the nose
                var f = Vector3.ProjectOnPlane(_car.transform.forward, up);
                if (f.sqrMagnitude > 1e-4f) dir = Quaternion.AngleAxis(Mathf.Clamp(Vector3.SignedAngle(f, dir, up), -Rider.BikeArc, Rider.BikeArc), up) * f;
            }
            var want = Quaternion.LookRotation(dir.normalized, up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 240f * Time.deltaTime);
        }
    }
}
