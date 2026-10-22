using System;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
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

        /// <summary>Module start: crawl clip, the input/FSM hooks and the EarlyUpdate holster job.</summary>
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
            Log.Always(null,"[PlayerBody] Low Crawl ready: native Y stance, forward/back only, weapons holstered.");
        }
        /// <summary>Forgets the weapon FSMs and the cached state.</summary>
        internal static void Reset() { _camera = null; _weapons = _inHand = null; _holstered = false; _activeFrame = -1; }
        /// <summary>Job "Presentation.Crawling" (EarlyUpdate): keeps the weapon holstered while crawling.</summary>
        static void Tick()
        {
            if (!Game.Ready) { Reset(); return; }
            if (SaveBus.Busy) return;
            if (!Active) { _holstered = false; return; }
            Holster();
        }
        /// <summary>Holsters the drawn weapon once per crawl (Weapons FSM Activate, WeaponInHand Deactivate).</summary>
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
        /// <summary>FsmState enter postfix (every state entry; filter: the player's Movement FSM): re-evaluates Active and holsters on entering Crawling.</summary>
        static void Stance(Fsm fsm, FsmState state)
        {
            var movement = PlayerState.MovementFsm;
            if (movement == null || fsm != movement.Fsm) return;
            _activeFrame = -1;   // the stance changed: Active is evaluated again
            if (state.Name == "Crawling" && Game.Ready && !SaveBus.Busy) Holster();
        }
        /// <summary>GetButton prefix (every held-button action; filter: Active, "Fire", first-person rig): blocks firing while crawling.</summary>
        static bool HeldButton(GetButton action)
        {
            if (!Active || action.buttonName.Value != "Fire" || action.Owner == null || PlayerState.PlayerCamera == null || !action.Owner.transform.IsChildOf(PlayerState.PlayerCamera)) return true;
            if (action.storeResult != null && !action.storeResult.IsNone) action.storeResult.Value = false;
            return false;
        }
        /// <summary>SetVelocity prefix (filter: the player's Movement FSM, Active): no strafing while crawling.</summary>
        static bool Velocity(SetVelocity action)
        {
            if (PlayerState.MovementFsm != null && action.Fsm == PlayerState.MovementFsm.Fsm && Active && action.x != null) action.x.Value = 0f;
            return true;
        }
        /// <summary>GetButtonDown prefix (filter: Active): blocks jump/kick of the player and weapon/fire/reload/grenade buttons of the first-person rig.</summary>
        static bool Button(GetButtonDown action)
        {
            if (!Active) return true;
            var name = action.buttonName.Value;
            if (action.Owner == PlayerState.Player && (name == "Jump" || name == "Kick")) return false;
            if (name != "Weapon 1" && name != "Weapon 2" && name != "Weapon 3" && name != "Fire" && name != "Reload" && name != "Throw Grenade") return true;
            return action.Owner == null || PlayerState.PlayerCamera == null || !action.Owner.transform.IsChildOf(PlayerState.PlayerCamera);
        }
        /// <summary>Fsm.ProcessEvent prefix (every FSM event; filter: the two weapon FSMs by reference, then Active): drops weapon activation/switch events while crawling.</summary>
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
