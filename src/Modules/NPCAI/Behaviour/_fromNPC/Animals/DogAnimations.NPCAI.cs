using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>DogAnimations part owned by NPCAI: the attack decisions (reach, start/try attack, dash drive, idle pose timer) and the FSM hooks that gate native damage.</summary>
    internal static partial class DogAnimations
    {
        /// <summary>Random stand / lie duration between DogIdlePoseMinSeconds and DogIdlePoseMaxSeconds.</summary>
        private static float IdleSpan() { float lo = idleMin != null ? idleMin.Value : 10f, hi = idleMax != null ? Mathf.Max(lo, idleMax.Value) : 25f; return UnityEngine.Random.Range(lo, hi); }

        /// <summary>True when the dog's current target is within bite reach (line of reach checked).</summary>
        private static bool InReach(Dog d, GameObject target)
        {
            if (target == null || !target.activeInHierarchy || d.Agent.Target != target) return false;
            if (d.Companion != null && !d.Companion.TargetAlive(target)) return false;
            Vector3 origin = Origin(d);
            Vector3 point = ReachPoint(d, target, origin);
            Vector3 to = point - origin;
            if (to.magnitude > d.Reach + .2f || Mathf.Abs(to.y) > 1.2f) return false;
            Vector3 planar = to; planar.y = 0f;
            if (planar.sqrMagnitude > .01f && Vector3.Dot(d.Agent.T.forward, planar.normalized) < (d.Pose == Pose.RunningBite ? .65f : .45f)) return false;
            if (d.ReachLine == 0)
            {
                RaycastHit hit;
                d.ReachLine = (sbyte)(!Physics.Linecast(origin, point, out hit, Solid, QueryTriggerInteraction.Ignore) || IntendedTarget(hit.collider, target) ? 1 : 2);
            }
            return d.ReachLine == 1;
        }
        // the target's point nearest origin, once per frame per (target, origin); a new point clears the remembered line of reach
        /// <summary>The target's point nearest origin, once per frame per (target, origin).</summary>
        private static Vector3 ReachPoint(Dog d, GameObject target, Vector3 origin)
        {
            int frame = UnityEngine.Time.frameCount;
            if (d.ReachFrame == frame && ReferenceEquals(d.ReachOf, target) && d.ReachOrigin.x == origin.x && d.ReachOrigin.y == origin.y && d.ReachOrigin.z == origin.z) return d.ReachPoint;
            var col = TargetCollider(d, target);
            d.ReachFrame = frame; d.ReachOf = target; d.ReachOrigin = origin; d.ReachLine = 0;
            d.ReachPoint = col != null ? col.ClosestPoint(origin) : target.transform.position;
            return d.ReachPoint;
        }
        /// <summary>The collider of the target used for reach (cached per dog).</summary>
        private static Collider TargetCollider(Dog d, GameObject target)
        {
            float now = UnityEngine.Time.time;
            if (!ReferenceEquals(d.ColOf, target) || now >= d.ColAt + 2f || (!ReferenceEquals(d.TargetCol, null) && d.TargetCol == null))
            {
                d.ColOf = target; d.ColAt = now;
                d.TargetCol = target.GetComponent<Collider>() ?? target.GetComponentInChildren<Collider>();
            }
            return d.TargetCol;
        }
        /// <summary>True when the collider belongs to the target.</summary>
        private static bool IntendedTarget(Collider collider, GameObject target)
        { return collider != null && target != null && (collider.gameObject == target || collider.transform.IsChildOf(target.transform)); }
        /// <summary>The target's reach point seen from the dog's mouth origin.</summary>
        private static Vector3 TargetPoint(Dog d, GameObject target) { return ReachPoint(d, target, Origin(d)); }
        /// <summary>True when the native damage may be applied now (reach at contact).</summary>
        private static bool DamageReady(Dog d)
        {
            if (d.Companion != null) return !d.Companion.Riding && d.Companion.TargetAlive(d.Agent.Target);
            // (WW perf, D10) no enabled Damage FSM: looked for again at most every 0.5 s (was GetComponents + LINQ every frame)
            if ((d.DamageComponent == null || !d.DamageComponent.enabled) && UnityEngine.Time.time >= d.NextDamageLook)
            {
                d.NextDamageLook = UnityEngine.Time.time + .5f;
                PlayMakerFSM found = null;
                d.Agent.Owner.GetComponents(_fsms);
                for (int i = 0; i < _fsms.Count; i++) { var f = _fsms[i]; if (f != null && f.enabled && f.FsmName == "Damage") { found = f; break; } }
                _fsms.Clear();
                d.DamageComponent = found;
            }
            return d.DamageComponent != null && d.DamageComponent.enabled && d.DamageComponent.Fsm != null && d.DamageComponent.Fsm.Initialized && d.DamageComponent.Fsm.ActiveStateName == "trigger";
        }
        /// <summary>Starts a bite / running bite / jump attack against the Damage FSM's target.</summary>
        private static void StartAttack(Dog d, Fsm damage, bool running)
        {
            d.BiteTarget = d.Agent.Target; d.DamageFsm = damage; d.HitSent = false;
            Switch(d, running ? Pose.RunningBite : Pose.Bite, running ? "MovingBite" : "Bite");
            d.AttackRate = d.Clips[d.Playing].length / (running ? .70f : .48f);
            d.NextBite = Time.time + (running ? 1.02f : .58f) + UnityEngine.Random.Range(0f, .04f);
            if (d.Companion != null) d.Companion.SetCooldown(d.NextBite);
            d.RecoveryUntil = Time.time + (running ? .90f : .56f);
            if (!running) return;
            Vector3 direction = TargetPoint(d, d.BiteTarget) - d.Agent.T.position; direction.y = 0f;
            d.DashDirection = direction.sqrMagnitude > .001f ? direction.normalized : d.Agent.T.forward;
            d.DashSpeed = Mathf.Clamp(Mathf.Max(RunSpeed(d), Speed(d)), 5f, 7f);
            // Once committed, pursue the snapshot direction rather than the moving target.
            if (d.Held) { d.Held = false; Hold(d, false); }
            if (d.Companion != null) { d.Dashing = true; return; }
            d.Drag = d.Body.drag; d.Body.drag = 0f; d.Dashing = true;
            Brain.SetTravel(d.Agent.Owner, true); d.Agent.Travel = true;
        }
        /// <summary>Tries an attack when the target is in reach and the dog is ready.</summary>
        private static void TryAttack(Dog d)
        {
            var a = d.Agent;
            if (d.Pose != Pose.Locomotion || a.State != Senses.State.Combat || a.Target == null || Time.time < d.NextBite || !DamageReady(d)) return;
            Vector3 to = TargetPoint(d, a.Target) - a.T.position; to.y = 0f;
            float distance = to.magnitude;
            var damage = d.Companion != null ? null : d.DamageComponent.Fsm;
            if (InReach(d, a.Target)) { StartAttack(d, damage, false); return; }
            float speed = Mathf.Clamp(Mathf.Max(RunSpeed(d), Speed(d)), 5f, 7f);
            if (d.Clips.ContainsKey("MovingBite") && distance <= d.Reach + speed * .22f && Grounded(d) && (distance < .05f || Vector3.Dot(a.T.forward, to / distance) >= .55f))
                StartAttack(d, damage, true);
        }
        /// <summary>Moves a dashing (running bite) dog toward its target each frame.</summary>
        private static void DriveDash(Dog d, float dt)
        {
            float seconds = d.Time / Mathf.Max(.01f, d.AttackRate);
            float speed = seconds < .12f ? 2f : seconds < .48f ? d.DashSpeed : 2f;
            if (d.Companion != null) { d.Companion.Dash(d.DashDirection, speed, dt, d.BiteTarget); return; }
            RaycastHit hit;
            bool blocked = d.Body.SweepTest(d.DashDirection, out hit, speed * Mathf.Max(dt, .016f) + .08f, QueryTriggerInteraction.Ignore);
            if (blocked && (IntendedTarget(hit.collider, d.BiteTarget) || hit.normal.y > .6f)) blocked = false;
            Vector3 velocity = d.DashDirection * (blocked ? 0f : speed); velocity.y = d.Body.velocity.y;
            d.Agent.T.rotation = Quaternion.LookRotation(d.DashDirection); d.Body.velocity = velocity;
        }
        /// <summary>Wait.OnEnter prefix: the Damage FSM's wait of a driven dog follows the animation.</summary>
        public static void BeforeDamageWait(Wait __instance)
        {
            if (Dogs.Count == 0 || !NpcaiRuntime.Running) return;      // (WW) cheap filter: Wait.OnEnter runs everywhere
            var fsm = __instance.Fsm; Dog d;
            if (fsm == null || fsm.Name != "Damage" || fsm.ActiveStateName != "attack" || fsm.GameObject == null || !Dogs.TryGetValue(fsm.GameObject.GetInstanceID(), out d) || !Eligible(d) || __instance.time == null) return;
            if (!d.OriginalWaits.ContainsKey(__instance)) d.OriginalWaits[__instance] = __instance.time.Value;
            __instance.time.Value = d.Pose == Pose.RunningBite ? .48f : .32f;
        }

        /// <summary>Fsm.ProcessEvent prefix: native damage events of driven dogs only when the bite really connects.</summary>
        public static bool BeforeDamageEvent(Fsm __instance, FsmEvent fsmEvent, FsmEventData eventData)
        {
            if (Dogs.Count == 0 || !NpcaiRuntime.Running) return true;      // (WW) Fsm.ProcessEvent is the hottest method in the game: no dog, no string compare
            if (__instance == null || fsmEvent == null || fsmEvent.Name != "attack" || __instance.Name != "Damage" || __instance.GameObject == null) return true;
            Dog d;
            if (!Dogs.TryGetValue(__instance.GameObject.GetInstanceID(), out d) || !Eligible(d) || d.Committing) return true;
            if (d.Pose == Pose.Bite || d.Pose == Pose.RunningBite || d.Pose == Pose.JumpAttack || d.Pose == Pose.LieDown || d.Pose == Pose.Rest || d.Pose == Pose.GetUp || Time.time < d.NextBite) return false;
            if (d.Agent.State != Senses.State.Combat || d.Agent.Target == null) return true;
            StartAttack(d, __instance, false);
            bool leap = jumping.Value && d.Clips.ContainsKey("JumpAttack") && Grounded(d) && !Senses.PlayerInCar && UnityEngine.Random.value < .25f;
            if (leap) { Switch(d, Pose.JumpAttack, "JumpAttack"); d.NextBite = Time.time + d.Clips[d.Playing].length; }
            if (leap)
            {
                Vector3 direction = d.BiteTarget.transform.position - d.Agent.T.position; direction.y = 0f;
                RaycastHit hit = new RaycastHit();
                bool blocked = direction.sqrMagnitude > .01f && d.Body.SweepTest(direction.normalized, out hit, .6f, QueryTriggerInteraction.Ignore);
                if (blocked && hit.collider != null && (hit.collider.gameObject == d.BiteTarget || hit.collider.transform.IsChildOf(d.BiteTarget.transform))) blocked = false;
                if (direction.sqrMagnitude > .01f && !blocked)
                {
                    d.Drag = d.Body.drag; d.Body.drag = 0f; d.Jumping = true; d.JumpSince = Time.time;
                    Brain.SetTravel(d.Agent.Owner, true); d.Agent.Travel = true;
                    d.Body.velocity = direction.normalized * 1.2f + Vector3.up * 2.2f;
                }
                else StartAttack(d, __instance, false);
            }
            return false;
        }

        /// <summary>SetVelocity.DoSetVelocity prefix: held / dashing dogs are moved by this module, not the FSM.</summary>
        public static bool BeforeSetVelocity(SetVelocity __instance)
        {
            if (Dogs.Count == 0 || !NpcaiRuntime.Running) return true;
            var fsm = __instance.Fsm; Dog d;
            return fsm == null || fsm.Name != "Movement" || fsm.GameObject == null || !Dogs.TryGetValue(fsm.GameObject.GetInstanceID(), out d) || !Eligible(d) || (!d.Held && !d.Dashing);
        }
    }
}
