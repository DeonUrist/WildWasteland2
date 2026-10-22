using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// Drives the unload sequence by replaying the weapon's own Reload FSM events.
    internal class Unloader
    {
        private enum Phase { Idle, WaitAnimFinished, WaitAnimStart, WaitAnimEnd }
        private Phase _phase = Phase.Idle;
        private PlayMakerFSM _reload;
        private GameObject _weapon;
        private PlayMakerFSM _ammoFsm;
        private string _caliber;
        private float _deadline;
        private bool _moved, _aborted;
        private int _rounds;
        private string _item;

        public bool Busy { get { return _phase != Phase.Idle; } }
        public string PhaseName { get { return _phase.ToString(); } }
        public string WeaponName { get { return _weapon != null ? _weapon.name : "-"; } }

        public void TryStart()
        {
            if (PerGunAmmo.Suspended) return;
            var reload = FindActiveReloadFsm();
            if (reload == null) { AmmoSettings.V("No active weapon Reload FSM in idle"); return; }
            var inGun = reload.FsmVariables.GetFsmInt("ammo_in_gun");
            if (inGun == null) { AmmoSettings.V("Reload FSM has no ammo_in_gun"); return; }
            if (inGun.Value <= 0) { AmmoSettings.V("Gun already empty"); return; }

            // Caliber variable name from the checkAmmo state's GetFsmInt action.
            string caliber = null;
            var check = FindState(reload, "checkAmmo");
            if (check != null)
                foreach (var a in check.Actions)
                {
                    var g = a as GetFsmInt;
                    if (g != null && g.fsmName != null && g.fsmName.Value == "Ammo") { caliber = g.variableName.Value; break; }
                }
            if (string.IsNullOrEmpty(caliber)) { Core.Log.Warn("[Guns] " + "Could not determine caliber for " + reload.gameObject.name); return; }

            var gm = GameRefs.GameManager;
            var ammoFsm = gm != null ? FsmUtil.Find(gm, "Ammo") : null;
            if (ammoFsm == null || ammoFsm.FsmVariables.GetFsmInt(caliber) == null) { Core.Log.Warn("[Guns] " + "Ammo FSM / variable " + caliber + " not found"); return; }

            // The state the game enters after checkAmmo passes: it sends Deactivate/Reloading/animation events.
            FsmState start = null;
            if (check != null)
                foreach (var t in check.Transitions) if (t.EventName == "next") { start = FindState(reload, t.ToState); break; }
            if (start == null) { Core.Log.Warn("[Guns] " + "No start state found for " + reload.gameObject.name); return; }

            _reload = reload; _weapon = reload.gameObject; _ammoFsm = ammoFsm; _caliber = caliber; _moved = false; _rounds = inGun.Value; _item = PerGunAmmo.CurrentItem;
            CombatEvents.UnloadStarted(_weapon.name, _item, _rounds);
            bool threePhase = false;
            foreach (var t in start.Transitions) if (t.EventName == "AnimStart") { threePhase = true; break; }
            if (Core.Log.Verbose) Core.Log.Info("[Guns] Unloading " + _weapon.name + ": " + inGun.Value + " x " + caliber + (threePhase ? " (3-phase)" : ""));

            ReplaySendEvents(start);
            _phase = threePhase ? Phase.WaitAnimStart : Phase.WaitAnimFinished;
            _deadline = Time.unscaledTime + AmmoSettings.PhaseTimeout.Value;
        }

        /// Called from the Harmony hook on Fsm.Event.
        public void OnFsmEvent(Fsm fsm, string evName)
        {
            if (_phase == Phase.Idle || fsm == null || fsm.GameObject != _weapon) return;
            switch (_phase)
            {
                case Phase.WaitAnimFinished: if (evName == "AnimFinished") { AmmoSettings.V("AnimFinished"); Finish(); } break;
                case Phase.WaitAnimStart: if (evName == "AnimStart") { AmmoSettings.V("AnimStart"); AfterAnimStart(); } break;
                case Phase.WaitAnimEnd: if (evName == "AnimEnd") { AmmoSettings.V("AnimEnd"); Finish(); } break;
            }
        }

        public void Tick()
        {
            if (_phase == Phase.Idle) return;
            if (_weapon == null || !_weapon.activeInHierarchy) { Core.Log.Warn("[Guns] Weapon vanished during unload; aborting"); _aborted = true; Finish(); return; }
            if (Time.unscaledTime < _deadline) return;
            Core.Log.Warn("[Guns] Unload phase " + _phase + " timed out; continuing");
            if (_phase == Phase.WaitAnimStart) AfterAnimStart(); else Finish();
        }

        private void AfterAnimStart()
        {
            MoveAmmo();
            // Replay the "empty shell" sound the game plays between the open and close animations.
            foreach (var st in _reload.FsmStates)
            {
                if (st.Name.IndexOf("shell", StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (var a in st.Actions)
                {
                    var ps = a as PlaySound;
                    if (ps == null || ps.clip == null) continue;
                    var clip = ps.clip.Value as AudioClip;
                    if (clip != null && EmptyGunSound.Before(ps)) AudioSource.PlayClipAtPoint(clip, _weapon.transform.position + ps.position.Value, ps.volume.Value);
                }
            }
            // State that waits for AnimEnd sends ReloadEnd (close animation).
            FsmState endState = null;
            foreach (var s in _reload.FsmStates)
            {
                foreach (var t in s.Transitions) if (t.EventName == "AnimEnd") { endState = s; break; }
                if (endState != null) break;
            }
            if (endState != null) ReplaySendEvents(endState);
            _phase = Phase.WaitAnimEnd;
            _deadline = Time.unscaledTime + AmmoSettings.PhaseTimeout.Value;
        }

        private void MoveAmmo()
        {
            if (_moved) return;
            _moved = true;
            var inGun = _reload.FsmVariables.GetFsmInt("ammo_in_gun");
            var store = _ammoFsm.FsmVariables.GetFsmInt(_caliber);
            if (inGun == null || store == null) return;
            int n = inGun.Value;
            var capVar = _ammoFsm.FsmVariables.GetFsmInt(_caliber + "_capacity");
            int cap = capVar != null ? capVar.Value : int.MaxValue;
            int space = Math.Max(0, cap - store.Value);
            int toStore = Math.Min(n, space);
            int leftover = n - toStore;

            store.Value += toStore;
            inGun.Value = leftover;
            if (leftover > 0)
            {
                if (AmmoBox.Drop(_caliber, leftover, _weapon.transform)) inGun.Value = 0;
                else Core.Log.Warn("[Guns] Could not spawn an ammo box; " + leftover + " rounds left in the gun");
            }
            var mirror = _reload.FsmVariables.GetFsmInt("ammo");
            if (mirror != null) mirror.Value = store.Value;
            if (inGun.Value == 0)
            {
                var attack = PerGunAmmo.Find(_weapon, "Attack");
                var hammer = attack == null ? null : attack.FsmVariables.GetFsmBool("hammer");
                if (hammer != null) hammer.Value = false;
            }
            if (Core.Log.Verbose) Core.Log.Info("[Guns] Returned " + toStore + " x " + _caliber + " to backpack (now " + store.Value + "/" + cap + ")" + (leftover > 0 ? ", " + leftover + " dropped as a box" : ""));
        }

        private void Finish()
        {
            MoveAmmo();
            _phase = Phase.Idle; // Event replay can synchronously re-enter this hook.
            CombatEvents.UnloadFinished(_weapon != null ? _weapon.name : null, _item, _rounds, !_aborted);
            _aborted = false;
            var idle = FindState(_reload, "idle");
            if (idle != null && _weapon != null && _weapon.activeInHierarchy) ReplaySendEvents(idle); // MenuOut / Activate: re-enable firing etc.
            _reload = null; _weapon = null; _ammoFsm = null;
        }

        internal void CompleteForSwitch() { if (Busy) Finish(); }
        internal void CancelForLoad() { _phase = Phase.Idle; _reload = null; _weapon = null; _ammoFsm = null; }

        private void ReplaySendEvents(FsmState st)
        {
            foreach (var a in st.Actions)
            {
                var se = a as SendEvent;
                if (se == null || !se.Enabled || se.sendEvent == null) continue;
                AmmoSettings.V("  replay " + st.Name + ": " + se.sendEvent.Name);
                try { _reload.Fsm.Event(se.eventTarget, se.sendEvent); }
                catch (Exception e) { Core.Log.Warn("[Guns] SendEvent replay failed: " + e.Message); }
            }
        }

        private static FsmState FindState(PlayMakerFSM f, string name)
        {
            foreach (var s in f.FsmStates) if (s.Name == name) return s;
            return null;
        }

        // (WW) Searches the player's WeaponsArm subtree (active objects only, as FindObjectsOfType did); the whole-scene scan is the
        // fallback when the rig is not found. Every model accepted below must sit under a WeaponsArm anyway.
        private static readonly List<PlayMakerFSM> _armFsms = new List<PlayMakerFSM>(64);
        private static PlayMakerFSM FindActiveReloadFsm()
        {
            var arm = PlayerState.WeaponsArm;
            if (arm != null)
            {
                arm.GetComponentsInChildren(false, _armFsms);
                try { var r = PickReload(_armFsms); if (r != null) return r; }
                finally { _armFsms.Clear(); }
            }
            return null;                        // (WW perf) no whole-scene scan: every accepted model sits under WeaponsArm anyway
        }

        private static PlayMakerFSM PickReload(IList<PlayMakerFSM> fsms)
        {
            for (int i = 0; i < fsms.Count; i++)
            {
                var f = fsms[i];
                if (f == null) continue;
                if (f.FsmName != "Reload" || !f.gameObject.activeInHierarchy || !f.enabled) continue;
                if (f.Fsm == null || !f.Fsm.Initialized) continue;
                if (f.FsmVariables.GetFsmInt("ammo_in_gun") == null) continue;
                var p = f.transform.parent;
                bool underArm = false;
                while (p != null) { if (p.name == "WeaponsArm") { underArm = true; break; } p = p.parent; }
                if (!underArm) continue;
                if (f.ActiveStateName != "idle") { AmmoSettings.V(f.gameObject.name + " Reload busy (" + f.ActiveStateName + ")"); continue; }
                return f;
            }
            return null;
        }
    }
}
