using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Survival
{
    /// <summary>Temporary native effect/input ownership and pooled positional hallucination audio.</summary>
    internal sealed class InsanityEpisode
    {
        private readonly Insanity _state;
        private readonly IModule _owner;
        internal readonly BerserkEpisode Berserk;
        private readonly DeliriumEpisode _delirium;
        private readonly MentalCameraVolumes _cameraVolumes;
        private readonly DoppelgangerEpisode _doppelganger;
        private PlayMakerFSM _alcohol, _add, _remove, _switcher, _handler;
        private PlayMakerFSM _sanityEffect;
        private FloatRemap _visualRemap;
        private SetProperty _visualWrite;
        private FsmFloat _visualInput;
        private bool _visualEnabled;
        private Transform _weaponsParent;
        private bool _frozen, _addEnabled, _removeEnabled, _vertigo, _alcoholEnabled, _holstering, _paused;
        private string _alcoholState;
        private FsmFloat _weight;
        private float _oldWeight, _nextSound;
        private readonly Dictionary<PlayMakerFSM, bool> _blocked = new Dictionary<PlayMakerFSM, bool>();
        private readonly List<GameObject> _models = new List<GameObject>();
        private readonly List<AudioClip> _sounds = new List<AudioClip>();
        private readonly HashSet<AudioClip> _knownSounds = new HashSet<AudioClip>();
        private readonly AudioSource[] _voices = new AudioSource[3];
        private readonly Vector3[] _soundWorld = new Vector3[3];
        private int _catalog = -1, _soundIndex, _voiceIndex, _lastSound = -1;
        internal InsanityEpisode(Insanity state, IModule owner) { _state = state; _owner = owner; Berserk = new BerserkEpisode(state, owner); _delirium = new DeliriumEpisode(state, owner); _cameraVolumes = new MentalCameraVolumes(state, owner); _doppelganger = new DoppelgangerEpisode(state, owner); }
        internal void Init()
        {
            Berserk.Init();
            _doppelganger.Init();
            WorldIndex.WatchName(_owner, "Alcohol_Effect", (t, n, c) => _alcohol = FsmUtil.Find(t.gameObject, "AlcoholEffect"));
            WorldIndex.WatchName(_owner, "Sanity_Effect", (t, n, c) => _sanityEffect = FsmUtil.Find(t.gameObject, "SanityEffect"));
            Hooks.ProcessEvent.Prefix(_owner, BeforeEvent);
            Hooks.GetButtonDownUpdate.Prefix(_owner, BeforeButton);
            Hooks.ActivateGameObjectEnter.Prefix(_owner, BeforeActivate);
            Hooks.FsmFloatAddEnter.Prefix(_owner, BeforeBuffer);
            Hooks.SetFsmFloatEnter.Prefix(_owner, BeforeBufferSet);
            Scheduler.EveryFrame(_owner, "Survival.Insanity ownership", Maintain, Phase.EarlyUpdate);
            Scheduler.EveryFrame(_owner, "Survival.Insanity final value", Maintain, Phase.LateUpdateLast);
            Scheduler.Sliced(_owner, "Survival.Insanity sound bank", 500, PrepareSound);
        }
        internal void Resolve()
        {
            if (!GameRefs.WorldActive) return;
            if (_add == null) _add = FsmUtil.Find(GameRefs.Player, "SanityAdd");
            if (_remove == null) _remove = FsmUtil.Find(GameRefs.Player, "SanityRemove");
            if (_handler == null || _switcher == null)
            {
                var camera = PlayerState.PlayerCamera;
                var weapons = camera != null ? camera.Find("HandItemUse/Weapons") ?? camera.Find("Weapons") : null;
                if (weapons != null) { _handler = FsmUtil.Find(weapons.gameObject, "WeaponInHand"); _switcher = FsmUtil.Find(weapons.gameObject, "Weapons"); }
            }
            if (_weaponsParent == null) _weaponsParent = PlayerState.WeaponsParent;
        }
        internal void Start() { Resolve(); _nextSound = Time.time; Maintain(); }
        private void Maintain()
        {
            _state.HoldInsanity();
            if (_state.LocksInsanity)
            {
                if (!_frozen && _add != null && _remove != null) { _addEnabled = _add.enabled; _removeEnabled = _remove.enabled; _frozen = true; }
                if (_frozen) { if (_add != null) _add.enabled = false; if (_remove != null) _remove.enabled = false; }
            }
            if (_state.Active && !PlayerState.Dead)
            {
                Visuals();
                if (_state.EventType == InsanityEventType.Vertigo) Vertigo();
                else if (_state.EventType == InsanityEventType.Berserk) Berserk.Maintain(_switcher, _handler, _weaponsParent);
                else if (_state.EventType == InsanityEventType.NoMoreDeath) NoMoreDeath();
            }
            _delirium.Maintain();
            Berserk.PauseSound();
            bool paused = !GameRefs.InGame;
            for (int i = 0; i < _voices.Length; i++) if (_voices[i] != null)
            {
                _voices[i].transform.position = Origin.Scene(_soundWorld[i]);
                if (paused != _paused) { if (paused) _voices[i].Pause(); else _voices[i].UnPause(); }
            }
            _paused = paused;
        }
        private void Visuals()
        {
            if (_sanityEffect == null) return;
            if (_visualRemap == null) foreach (var state in _sanityEffect.FsmStates) foreach (var action in state.Actions)
            {
                var write = action as SetProperty; if (write != null) _visualWrite = write;
                var remap = action as FloatRemap; if (remap == null || remap.theFloat.Name != "Sanity") continue;
                _visualRemap = remap; _visualInput = remap.theFloat; _visualEnabled = _sanityEffect.enabled; remap.theFloat = _state.EventType == InsanityEventType.Delirium ? 80f : 100f;
                Log.Always(_owner, _state.EventType == InsanityEventType.Delirium ? "Delirium: native insanity visual effect suppressed." : "Mental break visuals: native insanity effect at 100; alcohol effect remains independent.");
            }
            if (_visualRemap == null) return;
            _sanityEffect.enabled = true;
            if (!_sanityEffect.gameObject.activeSelf) _sanityEffect.gameObject.SetActive(true);
            _visualRemap.OnUpdate();
            _cameraVolumes.Bind(_visualWrite?.targetProperty.TargetObject.Value as Component, false);
            if (_state.EventType == InsanityEventType.Delirium) _visualWrite?.OnUpdate();
        }
        private void Vertigo()
        {
            if (_alcohol == null || !_alcohol.Fsm.Initialized) return;
            if (!_vertigo)
            {
                _weight = _alcohol.FsmVariables.FindFsmFloat("Weight"); if (_weight == null) return;
                _alcoholEnabled = _alcohol.enabled; _alcoholState = _alcohol.ActiveStateName; _oldWeight = _weight.Value; _vertigo = true;
                _alcohol.enabled = true; _weight.Value = 1; _alcohol.SendEvent("Activate");
                foreach (var state in _alcohol.FsmStates) foreach (var action in state.Actions)
                { var write = action as SetProperty; if (write != null) _cameraVolumes.Bind(write.targetProperty.TargetObject.Value as Component, true); }
            }
            _weight.Value = 1;
        }
        private void NoMoreDeath()
        {
            if (_weaponsParent == null || _switcher == null || _handler == null) return;
            if (_blocked.Count == 0)
            {
                Holster();
                foreach (var fsm in _weaponsParent.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (fsm.FsmName != "Attack" && fsm.FsmName != "Animation") continue;
                    if (fsm.gameObject.name == "kick" && fsm.FsmName == "Animation" && fsm.Fsm.Initialized) fsm.Fsm.SetState("off");
                    _blocked[fsm] = fsm.enabled;
                    if (fsm.FsmName == "Attack" && fsm.gameObject.name != "kick" && !_models.Contains(fsm.gameObject)) _models.Add(fsm.gameObject);
                }
            }
            foreach (var pair in _blocked) if (pair.Key != null && pair.Key.enabled) pair.Key.enabled = false;
            if (_handler.ActiveStateName != "off" && _handler.ActiveStateName != "Deactivate") Holster();
            foreach (var model in _models) if (model != null && model.activeSelf) model.SetActive(false);
        }
        private void Holster()
        {
            if (_holstering || _switcher == null || !_switcher.Fsm.Initialized) return;
            _holstering = true;
            try { _switcher.Fsm.SetState("off 2"); }
            finally { _holstering = false; }
        }
        private bool BeforeEvent(Fsm fsm, FsmEvent evt, FsmEventData data)
        {
            if (evt == null) return true;
            if (_state.Active && _vertigo && _alcohol != null && ReferenceEquals(fsm, _alcohol.Fsm) && (evt.Name == "next" || evt.Name == "Deactivate")) return false;
            if (!_state.WeaponsBlocked || _holstering) return true;
            if (_handler != null && ReferenceEquals(fsm, _handler.Fsm)) return evt.Name == "Deactivate" || evt.Name == "FINISHED";
            if (_switcher != null && ReferenceEquals(fsm, _switcher.Fsm)) return evt.Name == "Activate" || evt.Name == "weapon off" || evt.Name == "de_Attach" || evt.Name == "FINISHED";
            return true;
        }
        private bool BeforeButton(GetButtonDown action)
        {
            if (!_state.WeaponsBlocked || _switcher == null || !ReferenceEquals(action.Fsm, _switcher.Fsm)) return true;
            if (action.storeResult != null && !action.storeResult.IsNone) action.storeResult.Value = false;
            action.Finish(); return false;
        }
        private bool BeforeActivate(ActivateGameObject action)
        {
            if (!_state.WeaponsBlocked || !action.activate.Value || _weaponsParent == null) return true;
            var target = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
            if (target == null || !target.transform.IsChildOf(_weaponsParent)) return true;
            action.Finish(); return false;
        }
        private bool BeforeBuffer(FsmFloatAdd action)
        {
            if (!_state.LocksInsanity || action.fsmName.Value != "SanityAdd" && action.fsmName.Value != "SanityRemove" || action.Fsm.GetOwnerDefaultTarget(action.gameObject) != GameRefs.Player) return true;
            action.Finish(); return false;
        }
        private bool BeforeBufferSet(SetFsmFloat action)
        {
            if (!_state.LocksInsanity || action.fsmName.Value != "SanityAdd" && action.fsmName.Value != "SanityRemove" || action.Fsm.GetOwnerDefaultTarget(action.gameObject) != GameRefs.Player) return true;
            action.Finish(); return false;
        }
        private bool PrepareSound()
        {
            if (!GameRefs.WorldActive || !PrefabCatalog.Ready) return false;
            if (_catalog != PrefabCatalog.Generation) { _catalog = PrefabCatalog.Generation; _soundIndex = 0; _sounds.Clear(); _knownSounds.Clear(); _lastSound = -1; }
            var fsms = PrefabCatalog.AssetFsms;
            if (_soundIndex >= fsms.Count) return false;
            var fsm = fsms[_soundIndex++]; bool monster = false;
            for (var t = fsm != null ? fsm.transform : null; t != null; t = t.parent) if (t.CompareTag("Mutant") || t.CompareTag("MutantSanity")) { monster = true; break; }
            if (!monster) return SoundsRemain(fsms.Count);
            foreach (var state in fsm.FsmStates) foreach (var action in state.Actions)
            {
                var clip = action as SetAudioClip; if (clip != null) AddSound(clip.audioClip?.Value);
                var sound = action as PlaySound; if (sound != null) AddSound(sound.clip?.Value);
                var random = action as PlayRandomSound; if (random?.audioClips != null) foreach (var item in random.audioClips) AddSound(item?.Value);
            }
            foreach (var array in fsm.FsmVariables.ArrayVariables) if (array.Values != null) foreach (var value in array.Values) AddSound(value as AudioClip);
            return SoundsRemain(fsms.Count);
        }
        private bool SoundsRemain(int count)
        { if (_soundIndex == count) Log.Always(_owner, "Insanity sound bank ready: " + _sounds.Count + " creature clips."); return _soundIndex < count; }
        private void AddSound(Object value)
        { var clip = value as AudioClip; if (clip != null && _knownSounds.Add(clip)) _sounds.Add(clip); }
        internal void Tick()
        {
            Berserk.Tick();
            var player = GameRefs.Player;
            if (player == null || _sounds.Count == 0 || Time.time < _nextSound) return;
            _nextSound = Time.time + Random.Range(3f, 6f);
            int sound = Random.Range(0, _sounds.Count); if (_sounds.Count > 1 && sound == _lastSound) sound = (sound + 1) % _sounds.Count;
            _lastSound = sound;
            int index = _voiceIndex++ % _voices.Length; var source = _voices[index];
            if (source == null)
            {
                var go = new GameObject("Survival.InsanitySound"); go.transform.SetParent(Runner.Host.transform, false);
                source = go.AddComponent<AudioSource>(); _voices[index] = source; source.playOnAwake = false; source.loop = false; source.spatialBlend = 1;
                source.volume = .75f; source.minDistance = 5; source.maxDistance = 50; source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0;
            }
            float angle = Random.Range(0, Mathf.PI * 2), radius = Random.Range(8f, 22f);
            Vector3 at = player.transform.position + new Vector3(Mathf.Cos(angle) * radius, Random.Range(0f, 3f), Mathf.Sin(angle) * radius);
            _soundWorld[index] = Origin.World(at); source.transform.position = at; source.clip = _sounds[sound]; source.Play();
        }
        internal void End()
        {
            _cameraVolumes.End();
            _doppelganger.End();
            _delirium.End();
            Berserk.End();
            foreach (var source in _voices) if (source != null) source.Stop();
            if (_visualRemap != null)
            {
                _visualRemap.theFloat = _visualInput;
                if (_sanityEffect != null)
                {
                    _visualRemap.OnUpdate();
                    foreach (var state in _sanityEffect.FsmStates) foreach (var action in state.Actions)
                        if (action is SetProperty || action is SetAudioVolume) action.OnUpdate();
                    _sanityEffect.gameObject.SetActive(_visualInput.Value > 80); _sanityEffect.enabled = _visualEnabled;
                }
                _visualRemap = null; _visualInput = null; _visualWrite = null;
            }
            if (_vertigo)
            {
                _vertigo = false;
                if (_alcohol != null) { _weight.Value = _oldWeight; _alcohol.Fsm.SetState(_alcoholState); _alcohol.enabled = _alcoholEnabled; }
                _weight = null;
            }
            foreach (var pair in _blocked) if (pair.Key != null) pair.Key.enabled = pair.Value;
            _blocked.Clear(); _models.Clear();
        }
        internal void ReleaseFreeze(bool discardBuffers)
        {
            if (!_frozen) return;
            if (discardBuffers)
            {
                if (_add != null) { _add.FsmVariables.GetFsmFloat("SanityAdd").Value = 0; _add.Fsm.SetState("off"); }
                if (_remove != null) { _remove.FsmVariables.GetFsmFloat("SanityRemove").Value = 0; _remove.Fsm.SetState("off"); }
            }
            if (_add != null) _add.enabled = _addEnabled; if (_remove != null) _remove.enabled = _removeEnabled; _frozen = false;
        }
        internal void Clear()
        {
            End(); ReleaseFreeze(false);
            Berserk.Clear();
            _delirium.Clear();
            foreach (var source in _voices) if (source != null) Object.Destroy(source.gameObject);
            System.Array.Clear(_voices, 0, _voices.Length);
            _alcohol = _add = _remove = _switcher = _handler = _sanityEffect = null; _weaponsParent = null;
            _sounds.Clear(); _knownSounds.Clear(); _catalog = -1; _voiceIndex = 0; _paused = false;
        }
    }
}
