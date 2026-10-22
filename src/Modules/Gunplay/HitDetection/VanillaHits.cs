using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Gunplay
{
    // The vanilla player Attack FSM writes Bodypart.Damage before sending Damage.
    // Observe that write without changing the FSM, then measure its Health/Condition
    // result after the frame's damage events.
    // WW: subscribed to Core.Hooks.SetFsmFloatEnter (prefix, observe only, always returns true = never skips). The Guns
    // module reports its projectile hits through Services.Hud and sets ExternalReporter; while that is true (and Guns is
    // running) this observer stays quiet, so one shot never produces feedback twice. First line of the handler is a static
    // bool (feedback off / external reporter), then the 1.2.2 string filters; GetComponents use a reused list (no alloc).
    /// <summary>Gunplay part of the vanilla hit observer: notes the player's vanilla gun hits and flushes measured damage to the HUD hit feedback while projectiles are off.</summary>
    internal static class VanillaHits
    {
        private sealed class Hit
        {
            public GameObject Target;
            public FsmFloat Value;
            public float Before, Nominal;
            public Vector3 Point;
            public bool Head, Part;
        }
        private static readonly List<Hit> Pending = new List<Hit>();
        private static readonly List<PlayMakerFSM> _fsms = new List<PlayMakerFSM>(8);

        /// <summary>Core.Hooks.SetFsmFloatEnter prefix (every SetFsmFloat.OnEnter): notes the player's vanilla gun hits (Bodypart.Damage) for hit feedback while projectiles are off. Observe only, never skips.</summary>
        public static bool BeforeSetFsmFloat(SetFsmFloat __instance)
        {
            var feedback = ModApi.Get<IHitFeedback>();
            if (feedback == null || !feedback.Enabled || GunplaySettings.ProjectilesEnabled) return true;     // projectiles report their own hits (ex HitFeedbackService.External && Guns running)
            if (__instance.fsmName == null || __instance.fsmName.Value != "Bodypart" ||
                __instance.variableName == null || __instance.variableName.Value != "Damage") return true;
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Name != "Attack" || fsm.GameObject == null) return true;
            if (!HasFsm(fsm.GameObject, "Reload")) return true;     // a gun (melee weapons have no Reload FSM)
            var target = fsm.GetOwnerDefaultTarget(__instance.gameObject);
            if (target == null) return true;

            GameObject valueOwner;
            FsmFloat value = CreatureHealth(target.transform, out valueOwner);   // (1.2.1) a creature riding in a car is not the car
            bool part = false;
            if (value == null)
            {
                value = Condition(target.transform, out valueOwner);
                part = value != null;
                if (!part)
                {
                    valueOwner = target.transform.root.gameObject;
                    value = Health(valueOwner);
                }
            }
            // Ragdolls and scenery have no creature Health: no marker or number.
            if (value == null) return true;
            var point = fsm.Variables.FindFsmVector3("hitPoint");
            Vector3 at = point != null ? point.Value : target.transform.position;
            bool head = !part && target.transform.parent != null && HasFsm(target, "Bodypart");
            float nominal = __instance.setValue != null ? Mathf.Abs(__instance.setValue.Value) : 0f;
            foreach (var pending in Pending)
                if (pending.Target == valueOwner && pending.Value == value)
                { pending.Nominal += nominal; pending.Point = at; pending.Head |= head; return true; }
            Pending.Add(new Hit { Target = valueOwner, Value = value, Before = value.Value,
                Nominal = nominal, Point = at, Head = head, Part = part });
            return true;
        }

        /// <summary>The object has an FSM of that name (no allocation).</summary>
        private static bool HasFsm(GameObject go, string name)
        {
            go.GetComponents(_fsms);
            bool found = false;
            for (int i = 0; i < _fsms.Count; i++) if (_fsms[i] != null && _fsms[i].FsmName == name) { found = true; break; }
            _fsms.Clear();
            return found;
        }

        /// <summary>A float variable of a named FSM on the object (no allocation).</summary>
        private static FsmFloat FindFloat(GameObject go, string fsmName, string variable)
        {
            go.GetComponents(_fsms);
            FsmFloat result = null;
            for (int i = 0; i < _fsms.Count && result == null; i++)
            {
                var fsm = _fsms[i];
                if (fsm != null && fsm.FsmName == fsmName) result = fsm.FsmVariables.FindFsmFloat(variable);
            }
            _fsms.Clear();
            return result;
        }

        /// <summary>The nearest vehicle-part Condition up from the hit object.</summary>
        private static FsmFloat Condition(Transform target, out GameObject owner)
        {
            for (var t = target; t != null; t = t.parent)
            {
                var value = FindFloat(t.gameObject, "Condition", "Condition");
                if (value != null) { owner = t.gameObject; return value; }
            }
            owner = null; return null;
        }
        // the nearest Health FSM on the way up from the hit object, never past a vehicle part or a car root (Apocapatrol seats its crews
        // under the car, so transform.root of a driver is the car)
        /// <summary>The nearest creature Health up from the hit object, never past a vehicle part or car root.</summary>
        private static FsmFloat CreatureHealth(Transform target, out GameObject owner)
        {
            for (var t = target; t != null; t = t.parent)
            {
                if (t.CompareTag("vehPart") || t.CompareTag("vehPartRemoved") || t.GetComponent("VehicleController") != null) break;
                var value = FindFloat(t.gameObject, "Health", "Health");
                if (value != null) { owner = t.gameObject; return value; }
            }
            owner = null; return null;
        }
        /// <summary>Last resort: any Health FSM below the root.</summary>
        private static FsmFloat Health(GameObject root)
        {
            // last resort, once per hit on an object without creature Health/Condition (reused list instead of the array overload)
            root.GetComponentsInChildren(_fsms);
            FsmFloat result = null;
            for (int i = 0; i < _fsms.Count && result == null; i++)
                if (_fsms[i] != null && _fsms[i].FsmName == "Health") result = _fsms[i].FsmVariables.FindFsmFloat("Health");
            _fsms.Clear();
            return result;
        }
        /// <summary>Late in the frame (HUD job): measured damage of the noted hits goes to the hit feedback (IHitFeedback).</summary>
        public static void Flush()
        {
            if (GunplaySettings.ProjectilesEnabled) { Pending.Clear(); return; }
            if (Pending.Count == 0) return;
            var hud = Services.Hud;
            foreach (var hit in Pending)
            {
                if (hit.Target == null) continue;
                float damage = Mathf.Max(0f, hit.Before - hit.Value.Value);
                // The game's FSM may process its event on the next tick. Preserve
                // the gun's nominal damage rather than silently losing feedback.
                if (damage <= 0f) damage = hit.Nominal;
                if (hud == null) continue;
                if (hit.Part) hud.PartHit(hit.Target, hit.Point, damage);
                else hud.PlayerHit(hit.Target, hit.Point, damage, hit.Head);
            }
            Pending.Clear();
        }
        /// <summary>Origin shift of the noted hit points.</summary>
        public static void Shift(Vector3 delta) { foreach (var hit in Pending) hit.Point += delta; }
        /// <summary>Drops the noted hits.</summary>
        public static void Clear() { Pending.Clear(); }
    }
}
