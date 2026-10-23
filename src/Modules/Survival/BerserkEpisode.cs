using System;
using System.Collections.Generic;
using System.Globalization;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Survival
{
    /// <summary>Owns one random carried weapon through its native draw, fire, reload and drop paths.</summary>
    internal sealed class BerserkEpisode
    {
        private static readonly string[] Slots = { "Slot 1", "Slot 2", "Slot 3" };
        private readonly Insanity _state;
        private readonly IModule _owner;
        private readonly BerserkVoice _voice;
        private readonly BerserkHallucination _hallucination;
        private readonly List<PlayMakerFSM> _attacks = new List<PlayMakerFSM>();
        private readonly PlayMakerFSM[] _drops = new PlayMakerFSM[3];
        private PlayMakerFSM _switcher, _handler, _attack, _reload;
        private Transform _parent, _slot;
        private string _drawSlot;
        private GameObject _item;
        private FsmInt _ammo;
        private int _selected = -2;
        private float _remaining, _reloadRetry;
        private float _nextWeaponCheck;
        private bool _drawing, _drawReported;
        private bool Active { get { return _state.Active && _state.EventType == InsanityEventType.Berserk; } }
        internal BerserkEpisode(Insanity state, IModule owner) { _state = state; _owner = owner; _voice = new BerserkVoice(owner); _hallucination = new BerserkHallucination(state, owner); }
        internal void Init()
        {
            Hooks.ProcessEvent.Prefix(_owner, BeforeEvent);
            Hooks.GetButtonDownUpdate.Prefix(_owner, BeforeButton);
            _hallucination.Init();
        }
        internal void Maintain(PlayMakerFSM switcher, PlayMakerFSM handler, Transform parent)
        {
            if (!Active || !GameRefs.InGame || !Application.isFocused || SaveBus.Busy || switcher == null || handler == null || parent == null) return;
            _switcher = switcher; _handler = handler; _parent = parent;
            if (!_switcher.Fsm.Initialized || !_handler.Fsm.Initialized) return;
            _voice.Begin();
            if (_attacks.Count == 0) foreach (var fsm in parent.GetComponentsInChildren<PlayMakerFSM>(true))
                if (fsm.FsmName == "Attack" && fsm.gameObject.name != "kick") _attacks.Add(fsm);
            if (_selected < 0 && Time.unscaledTime >= _nextWeaponCheck)
            {
                _nextWeaponCheck = Time.unscaledTime + .25f;
                int weapon = ChooseWeapon();
                if (_selected == -2 || weapon >= 0) { _selected = weapon; _slot = null; _attack = _reload = null; _ammo = null; _drawReported = false; }
            }
            if (_slot == null)
            {
                if (_selected >= 0) { _drawSlot = Slots[_selected]; _slot = _switcher.transform.Find(_drawSlot); }
                else for (int i = 0; i < Slots.Length; i++)
                {
                    var holder = _switcher.transform.Find(Slots[i]);
                    if (holder != null && PlayerInventory.FirstItem(holder) == null) { _drawSlot = Slots[i]; _slot = holder; break; }
                }
                if (_slot == null) return;
                if (_selected >= 0 && _drops[_selected] == null) _drops[_selected] = FsmUtil.Find(_slot.gameObject, "DropWeapon");
                _item = PlayerInventory.FirstItem(_slot);
                if (_item == null) _selected = -1;
                Log.Always(_owner, "Berserk: selected " + (_selected < 0 ? "hands" : _drawSlot) + "; attacks every 3-5 gameplay seconds; drop remains available.");
            }
            Dropped();
            if (_slot == null) return;
            bool ready = _selected < 0 ? _handler.ActiveStateName == "hands" : _handler.FsmVariables.FindFsmGameObject("Item")?.Value == _item && !string.IsNullOrEmpty(_handler.FsmVariables.FindFsmString("weaponType")?.Value);
            if (_switcher.ActiveStateName != _drawSlot || !ready)
            {
                _drawing = true;
                try { _switcher.Fsm.SetState(_drawSlot); _switcher.Fsm.UpdateStateChanges(); _handler.Fsm.UpdateStateChanges(); }
                finally { _drawing = false; }
            }
            if (_attack == null || !_attack.gameObject.activeInHierarchy)
            {
                _attack = _reload = null; _ammo = null;
                if (_selected >= 0 && _handler.FsmVariables.FindFsmGameObject("Item")?.Value != _item) return;
                foreach (var fsm in _attacks) if (fsm != null && fsm.isActiveAndEnabled)
                { _attack = fsm; _reload = FsmUtil.Find(fsm.gameObject, "Reload"); _ammo = _reload != null ? _reload.FsmVariables.FindFsmInt("ammo_in_gun") : null; break; }
            }
            if (!_drawReported && _attack != null)
            { _drawReported = true; Log.Always(_owner, "Berserk: drawn " + _handler.ActiveStateName + "; native attack ready."); }
        }
        private int ChooseWeapon()
        {
            int selected = -1, count = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                var holder = _switcher.transform.Find(Slots[i]);
                if (_drops[i] == null && holder != null) _drops[i] = FsmUtil.Find(holder.gameObject, "DropWeapon");
                if (holder != null && PlayerInventory.FirstItem(holder) != null && UnityEngine.Random.Range(0, ++count) == 0) selected = i;
            }
            return selected;
        }
        private bool Dropped()
        {
            if (_selected < 0 || _slot == null) return _selected == -1;
            if (_item != null && _item.transform.parent == _slot) return false;
            _selected = ChooseWeapon(); _slot = null; _item = null; _drawSlot = null; _attack = _reload = null; _ammo = null;
            _drawReported = false;
            Log.Always(_owner, "Berserk: weapon dropped; drawing " + (_selected >= 0 ? Slots[_selected] : "hands; no carried weapon remains") + "."); return true;
        }
        internal void Tick()
        {
            if (!Active || !GameRefs.InGame || !Application.isFocused || PlayerState.Dead || SaveBus.Busy) return;
            _voice.Tick();
            _remaining = Mathf.Max(0, _remaining - Time.deltaTime); _reloadRetry = Mathf.Max(0, _reloadRetry - Time.deltaTime);
            Dropped();
            if (_attack == null || !_attack.isActiveAndEnabled || !_attack.Fsm.Initialized) return;
            if (_reload != null && _ammo != null && _ammo.Value <= 0)
            {
                if (_reload.isActiveAndEnabled && _reload.Fsm.Initialized && _reload.ActiveStateName == "idle" && _reloadRetry <= 0)
                { _reloadRetry = 3; _reload.SendEvent("next"); if (Log.L1) Log.Main(_owner, "Berserk: native reload requested."); }
                return;
            }
            if (_remaining > 0 || _attack.ActiveStateName != "on" || _reload != null && _reload.ActiveStateName != "idle") return;
            _remaining = UnityEngine.Random.Range(3f, 5f); _attack.SendEvent("next");
            if (Log.L1) Log.Main(_owner, "Berserk: native attack; next in " + _remaining.ToString("0.00", CultureInfo.InvariantCulture) + " gameplay seconds.");
        }
        private bool BeforeEvent(Fsm fsm, FsmEvent evt, FsmEventData data)
        {
            if (!Active || _drawing || evt == null) return true;
            if (_switcher != null && ReferenceEquals(fsm, _switcher.Fsm))
                return evt.Name == "FINISHED" || evt.Name == "Activate_2" || evt.Name == "Activate" && DropCleanup();
            if (_handler != null && ReferenceEquals(fsm, _handler.Fsm))
                return !evt.Name.StartsWith("Slot_", StringComparison.Ordinal) && (evt.Name != "Deactivate" || DropCleanup());
            return true;
        }
        private bool DropCleanup()
        { for (int i = 0; i < _drops.Length; i++) if (_drops[i] != null && _drops[i].ActiveStateName == "back") return true; return false; }
        private bool BeforeButton(GetButtonDown action)
        {
            if (!Active) return true;
            bool blocked = _switcher != null && ReferenceEquals(action.Fsm, _switcher.Fsm);
            if (!blocked) return true;
            if (action.storeResult != null && !action.storeResult.IsNone) action.storeResult.Value = false;
            return false;
        }
        internal void PauseSound() { _voice.Pause(!GameRefs.InGame || !Application.isFocused || PlayerState.Dead); _hallucination.Maintain(); }
        internal void SuspendHallucination() { _hallucination.End(); }
        internal string Record()
        {
            Dropped(); var culture = CultureInfo.InvariantCulture;
            return _selected.ToString(culture) + "\n" + _remaining.ToString("R", culture) + "\n" + _voice.Record();
        }
        internal void Restore(string[] values)
        {
            _selected = int.Parse(values[7], CultureInfo.InvariantCulture); _remaining = float.Parse(values[8], CultureInfo.InvariantCulture);
            if (_selected < -2 || _selected > 2 || float.IsNaN(_remaining) || float.IsInfinity(_remaining) || _remaining < 0 || _remaining > 5) throw new FormatException("Invalid Berserk weapon state");
            _voice.Restore(values[9], values[10]);
        }
        internal void End()
        {
            _hallucination.End();
            _voice.Stop(); _selected = -2; _remaining = _reloadRetry = _nextWeaponCheck = 0; _drawing = _drawReported = false;
            _switcher = _handler = _attack = _reload = null; _parent = _slot = null; _item = null; _ammo = null;
            _drawSlot = null;
            _attacks.Clear();
            Array.Clear(_drops, 0, _drops.Length);
        }
        internal void Clear() { End(); _voice.Clear(); _hallucination.Clear(); }
    }
}
