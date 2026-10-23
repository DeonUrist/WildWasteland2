using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.Rendering;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Survival
{
    /// <summary>Temporary Coyote hostility; player attacks retain the native remembered faction relation.</summary>
    internal sealed class BerserkHallucination
    {
        private sealed class Actor
        {
            internal GameObject Owner;
            internal PlayMakerFSM Enemy;
            internal bool Hostility, PrivateHostile;
            internal float NextInform;
        }
        private readonly Insanity _state;
        private readonly IModule _owner;
        private readonly Dictionary<GameObject, Actor> _actors = new Dictionary<GameObject, Actor>();
        private readonly List<GameObject> _coyotes = new List<GameObject>(), _near = new List<GameObject>();
        private readonly HashSet<GameObject> _knownCoyotes = new HashSet<GameObject>();
        private PlayMakerFSM _relation;
        private Camera _camera;
        private CommandBuffer _tint;
        private Material _material;
        private Mesh _screen;
        private int _scan;
        private float _nextScan;
        private bool Active { get { return _state.Active && _state.EventType == InsanityEventType.Berserk; } }
        private bool Running { get { return Active && GameRefs.InGame && !SaveBus.Busy && Application.isFocused && !PlayerState.Dead; } }
        internal BerserkHallucination(Insanity state, IModule owner) { _state = state; _owner = owner; }
        internal void Init()
        {
            ModEvents.Subscribe<NpcSpawned>(_owner, Spawned);
            Hooks.CreateObjectEnter.Postfix(_owner, Created);
            Scheduler.Sliced(_owner, "Survival.Berserk Coyotes", 500, Step);
        }
        private void Spawned(ref NpcSpawned spawned)
        { if (spawned.Faction == "Coyotes") AddCoyote(Entities.Find(spawned.Npc)); }
        private void Created(CreateObject action) { AddCoyote(action.storeObject != null ? action.storeObject.Value : null); }
        private void AddCoyote(GameObject npc)
        { if (npc != null && npc.CompareTag("Coyotes") && !_knownCoyotes.Contains(npc) && FsmUtil.Find(npc, "Health") != null && _knownCoyotes.Add(npc)) _coyotes.Add(npc); }
        private void RefreshCoyotes()
        {
            for (int i = _coyotes.Count - 1; i >= 0; i--) if (_coyotes[i] == null)
            { _knownCoyotes.Remove(_coyotes[i]); _coyotes.RemoveAt(i); }
            var player = GameRefs.Player; var control = ModApi.Get<INpcControl>();
            if (player == null || control == null) return;
            control.NpcsNear(player.transform.position, float.MaxValue, _near);
            foreach (var npc in _near) AddCoyote(npc);
            _near.Clear();
        }
        internal void Maintain()
        {
            if (!Active || PlayerState.Dead) { End(); return; }
            if (_relation == null) _relation = FsmUtil.Find(GameRefs.GameManager, "FactionRelation_Coyotes");
            if (!Running) { DetachTint(); return; }
            var camera = GameRefs.MainCamera;
            if (camera == null || _camera == camera) return;
            DetachTint();
            if (_material == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null || !shader.isSupported) { Log.Warn(_owner, "Berserk: red tint shader unavailable."); return; }
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); _material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                _material.SetInt("_Cull", (int)CullMode.Off); _material.SetInt("_ZWrite", 0); _material.SetInt("_ZTest", (int)CompareFunction.Always);
                _screen = new Mesh { name = "Survival.BerserkTint", hideFlags = HideFlags.HideAndDontSave };
                _screen.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
                MentalBreakVisuals visuals; _state.TryGetVisuals(Entities.Player, out visuals);
                var red = new Color(1f, .025f, .015f, visuals.RedTint); _screen.colors = new[] { red, red, red, red };
                _screen.triangles = new[] { 0,1,2,0,2,3 }; _screen.RecalculateBounds();
            }
            _tint = new CommandBuffer { name = "Wild Wasteland Berserk red tint" }; _camera = camera;
            camera.AddCommandBuffer(CameraEvent.AfterImageEffects, _tint); Camera.onPreRender += BeforeRender;
            Log.Always(_owner, "Berserk: red screen tint active; Coyotes temporarily attack with their original appearance; player attacks retain native hostility.");
        }
        private void BeforeRender(Camera camera)
        {
            if (camera != _camera) return;
            _tint.Clear(); if (!Running) return;
            _tint.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            _tint.DrawMesh(_screen, Matrix4x4.identity, _material);
            _tint.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
        }
        private bool FactionHostile()
        { return _relation != null && (_relation.ActiveStateName == "PlayerEnemy" || _relation.FsmVariables.FindFsmInt("OnOff")?.Value > 0); }
        private bool Step()
        {
            if (!Running || Time.unscaledTime < _nextScan) return false;
            if (_scan >= _coyotes.Count) { RefreshCoyotes(); _scan = 0; _nextScan = Time.unscaledTime + .25f; return false; }
            var root = _coyotes[_scan++];
            if (root != null && root.activeInHierarchy)
            {
                Actor actor;
                if (!_actors.TryGetValue(root, out actor)) { actor = new Actor { Owner = root }; _actors.Add(root, actor); }
                if (!actor.Hostility)
                {
                    actor.Enemy = FsmUtil.Find(actor.Owner, "PlayerIsEnemy");
                    actor.PrivateHostile = actor.Enemy != null && actor.Enemy.ActiveStateName == "PlayerEnemy" && !FactionHostile();
                    actor.Hostility = true; SetHostile(actor, true);
                    Log.Always(_owner, "Berserk: " + actor.Owner.name + " temporarily hostile; Coyote faction, weapons, health and loot retained.");
                }
                else if (actor.Enemy != null && actor.Enemy.ActiveStateName != "PlayerEnemy") SetHostile(actor, true);
                var player = GameRefs.Player;
                if (player != null && Time.time >= actor.NextInform && (root.transform.position - player.transform.position).sqrMagnitude <= 10000)
                { actor.NextInform = Time.time + 1; ModApi.Get<INpcApi>()?.Inform(actor.Owner, player); }
            }
            return true;
        }
        private void SetHostile(Actor actor, bool hostile)
        {
            var control = ModApi.Get<INpcControl>();
            if (control != null) control.SetPlayerHostile(actor.Owner, hostile);
            else if (actor.Enemy != null && actor.Enemy.Fsm.Initialized) actor.Enemy.Fsm.SetState(hostile ? "PlayerEnemy" : "PlayerFriendly");
        }
        private void DetachTint()
        {
            if (ReferenceEquals(_camera, null) && _tint == null) return;
            Camera.onPreRender -= BeforeRender;
            if (_camera != null && _tint != null) _camera.RemoveCommandBuffer(CameraEvent.AfterImageEffects, _tint);
            if (_tint != null) _tint.Release(); _tint = null; _camera = null;
        }
        internal void End()
        {
            DetachTint();
            foreach (var actor in _actors.Values)
            {
                if (actor.Owner != null && actor.Hostility) SetHostile(actor, actor.PrivateHostile || FactionHostile());
            }
            if (_actors.Count > 0) Log.Always(_owner, "Berserk: temporary Coyote aggression ended; native remembered hostility retained.");
            _actors.Clear(); _scan = 0; _nextScan = 0;
        }
        internal void Clear()
        { End(); _coyotes.Clear(); _knownCoyotes.Clear(); _near.Clear(); Object.Destroy(_material); Object.Destroy(_screen); _material = null; _screen = null; _relation = null; }
    }
}
