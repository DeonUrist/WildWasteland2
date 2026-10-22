using WildWasteland.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Visual part of PassengerGuard: melee props hidden in place, aim point on the target's Head bone; Presentation part (Wave A split of Crews/Passenger.cs).</summary>
    internal partial class PassengerGuard
    {
        /// <summary>The melee props (machete, old knife, shiv) under a passenger.</summary>
        private static GameObject[] FindMeleeWeapons(GameObject passenger)
        {
            return Array.FindAll(Array.ConvertAll(passenger.GetComponentsInChildren<Transform>(true), t => t.gameObject), go =>
            {
                string name = go.name;
                return string.Equals(name, "machete", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "old_knife", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "shiv", StringComparison.OrdinalIgnoreCase);
            });
        }

        // The melee prop is neutralised in place (renderers and colliders off) rather than deactivated: the Attack FSM's
        // close-range state re-activates the object every frame, and a SetActive ping-pong made its collider enter the car's
        // CarAttack trigger every frame ("Could not find FSM: ID on collider" spam, a physics pair reset per frame, lag).
        private float _nextWeaponCheck, _nextGuard;
        /// <summary>Neutralises the melee props in place (renderers and colliders off) instead of deactivating them.</summary>
        private void HideMeleeWeapons()
        {
            foreach (var r in _meleeRenderers) if (r != null && r.enabled) r.enabled = false;
            foreach (var c in _meleeColliders) if (c != null && c.enabled) c.enabled = false;
        }

        /// <summary>After PlayMaker: hides the melee props of a ranged passenger twice a second.</summary>
        private void LateUpdate()
        { if (!(Policy != null && Policy.Running)) return;
            // Attack can activate the right-hand blade after Update when its close-range state runs. Apply this after
            // PlayMaker so no melee prop is ever displayed, while leaving left-hand firearms and their effects alone.
            if (_rangedPrefab && Time.time >= _nextWeaponCheck) { _nextWeaponCheck = Time.time + 0.5f; HideMeleeWeapons(); }
        }

        // the Head bone of the target (found once per target, not by walking its whole hierarchy every frame)
        /// <summary>Aim point on the target Head bone (found once per target), else the target root.</summary>
        private Vector3 AimPoint(GameObject target)
        {
            if (target != _aimTargetGo || (_aimHead == null && target != null))
            {
                _aimTargetGo = target; _aimHead = null;
                foreach (var t in target.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name;
                    int colon = n.LastIndexOf(':');
                    if ((colon >= 0 ? n.Substring(colon + 1) : n) == "Head") { _aimHead = t; break; }
                }
                if (_aimHead == null) _aimHead = target.transform;   // no Head: aim at the root (collider centre would need a lookup per frame too)
            }
            return _aimHead.position;
        }
    }
}
