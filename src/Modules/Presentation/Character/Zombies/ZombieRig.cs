using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>Humanoid zombie clips, reversible pose fades, and upper-body punches over a continuous running stride.</summary>
    internal sealed class ZombieRig : IDisposable
    {
        static readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
        static readonly string[] Required = { "Idle", "Walk", "Run", "Crawl", "CrawlRun", "Attack", "NeckBite", "Bite", "Bite2", "Agony", "Scream", "GetUp1", "GetUp2", "GetUp3" };
        static AssetBundle _bundle;
        static bool _tried;
        readonly Animator _animator;
        readonly Transform _leftLeg, _rightLeg;
        readonly bool _rootMotion;
        readonly AnimatorCullingMode _culling;
        readonly float _speed;
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        AnimationLayerMixerPlayable _layers;
        AnimationPlayableOutput _output;
        AnimationClipPlayable _current, _previous, _punch;
        float _previousTime, _blend, _runTime, _punchWeight;
        string _playing;
        bool _disposed, _punchWanted, _suspended;

        internal static bool Ready { get { return Clips.Count == Required.Length; } }
        internal bool Valid { get { return !_disposed && _animator != null && _graph.IsValid(); } }
        internal static float Length(string name) { AnimationClip clip; return Clips.TryGetValue(name, out clip) ? clip.length : 0f; }

        /// <summary>Retains one bundle across scenes/module restarts, like the canine animation bundle.</summary>
        internal static void Load()
        {
            if (_tried) return;
            _tried = true;
            string path = PresentationModule.AssetPath("Models/zombie-anims-v3.bundle");
            try
            {
                if (!File.Exists(path)) throw new FileNotFoundException("Zombie animation bundle absent", path);
                _bundle = AssetBundle.LoadFromFile(path);
                if (_bundle == null) throw new InvalidDataException("Unity could not open the zombie bundle");
                foreach (var clip in _bundle.LoadAllAssets<AnimationClip>())
                    if (clip != null && clip.humanMotion) Clips[clip.name] = clip;
                foreach (string name in Required) if (!Clips.ContainsKey(name)) throw new InvalidDataException("Missing humanoid zombie clip: " + name);
                Log.Main(PresentationModule.Instance, "Zombie animations: loaded 14 humanoid clips; running-leg punches and three resting styles; native damage and ragdolls retained.");
            }
            catch (Exception e) { Clips.Clear(); Log.Warn(PresentationModule.Instance, "Zombie animations unavailable; native animations retained: " + e.Message); }
        }

        internal ZombieRig(Animator animator)
        {
            _animator = animator; _rootMotion = animator.applyRootMotion; _culling = animator.cullingMode; _speed = animator.speed;
            _leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg); _rightLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            try
            {
                _graph = PlayableGraph.Create("WW.Zombie");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                _mixer = AnimationMixerPlayable.Create(_graph, 2);
                _layers = AnimationLayerMixerPlayable.Create(_graph, 2);
                _graph.Connect(_mixer, 0, _layers, 0); _layers.SetInputWeight(0, 1f);
                _punch = AnimationClipPlayable.Create(_graph, Clips["Attack"]);
                _punch.SetApplyFootIK(false); _punch.SetApplyPlayableIK(false); _punch.SetSpeed(0);
                _graph.Connect(_punch, 0, _layers, 1);
                _layers.SetLayerMaskFromAvatarMask(1, CharacterRig.UpperMask());
                _output = AnimationPlayableOutput.Create(_graph, "zombie", animator);
                _output.SetSourcePlayable(_layers);
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.speed = 1f;
                _graph.Play();
            }
            catch { Dispose(); throw; }
        }

        /// <summary>Changes clip with a short pose fade; time may run forward, freeze, or reverse for lie-down.</summary>
        internal void Play(string name, float seconds, float fade)
        {
            if (!Valid || _suspended) return;
            _punchWanted = name == "Attack";
            if (_punchWanted) { _punch.SetTime(seconds); name = "Run"; }
            if (name == "Run") seconds = _runTime;
            if (_playing == name) { _current.SetTime(seconds); return; }
            if (_previous.IsValid()) { _mixer.DisconnectInput(1); _graph.DestroyPlayable(_previous); }
            _previous = _current;
            if (_previous.IsValid())
            {
                _previousTime = (float)_previous.GetTime();
                _mixer.DisconnectInput(0); _graph.Connect(_previous, 0, _mixer, 1);
            }
            _current = AnimationClipPlayable.Create(_graph, Clips[name]);
            _current.SetApplyFootIK(false); _current.SetApplyPlayableIK(false); _current.SetSpeed(0); _current.SetTime(seconds);
            _graph.Connect(_current, 0, _mixer, 0);
            _playing = name; _blend = _previous.IsValid() ? fade : 0f;
        }

        /// <summary>Evaluates only the selected pose, preserving the previous pose during a transition.</summary>
        internal void Sample(float dt)
        {
            if (!Valid || _suspended || !_current.IsValid()) return;
            _blend = Mathf.Max(0f, _blend - dt);
            if (_playing == "Run") { _runTime = Mathf.Repeat(_runTime + dt, Clips["Run"].length); _current.SetTime(_runTime); }
            _punchWeight = Mathf.MoveTowards(_punchWeight, _punchWanted ? 1f : 0f, dt / .1f);
            _layers.SetInputWeight(1, _punchWeight);
            float old = Mathf.Clamp01(_blend / .18f);
            if (_previous.IsValid()) _previous.SetTime(_previousTime);
            _mixer.SetInputWeight(0, 1f - old); _mixer.SetInputWeight(1, old);
            _graph.Evaluate(0f);
            if (_playing == "Run")
            {
                Vector3 axis = _animator.transform.forward;
                if (_leftLeg != null) _leftLeg.rotation = Quaternion.AngleAxis(4f, axis) * _leftLeg.rotation;
                if (_rightLeg != null) _rightLeg.rotation = Quaternion.AngleAxis(-4f, axis) * _rightLeg.rotation;
            }
        }

        internal void Suspend(bool suspended)
        { if (!Valid) return; _suspended = suspended; _output.SetTarget(suspended ? null : _animator); }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_graph.IsValid()) _graph.Destroy();
            if (_animator != null)
            {
                _animator.applyRootMotion = _rootMotion; _animator.cullingMode = _culling; _animator.speed = _speed;
            }
        }
    }
}
