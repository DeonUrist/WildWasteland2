using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Core
{
    /// <summary>Actor and part classification used by the projectiles (Wave A split of Tracers.cs; B01 merges it into a Core ActorQuery service shared with HUD VanillaHits and NPC): creature root, owning vehicle part, corpse/bodypart/head tests and the Health variable.</summary>
    public static class ActorQuery
    {
        /// <summary>Clears classifications and native references at a scene boundary.</summary>
        internal static void Reset() { _isPart.Clear(); _health.Clear(); _creature.Clear(); }
        /// <summary>Drops destroyed health roots and short-lived classification caches at the existing projectile sweep.</summary>
        public static void Sweep()
        {
            _dead.Clear(); foreach (var pair in _health) if (pair.Value.Root == null) _dead.Add(pair.Key);
            foreach (int key in _dead) _health.Remove(key); _isPart.Clear(); _creature.Clear();
        }
        private static readonly List<int> _dead = new List<int>();
        // (1.5.2) A dead NPC is a separate "<Name>_Dead" ragdoll (layer Item, limbs on Default with Bodypart FSMs that add to a Health
        // the corpse doesn't have): a Bodypart on a body with no Health FSM. The game picks the hit effect by layer (Actor = blood), so
        // its limbs got the ground's dust; and our creature test showed damage numbers on it.
        /// <summary>Recognizes healthless creature ragdolls while excluding players and vehicle parts.</summary>
        public static bool IsCorpse(GameObject go)
        {
            if (go == null || IsPlayerObj(go)) return false;
            if (OwningPart(go.transform) != null || go.CompareTag("vehPartRemoved") || IsVehiclePart(go)) return false;
            if (!HasBodypart(go) && !HasBodypart(RootOf(go.transform).gameObject)) return false;
            return float.IsNaN(HealthOf(go));
        }

        /// <summary>Tests for a native Bodypart FSM on the exact object.</summary>
        public static bool HasBodypart(GameObject go)
        {
            foreach (var f in go.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "Bodypart") return true;
            return false;
        }


        /// <summary>Checks whether the object belongs to the tagged player hierarchy.</summary>
        public static bool IsPlayerObj(GameObject go)
        {
            for (var t = go.transform; t != null; t = t.parent) if (t.CompareTag("Player")) return true;
            return false;
        }

        // the hit object is a creature's head when it is a child with its own Bodypart FSM (the player's "head", NPC mixamorig:Head)
        /// <summary>Recognizes a child hit object carrying its own native Bodypart FSM.</summary>
        public static bool IsHead(GameObject go)
        {
            return go.transform.parent != null && HasBodypart(go);
        }

        // Vehicle parts (45 prefabs: engines, wheels, doors ...) carry a vanilla Bodypart FSM whose Damage event subtracts the bullet's damage 1:1 from
        // their Condition (an akms round = -23 %). With [Tracers] VehicleDamage on, the mod's VehicleDamagePer1 rule replaces that: the vanilla
        // Damage event / Bodypart.Damage write are not replayed for a part (the other hit actions - effect, HitEffect, DamageFlammable, CheckFriendly - are).
        private static readonly Dictionary<int, bool> _isPart = new Dictionary<int, bool>();
        /// <summary>Caches whether native condition or repair FSMs identify a vehicle part.</summary>
        public static bool IsVehiclePart(GameObject go)
        {
            bool part;
            int id = go.GetInstanceID();
            if (_isPart.TryGetValue(id, out part)) return part;
            part = false;
            foreach (var f in go.GetComponents<PlayMakerFSM>()) if (f != null && (f.FsmName == "Condition" || f.FsmName == "Repair")) { part = true; break; }
            _isPart[id] = part;
            return part;
        }


        // [Debug] HitLog: the Health of the thing hit (its root's Health FSM), before and after
        // the creature's Health variable, found once per root (a full hierarchy scan) and cached by root object
        /// <summary>Native root identity and cached Health variable for an actor.</summary>
        private sealed class HealthRef { public GameObject Root; public FsmFloat Var; }
        private static readonly Dictionary<int, HealthRef> _health = new Dictionary<int, HealthRef>();
        /// <summary>Reads the creature root Health variable, or NaN when the hierarchy has no health.</summary>
        public static float HealthOf(GameObject go)
        {
            if (go == null) return float.NaN;
            var root = RootOf(go.transform).gameObject;
            int id = root.GetInstanceID();
            HealthRef hr;
            if (_health.TryGetValue(id, out hr) && hr.Root == root) return hr.Var != null ? hr.Var.Value : float.NaN;
            hr = new HealthRef { Root = root };
            foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>())
                if (f.FsmName == "Health") { var v = f.FsmVariables.FindFsmFloat("Health"); if (v != null) { hr.Var = v; break; } }
            _health[id] = hr;
            return hr.Var != null ? hr.Var.Value : float.NaN;
        }


        // (1.0.1) The creature a collider belongs to. Apocapatrol seats its crews under the car (sitPos / Apocapatrol.PassengerPos), so
        // transform.root of a driver's collider is the car, not the NPC: the car's VehicleController made the driver "car metal" (sparks),
        // and HealthOf / the HUD target / HitLog read the car instead of the person. Walks up from the collider, never past a vehicle part
        // (tag vehPart / vehPartRemoved) or a car root (VehicleController): the first object with a Health FSM is the creature (NPC root);
        // without one (a ragdoll corpse, the player's parts) the highest object with a Bodypart FSM or on the NPC layer (10). null = not a creature.
        /// <summary>Collider-to-creature classification retaining source and root identity.</summary>
        private sealed class CreatureRef { public Transform From; public Transform Root; public bool Found; }
        private static readonly Dictionary<int, CreatureRef> _creature = new Dictionary<int, CreatureRef>();
        /// <summary>Finds an enclosing creature without crossing a vehicle part or vehicle root.</summary>
        public static Transform Creature(Transform t)
        {
            if (t == null) return null;
            int id = t.GetInstanceID();
            CreatureRef cr;
            if (_creature.TryGetValue(id, out cr) && cr.From == t && (cr.Root == null ? !cr.Found : true)) return cr.Root;
            Transform health = null, body = null;
            for (var p = t; p != null; p = p.parent)
            {
                if (p.CompareTag("vehPart") || p.CompareTag("vehPartRemoved") || p.GetComponent("VehicleController") != null) break;
                bool bp = false, hp = false;
                foreach (var f in p.GetComponents<PlayMakerFSM>())
                {
                    if (f == null) continue;
                    if (f.FsmName == "Health") hp = true;
                    else if (f.FsmName == "Bodypart") bp = true;
                }
                if (hp) { health = p; break; }
                if (bp || p.gameObject.layer == 10) body = p;
            }
            var root = health != null ? health : body;
            _creature[id] = new CreatureRef { From = t, Root = root, Found = root != null };
            return root;
        }
        // the creature's root when the collider is part of one, else the hierarchy root as before
        /// <summary>Uses the identified creature root, falling back to the transform hierarchy root.</summary>
        public static Transform RootOf(Transform t)
        {
            var c = Creature(t);
            return c != null ? c : t.root;
        }

        /// <summary>Finds an attached vehicle part ancestor while excluding seated creatures.</summary>
        public static Transform OwningPart(Transform t)
        {
            if (Creature(t) != null) return null;           // (1.0.1) a creature seated under a car part is never the part
            for (; t != null; t = t.parent) if (t.CompareTag("vehPart")) return t;
            return null;
        }
    }
}
