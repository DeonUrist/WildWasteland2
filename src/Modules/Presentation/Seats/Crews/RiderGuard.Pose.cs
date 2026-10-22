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

namespace WildWasteland.Modules.Presentation
{
    /// <summary>Visual part of a human turret: PlayableGraph/animation-service clips (crouch, throw, jump, stand), lance prop offset, own weapons hidden, body facing; Presentation (Wave A split of Crews/Rider.cs).</summary>
    internal sealed class RiderVisual : IRiderVisual
    {
        private bool _disposed;
        private bool _rootMotion;
        private readonly Dictionary<Renderer,bool> _visibility=new Dictionary<Renderer,bool>();
        /// <summary>Reports whether the per-actor visual lease still belongs to a live actor.</summary>
        public bool Active=>!_disposed && _actor!=null;
        private readonly GameObject _actor;
        private readonly IRiderPresentationState _state;
        private readonly Animator _anim;
        private readonly bool _standing;
        private PlayableGraph _graph;private bool _graphOk;
        private AnimationMixerPlayable _mixer;private AnimationClipPlayable _idle,_throw,_jump,_stand;
        private object _body;private float _throwLen=1.2f,_jumpLen=1f;private readonly float[] _w=new float[4];
        private readonly List<Renderer> _ownWeapons=new List<Renderer>();
        public Transform Lance { get; private set; }
        /// <summary>Reads the live traffic request for a standing rider pose.</summary>
        private bool StandingPose { get { return _state.StandingPose; } }
        private string name { get { return _actor!=null?_actor.name:"rider"; } }
        /// <summary>Reports whether the common character driver or fallback graph can animate this rider.</summary>
        public bool Animated { get { return (_body is IAnimatedCharacter && ((IAnimatedCharacter)_body).Valid && !((IAnimatedCharacter)_body).Suspended)||_graphOk; } }
        /// <summary>Returns the active throw clip duration used by traffic release timing.</summary>
        public float ThrowSeconds { get { return _throwLen; } }
        /// <summary>Attaches the actual animation and visual lance independently of traffic private state.</summary>
        internal RiderVisual(GameObject actor,IRiderPresentationState state)
        {
            _actor=actor;_state=state;_standing=state.StandingPose;_anim=actor.GetComponentInChildren<Animator>();if(_anim!=null)_rootMotion=_anim.applyRootMotion;
            Transform left=RiderVisualHelpers.Bone(actor.transform,"LeftHand"),right=RiderVisualHelpers.Bone(actor.transform,"RightHand")??left;
            foreach(var hand in new[]{left,right})if(hand!=null)foreach(var renderer in hand.GetComponentsInChildren<Renderer>(true))_ownWeapons.Add(renderer);
            foreach(var renderer in _ownWeapons)if(renderer!=null)_visibility[renderer]=renderer.enabled;
            HideWeapons();BuildGraph();
            var prefab=PrefabCatalog.Find("blastlance_1");
            if(right!=null && prefab!=null)
            {
                var go=UnityEngine.Object.Instantiate(prefab,right.position,right.rotation);
                foreach(var joint in go.GetComponentsInChildren<Joint>(true))UnityEngine.Object.DestroyImmediate(joint);
                foreach(var body in go.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(body);
                foreach(var collider in go.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
                foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(script);
                go.name="RiderLance";foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=actor.layer;
                go.SetActive(true);go.transform.SetParent(right,false);Lance=go.transform;Offset();
            }
        }
        /// <summary>Ends all owned graphs and visuals before another presentation owner takes over.</summary>
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;
            RiderVisuals.Remove(this);
            if(_graph.IsValid())_graph.Destroy();_graphOk=false;
            (_body as IAnimatedCharacter)?.Dispose();_body=null;
            if(Lance!=null)UnityEngine.Object.Destroy(Lance.gameObject);Lance=null;
            if(_anim!=null)_anim.applyRootMotion=_rootMotion;
            foreach(var entry in _visibility)if(entry.Key!=null)entry.Key.enabled=entry.Value;_visibility.Clear();
        }
        /// <summary>Holds the fallback throw at the release pose; common character commands own their own clocks.</summary>
        public void PauseThrow() { if(_graphOk)_throw.Pause(); }
        /// <summary>Builds the rider animation: the animation-service body when available, else its own PlayableGraph (crouch / idle, throw, jump, stand).</summary>
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
                var idle = _standing ? RiderVisualHelpers.Clip("Idle") ?? RiderVisualHelpers.Clip("RifleIdle") : RiderVisualHelpers.Clip("CrouchIdle") ?? RiderVisualHelpers.Clip("RifleCrouchIdle") ?? RiderVisualHelpers.Clip("Idle");
                var thr = RiderVisualHelpers.Clip("Throw");
                var jump = RiderVisualHelpers.Clip("Jump");
                var stand = RiderVisualHelpers.Clip("Idle");
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
        /// <summary>Starts a one-shot (1 = throw, 2 = jump) from the start.</summary>
        public void Play(int clip)
        {
            if (_body != null) { if (clip == 1) PlayerAnims.Throw(_body); else if (clip == 2) PlayerAnims.Jump(_body); return; }
            if (!_graphOk) return;
            if (clip == 1) { _throw.SetTime(0.0); _throw.Play(); }
            if (clip == 2) { _jump.SetTime(0.0); _jump.Play(); }
        }

        /// <summary>Blends towards the target clip over <paramref name="seconds"/> (animation-service body: its own blends).</summary>
        public void Blend(int target, float seconds)
        {
            // ModAPI: crouched while riding / throwing (the throw is on the upper body), standing (3), in the air (2) - its own blends
            if (_body != null) { PlayerAnims.Set(_body, !StandingPose&&(target==0||target==1), false, target == 2, 0f); return; }
            if (!_graphOk) return;
            float step = Time.deltaTime / Mathf.Max(0.01f, seconds);
            for (int i = 0; i < 4; i++) { float w = Mathf.MoveTowards(_w[i], i == target ? 1f : 0f, step); if (w == _w[i]) continue; _w[i] = w; _mixer.SetInputWeight(i, w); }   // compare-before-write
        }

        /// <summary>Places the lance prop in the throwing hand from the Turret pose settings.</summary>
        public void Offset()
        {
            if (Lance == null) return;
            Lance.localPosition = RiderVisualHelpers.Parse(SeatPoseSettings.RiderLanceOffset.Value, Vector3.zero);
            Lance.localRotation = Quaternion.Euler(RiderVisualHelpers.Parse(SeatPoseSettings.RiderLanceRotation.Value, Vector3.zero));
        }

        /// <summary>Hides the rider own weapon renderers (not the lance).</summary>
        public void HideWeapons()
        {
            foreach (var r in _ownWeapons) if (r != null && r.enabled && (Lance == null || !r.transform.IsChildOf(Lance))) r.enabled = false;
        }

    }
}
