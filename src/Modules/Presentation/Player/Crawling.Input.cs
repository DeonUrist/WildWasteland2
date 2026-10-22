using System;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Low Crawl, player side (split from PlayerBody/Animation/Crawling.cs in Wave A): while the Movement FSM is in "Crawling" the
    /// strafe velocity is zeroed, jump/kick/fire/weapon switching are blocked and the weapon is holstered. Target: Presentation player
    /// input (B05). The crawl clip itself is loaded by Crawling.LoadClip (Presentation/Character).
    /// </summary>
    internal static partial class Crawling
    {
        // (B05 perf, README 8 #1) asked by hooks on every GetButton/GetButtonDown/FSM event: evaluated once per frame, and again right
        // after the Movement FSM enters a state (Stance), so the crawl starts / ends blocking input in the same frame as before
        static int _activeFrame = -1; static bool _active;
        internal static bool Active
        {
            get
            {
                if (_activeFrame == Time.frameCount) return _active;
                _activeFrame = Time.frameCount;
                return _active = PresentationModule.Running && !PlayerState.InCar && !PlayerState.Dead && PlayerState.MoveState == "Crawling";
            }
        }
        static Transform _camera;
        static PlayMakerFSM _weapons, _inHand;
        static bool _holstered;

        internal static void Init(PresentationModule owner)
        {
            Reset();
            LoadClip();
            Hooks.SetVelocityDo.Prefix(owner, Velocity);
            Hooks.GetButtonDownUpdate.Prefix(owner, Button);
            Hooks.GetButtonDo.Prefix(owner, HeldButton);
            Hooks.ProcessEvent.Prefix(owner, WeaponEvent);
            Hooks.EnterState.Postfix(owner, Stance);
            Scheduler.EveryFrame(owner, "Presentation.Crawling", Tick, Phase.EarlyUpdate);
            Log.Always("[PlayerBody] Low Crawl ready: native Y stance, forward/back only, weapons holstered.");
        }
        internal static void Reset() { _camera = null; _weapons = _inHand = null; _holstered = false; _activeFrame = -1; }
        static void Tick()
        {
            if (!Game.Ready) { Reset(); return; }
            if (SaveBus.Busy) return;
            if (!Active) { _holstered = false; return; }
            Holster();
        }
        static void Holster()
        {
            if (_camera != PlayerState.PlayerCamera)
            {
                _camera = PlayerState.PlayerCamera; var weapons = _camera.Find("Weapons");
                _weapons = weapons != null ? FsmUtil.Find(weapons.gameObject, "Weapons") : null;
                _inHand = weapons != null ? FsmUtil.Find(weapons.gameObject, "WeaponInHand") : null;
                _holstered = false;
            }
            if (!_holstered && _weapons != null && _inHand != null)
            {
                _weapons.SendEvent("Activate"); _inHand.SendEvent("Deactivate"); _holstered = true;
            }
        }
        static void Stance(Fsm fsm, FsmState state)
        {
            var movement = PlayerState.MovementFsm;
            if (movement == null || fsm != movement.Fsm) return;
            _activeFrame = -1;   // the stance changed: Active is evaluated again
            if (state.Name == "Crawling" && Game.Ready && !SaveBus.Busy) Holster();
        }
        static bool HeldButton(GetButton action)
        {
            if (!Active || action.buttonName.Value != "Fire" || action.Owner == null || PlayerState.PlayerCamera == null || !action.Owner.transform.IsChildOf(PlayerState.PlayerCamera)) return true;
            if (action.storeResult != null && !action.storeResult.IsNone) action.storeResult.Value = false;
            return false;
        }
        static bool Velocity(SetVelocity action)
        {
            if (PlayerState.MovementFsm != null && action.Fsm == PlayerState.MovementFsm.Fsm && Active && action.x != null) action.x.Value = 0f;
            return true;
        }
        static bool Button(GetButtonDown action)
        {
            if (!Active) return true;
            var name = action.buttonName.Value;
            if (action.Owner == PlayerState.Player && (name == "Jump" || name == "Kick")) return false;
            if (name != "Weapon 1" && name != "Weapon 2" && name != "Weapon 3" && name != "Fire" && name != "Reload" && name != "Throw Grenade") return true;
            return action.Owner == null || PlayerState.PlayerCamera == null || !action.Owner.transform.IsChildOf(PlayerState.PlayerCamera);
        }
        static bool WeaponEvent(Fsm fsm, FsmEvent ev, FsmEventData data)
        {
            // (B05 perf) Fsm.ProcessEvent runs for every FSM event: only the two weapon FSMs matter, compared by reference first
            if (ev == null || (_inHand == null || fsm != _inHand.Fsm) && (_weapons == null || fsm != _weapons.Fsm) || !Active) return true;
            if (_inHand != null && fsm == _inHand.Fsm) return ev.Name != "Activate" && !ev.Name.StartsWith("Slot_", StringComparison.Ordinal);
            if (_weapons != null && fsm == _weapons.Fsm) return (ev.Name == "weapon off" || !ev.Name.StartsWith("weapon ", StringComparison.Ordinal)) && !ev.Name.StartsWith("Slot_", StringComparison.Ordinal);
            return true;
        }
    }
}
