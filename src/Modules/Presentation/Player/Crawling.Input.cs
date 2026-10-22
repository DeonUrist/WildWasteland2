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
        internal static bool Active { get { return PresentationModule.Running && !Game.InCar && !Game.Dead && Game.MoveState == "Crawling"; } }
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
        internal static void Reset() { _camera = null; _weapons = _inHand = null; _holstered = false; }
        static void Tick()
        {
            if (!Game.Ready) { Reset(); return; }
            if (SaveBus.Busy) return;
            if (!Active) { _holstered = false; return; }
            Holster();
        }
        static void Holster()
        {
            if (_camera != Game.PlayerCamera)
            {
                _camera = Game.PlayerCamera; var weapons = _camera.Find("Weapons");
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
        { if (Game.MovementFsm != null && fsm == Game.MovementFsm.Fsm && state.Name == "Crawling" && Game.Ready && !SaveBus.Busy) Holster(); }
        static bool HeldButton(GetButton action)
        {
            if (!Active || action.buttonName.Value != "Fire" || action.Owner == null || Game.PlayerCamera == null || !action.Owner.transform.IsChildOf(Game.PlayerCamera)) return true;
            if (action.storeResult != null && !action.storeResult.IsNone) action.storeResult.Value = false;
            return false;
        }
        static bool Velocity(SetVelocity action)
        {
            if (Game.MovementFsm != null && action.Fsm == Game.MovementFsm.Fsm && Active && action.x != null) action.x.Value = 0f;
            return true;
        }
        static bool Button(GetButtonDown action)
        {
            if (!Active) return true;
            var name = action.buttonName.Value;
            if (action.Owner == Game.Player && (name == "Jump" || name == "Kick")) return false;
            if (name != "Weapon 1" && name != "Weapon 2" && name != "Weapon 3" && name != "Fire" && name != "Reload" && name != "Throw Grenade") return true;
            return action.Owner == null || Game.PlayerCamera == null || !action.Owner.transform.IsChildOf(Game.PlayerCamera);
        }
        static bool WeaponEvent(Fsm fsm, FsmEvent ev, FsmEventData data)
        {
            if (!Active || ev == null) return true;
            if (_inHand != null && fsm == _inHand.Fsm) return ev.Name != "Activate" && !ev.Name.StartsWith("Slot_", StringComparison.Ordinal);
            if (_weapons != null && fsm == _weapons.Fsm) return (ev.Name == "weapon off" || !ev.Name.StartsWith("weapon ", StringComparison.Ordinal)) && !ev.Name.StartsWith("Slot_", StringComparison.Ordinal);
            return true;
        }
    }
}
