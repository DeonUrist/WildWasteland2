using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using WildWasteland.Core;

namespace WildWasteland.Modules.Survival
{
    /// <summary>One camera-only copy of the player's appearance; no NPC, collider, tag, entity or save registration.</summary>
    internal sealed class DoppelgangerEpisode
    {
        private readonly Insanity _state;
        private readonly IModule _owner;
        private readonly Dictionary<Transform, Transform> _copies = new Dictionary<Transform, Transform>();
        private readonly List<Renderer> _visible = new List<Renderer>();
        private Transform _source, _root;
        private Transform[] _nodes;
        private Renderer[] _renderers;
        private Animator _sourceAnimator;
        private SkinnedMeshRenderer _full;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable _idle, _walk;
        private float _idleLength, _walkLength, _nextTry, _nextGround, _floor, _blend;
        private Vector3 _world;
        private int _node, _renderer;
        private bool _ready, _placed, _behind, _warned, _subscribed;
        internal DoppelgangerEpisode(Insanity state, IModule owner) { _state = state; _owner = owner; }
        internal void Init()
        {
            Scheduler.Sliced(_owner, "Survival.Doppelganger appearance", 500, Prepare);
            Scheduler.EveryFrame(_owner, "Survival.Doppelganger motion", Tick, Phase.LateUpdateLast);
        }
        private bool Running(out MentalBreakVisuals visuals)
        { return _state.TryGetVisuals(Entities.Player, out visuals) && visuals.Doppelganger && GameRefs.InGame && !SaveBus.Busy && Application.isFocused; }
        private bool Prepare()
        {
            MentalBreakVisuals visuals;
            if (!Running(out visuals) || _ready || Time.unscaledTime < _nextTry) return false;
            try
            {
                if (_root == null)
                {
                    var presentation = ModApi.Get<ICharacterPresentation>(); var animation = ModApi.Get<IAnimationApi>();
                    Transform source;
                    if (presentation == null || animation == null || !presentation.Ready || !presentation.TryGetRoot(visuals.Player, out source))
                    {
                        _nextTry = Time.unscaledTime + 1;
                        if (!_warned) { _warned = true; Log.Warn(_owner, "Doppelganger: waiting for the player's Presentation body and animation clips."); }
                        return false;
                    }
                    _sourceAnimator = source.GetComponentInChildren<Animator>(true);
                    _full = null;
                    foreach (var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (skin.shadowCastingMode == ShadowCastingMode.ShadowsOnly && skin.bones.Length >= 20) { _full = skin; break; }
                    if (_sourceAnimator == null || _sourceAnimator.avatar == null || !_sourceAnimator.avatar.isHuman || _full == null)
                        throw new InvalidOperationException("Player full body mesh or humanoid avatar is unavailable");
                    _source = source; _nodes = source.GetComponentsInChildren<Transform>(true); _renderers = source.GetComponentsInChildren<Renderer>(true);
                    _root = new GameObject("Survival.Doppelganger") { hideFlags = HideFlags.HideAndDontSave, layer = Layers.Default }.transform;
                    _root.SetParent(Runner.Host.transform, false); _root.localScale = source.lossyScale;
                    _copies[source] = _root; _node = 1; _renderer = 0;
                    return true;
                }
                if (_source == null || _sourceAnimator == null) { End(); return false; }
                if (_node < _nodes.Length)
                {
                    var source = _nodes[_node++];
                    if (source == null) return true;
                    Transform parent;
                    if (!_copies.TryGetValue(source.parent, out parent)) return true;
                    var copy = new GameObject(source.name) { hideFlags = HideFlags.HideAndDontSave, layer = Layers.Default }.transform;
                    copy.SetParent(parent, false); copy.localPosition = source.localPosition; copy.localRotation = source.localRotation; copy.localScale = source.localScale;
                    copy.gameObject.SetActive(source.gameObject.activeSelf); _copies[source] = copy;
                    return true;
                }
                if (_renderer < _renderers.Length) { CopyRenderer(_renderers[_renderer++]); return true; }
                BuildAnimation();
                _ready = true; _nodes = null; _renderers = null; _copies.Clear();
                Camera.onPreCull += BeforeCull; Camera.onPostRender += AfterRender; _subscribed = true;
                Log.Always(_owner, "Doppelganger: player's full body, skin and equipment copied; local-camera-only visual, no AI or physics registration.");
            }
            catch (Exception error) { End(); _nextTry = Time.unscaledTime + 2; Log.Error(_owner, "Doppelganger appearance could not be prepared", error); }
            return false;
        }
        private void CopyRenderer(Renderer source)
        {
            if (source == null || source != _full && (!source.enabled || !source.gameObject.activeInHierarchy)) return;
            Transform target; if (!_copies.TryGetValue(source.transform, out target)) return;
            Renderer renderer;
            var skin = source as SkinnedMeshRenderer;
            if (skin != null)
            {
                if (skin != _full && skin.bones.Length >= 20 || skin.sharedMesh == null) return;
                var bones = skin.bones; var mapped = new Transform[bones.Length];
                for (int i = 0; i < bones.Length; i++) if (bones[i] != null && !_copies.TryGetValue(bones[i], out mapped[i])) return;
                Transform rootBone = null; if (skin.rootBone != null) _copies.TryGetValue(skin.rootBone, out rootBone);
                var copy = target.gameObject.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = skin.sharedMesh; copy.bones = mapped; copy.rootBone = rootBone; copy.localBounds = skin.localBounds; copy.updateWhenOffscreen = true;
                renderer = copy;
            }
            else
            {
                var mesh = source as MeshRenderer; var filter = mesh != null ? mesh.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null) return;
                target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                renderer = target.gameObject.AddComponent<MeshRenderer>();
            }
            renderer.sharedMaterials = source.sharedMaterials; renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true; renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.forceRenderingOff = true; _visible.Add(renderer);
        }
        private void BuildAnimation()
        {
            var animation = ModApi.Get<IAnimationApi>();
            var idle = animation.GetClip("Idle") ?? animation.GetClip("RifleIdle");
            var walk = animation.GetClip("Walk");
            if (idle == null || walk == null) throw new InvalidOperationException("Player idle/walk clips are unavailable");
            var animator = _copies[_sourceAnimator.transform].gameObject.AddComponent<Animator>();
            animator.avatar = _sourceAnimator.avatar; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _graph = PlayableGraph.Create("Survival.Doppelganger locomotion"); _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _mixer = AnimationMixerPlayable.Create(_graph, 2); _idle = AnimationClipPlayable.Create(_graph, idle); _walk = AnimationClipPlayable.Create(_graph, walk);
            _idle.SetSpeed(0); _walk.SetSpeed(0); _idleLength = Mathf.Max(.01f, idle.length); _walkLength = Mathf.Max(.01f, walk.length);
            _graph.Connect(_idle, 0, _mixer, 0); _graph.Connect(_walk, 0, _mixer, 1); _mixer.SetInputWeight(0, 1);
            AnimationPlayableOutput.Create(_graph, "local hallucination", animator).SetSourcePlayable(_mixer); _graph.Play();
        }
        private void Tick()
        {
            MentalBreakVisuals visuals;
            if (!_state.TryGetVisuals(Entities.Player, out visuals) || !visuals.Doppelganger)
            { if (_root != null || _warned || _nextTry > 0) End(); return; }
            if (!Running(out visuals)) { Hide(); return; }
            if (!_ready || _source == null || _root == null) { if (_ready) End(); return; }
            float dt = Mathf.Max(0, Time.deltaTime); if (dt <= 0) return;
            Vector3 forward = Vector3.ProjectOnPlane(_source.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            bool behind = visuals.Elapsed % 8 >= 6;
            Vector3 target = _source.position + (behind ? -forward * 2.2f : right * 1.6f + forward * .25f);
            bool teleport = _placed && behind && !_behind;
            Vector3 previous = _placed ? Origin.Scene(_world) : target;
            if (!_placed || teleport || (previous - target).sqrMagnitude > 144)
            {
                previous = target; _nextGround = 0;
                if (teleport) Log.Always(_owner, "Doppelganger: teleported behind the affected player.");
            }
            float speed = Mathf.Clamp(PlayerState.Velocity.magnitude + 1.5f, 1.5f, 9);
            Vector3 position = Vector3.MoveTowards(previous, target, speed * dt);
            if (Time.time >= _nextGround)
            {
                _nextGround = Time.time + .2f; RaycastHit hit;
                _floor = Physics.Raycast(position + Vector3.up * 2, Vector3.down, out hit, 6, Layers.Standable, QueryTriggerInteraction.Ignore) ? hit.point.y : target.y;
            }
            position.y = _floor;
            Vector3 velocity = Vector3.ProjectOnPlane(position - previous, Vector3.up) / dt;
            _world = Origin.World(position); _root.position = position;
            Vector3 facing = velocity.sqrMagnitude > .04f ? velocity.normalized : forward;
            _root.rotation = Quaternion.Slerp(_root.rotation, Quaternion.LookRotation(facing, Vector3.up), Mathf.Clamp01(dt * 10));
            _blend = Mathf.MoveTowards(_blend, Mathf.Clamp01(velocity.magnitude / 1.6f), dt * 5);
            _idle.SetTime(visuals.Elapsed % _idleLength); _walk.SetTime(visuals.Elapsed % _walkLength);
            _mixer.SetInputWeight(0, 1 - _blend); _mixer.SetInputWeight(1, _blend); _graph.Evaluate(0);
            _placed = true; _behind = behind;
        }
        private void BeforeCull(Camera camera)
        {
            MentalBreakVisuals visuals;
            bool show = _ready && _placed && camera == GameRefs.MainCamera && Running(out visuals);
            foreach (var renderer in _visible) if (renderer != null) renderer.forceRenderingOff = !show;
        }
        private void AfterRender(Camera camera) { Hide(); }
        private void Hide() { foreach (var renderer in _visible) if (renderer != null) renderer.forceRenderingOff = true; }
        internal void End()
        {
            if (_subscribed) { Camera.onPreCull -= BeforeCull; Camera.onPostRender -= AfterRender; _subscribed = false; }
            Hide(); if (_graph.IsValid()) _graph.Destroy();
            if (_root != null) { _root.gameObject.SetActive(false); UnityEngine.Object.Destroy(_root.gameObject); }
            if (_ready) Log.Always(_owner, "Doppelganger: local hallucination removed.");
            _source = _root = null; _sourceAnimator = null; _full = null; _nodes = null; _renderers = null;
            _copies.Clear(); _visible.Clear(); _ready = _placed = _behind = _warned = false; _node = _renderer = 0;
            _nextTry = _nextGround = _floor = _blend = 0;
        }
    }
}
