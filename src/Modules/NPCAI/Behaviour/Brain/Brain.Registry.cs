using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Brain part: the NPC registry (which NPCs get a brain) and the FSM lookups.</summary>
    internal static partial class Brain
    {
        // ---------- NPC registry ----------
        /// <summary>The brain of an object or null.</summary>
        private static Npc Get(GameObject owner)
        {
            Npc n;
            return owner != null && _npcs.TryGetValue(owner.GetInstanceID(), out n) ? n : null;
        }

        /// <summary>the first Movement SetVelocity of an NPC registers it (every ground NPC with a target runs that action every frame)</summary>
        private static Npc Of(GameObject owner)
        {
            if (owner == null) return null;
            int id = owner.GetInstanceID();
            Npc n;
            if (_npcs.TryGetValue(id, out n)) return n;
            if (_ignored.Contains(id)) return null;
            _retry = false;
            n = Make(owner);
            if (n == null) { if (!_retry) _ignored.Add(id); return null; }
            _npcs[id] = n;
            return n;
        }
        private static bool _retry;      // Make: not ready yet (Detection FSM not initialised) - asked again next time, not ignored for good

        /// <summary>Builds a brain for a ground NPC (root object with Movement/Attack/Detection FSMs and a gravity Rigidbody); flyers and parented objects are left alone.</summary>
        private static Npc Make(GameObject owner)
        {
            if (owner.transform.parent != null) return null;         // seated in a car (Apocapatrol), or a part of something
            // #63: Patrols flags its crew as simple convoy bandits before the body is switched on, so a crew member is seen here in the
            // moment it spawns, not yet seated. Refused before any work (no scan, no animation framework body built and torn down);
            // not cached as ignored, so a crew member that dismounts to fight (flagged Fighting) is registered normally.
            if (SimpleBandits.Has(owner) && !SimpleBandits.Fighting(owner)) { _retry = true; return null; }
            var rb = owner.GetComponent<Rigidbody>();
            if (rb == null || !rb.useGravity) return null;         // flyers (bats, wasps, Terror of the Night) have gravity off
            PlayMakerFSM attack = null, movement = null, detection = null, unstuck = null;
            var fsms = owner.GetComponents<PlayMakerFSM>();
            foreach (var f in fsms)
            {
                if (f == null) continue;
                switch (f.FsmName)
                {
                    case "Attack": attack = f; break;
                    case "Movement": movement = f; break;
                    case "Detection": detection = f; break;
                    case "Unstuck": unstuck = f; break;
                }
            }
            if (attack == null || movement == null || detection == null || unstuck == null) return null;
            if (detection.Fsm == null || !detection.Fsm.Initialized) { _retry = true; return null; }    // try again next frame (not cached as ignored)
            var target = detection.FsmVariables.FindFsmGameObject("detectedObj");
            if (target == null) return null;
            var n = new Npc { Owner = owner, T = owner.transform, Rb = rb, Col = owner.GetComponent<Collider>(), Attack = attack, Movement = movement, Target = target };
            n.HideVar = attack.FsmVariables.FindFsmGameObject("Hide");
            foreach (var f in fsms)
                if (f != null && f.FsmName == "Health" && f.Fsm != null && f.Fsm.Initialized) { n.HpVar = f.FsmVariables.FindFsmFloat("Health"); if (n.HpVar != null) n.HpMax = n.HpVar.Value; break; }
            WeaponRanges.Kind kind;
            n.Ranged = WeaponRanges.GunKindOf(owner, out kind);
            n.Kind = kind;
            n.MovementFsm = movement;
            n.MadeAt = Time.time;
            if (n.Ranged) SetupRanged(n);
            n.Stagger = (_created++ % 10) * 0.01f;
            n.NextTick = Time.time + n.Stagger;
            return n;
        }

        /// <summary>a gunman's aim pose (the Movement FSM's AttackRanged animator state); also run later for one whose gun showed up after its spawn</summary>
        private static void SetupRanged(Npc n)
        {
            var owner = n.Owner; var movement = n.MovementFsm;
            if (movement == null) return;

            n.Anim = owner.GetComponentInChildren<Animator>(true);
            TryRig(n);
            try
            {
                var mf = movement.Fsm;
                if (mf != null && mf.States != null)
                    foreach (var st in mf.States)
                    {
                        if (st == null || st.Name != "AttackRanged" || st.Actions == null) continue;
                        foreach (var a in st.Actions)
                        {
                            var ap = a as AnimatorPlay;
                            if (ap != null && ap.stateName != null && !string.IsNullOrEmpty(ap.stateName.Value)) { n.AimState = ap.stateName.Value; break; }
                        }
                    }
            }
            catch (Exception e) { if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: no aim pose for " + owner.name + ": " + e.Message); }
        
        }

        /// <summary>The brain behind an FSM of the given name (null when the FSM is not that NPC's or has no brain).</summary>
        private static Npc NpcOf(Fsm fsm, string fsmName)
        {
            if (fsm == null || fsm.Name != fsmName) return null;
            var n = Get(fsm.GameObject);
            return n != null && n.Mode != Mode.Off && On ? n : null;
        }

    }
}
