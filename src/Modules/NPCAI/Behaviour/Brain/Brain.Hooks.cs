using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Brain part: the Harmony / Core hook handlers (SetVelocity pedal, Rotate, LookAt, Raycast bumpers, SendEvent, AddForce, SmoothLookAt, hide).</summary>
    internal static partial class Brain
    {
        // ---------- hooks ----------
        /// <summary>SetVelocity.DoSetVelocity (Movement FSM): the pedal</summary>
        public static bool BeforeSetVelocity(SetVelocity __instance)
        {
            try
            {
                if (!On) return true;                                       // (WW) static flag first, then the FSM name (registration needs it)
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Movement") return true;
                var n = Of(fsm.GameObject);
                if (n == null) return true;
                if (n.Halted && n.Rb != null && !n.Rb.isKinematic) { n.CmdSpeed = 0f; Vector3 hv = n.Rb.velocity; hv.x = 0f; hv.z = 0f; n.Rb.velocity = hv; return false; }   // (1.8.0) a dog biting: stands
                if (n.Travel) { n.CmdSpeed = 0f; return false; }            // (1.4.0) under the ground (Burrow) / pouncing (Pounce): NPCAI moves it, even with the brain off
                if (n.Mode == Mode.Off) return true;
                if (n.Mode == Mode.Walk) { n.CmdSpeed = 0f; return false; }      // (1.6.0) Idle drives the body: the game's pedal (run or stop) is skipped
                float z = __instance.z != null && !__instance.z.IsNone ? __instance.z.Value : (__instance.vector != null && !__instance.vector.IsNone ? __instance.vector.Value.z : 0f);
                if (z > n.RunZ) n.RunZ = z;    // (1.4.0) its run speed: how long a run would take (Burrow's travel time)
                if (z <= 0f) return true;      // the Idle / attack states' "stop": vanilla
                if (n.Rb == null) return true;
                Vector3 poiDir = Vector3.zero; bool poi = false;
                float speed = n.Mode == Mode.BackUp ? -Mathf.Min(z, 2.5f) : (n.Mode == Mode.Hold || n.Mode == Mode.Rest || n.Mode == Mode.Search || (n.Mode == Mode.Cover && n.CoverArrived)) ? 0f : z * n.SpeedScale;
                if (n.Mode == Mode.Chase || n.Mode == Mode.Advance || n.Mode == Mode.Cover)
                {
                    // deliberate movement only: no heading yet (the first think after the alert) -> stand; facing away from the heading -> slow
                    // down while turning (full speed within 30 deg, 40 % at 90, 10 % from 120: a big turn is made nearly on the spot, not as an arc)
                    // (WW) on a POI route the body goes straight along it at full speed; only the turn it shows is smoothed
                    poi = speed > 0f && Mover.PoiDirection(n.Owner, n.T.position, out poiDir);
                    if (poi) { if (poiDir == Vector3.zero) speed = 0f; else { n.Heading = Mathf.Atan2(poiDir.x, poiDir.z) * Mathf.Rad2Deg; n.HasHeading = true; } }
                    else if (!n.HasHeading) speed = 0f;
                    else if (n.StrafeDev == 0f) speed *= TurnSpeedFactor(Mathf.Abs(Mathf.DeltaAngle(n.T.eulerAngles.y, n.Heading)));
                }
                if (n.Body != null && n.SpeedCap > 0f) speed = Mathf.Clamp(speed, -n.SpeedCap, n.SpeedCap);   // (1.1.1) no faster than the clip can carry the feet
                if (n.BoostUntil > 0f && speed > 0f) { if (Time.time < n.BoostUntil) speed *= n.Boost; else n.BoostUntil = 0f; }   // (1.7.0) a nest alarm
                n.CmdSpeed = Mathf.Max(0f, speed);
                // (1.1.1) strafing round a small detour: the legs go along the heading while the body faces the target
                if (poi) { if (!Mover.CarryOnPoiRoute(n.Owner, n.CmdSpeed, poiDir)) Mover.Push(n.Rb, poiDir * speed); return false; }   // (WW) carried along the POI route
                Vector3 fwd = poi ? poiDir : n.StrafeDev != 0f && n.HasHeading ? Quaternion.Euler(0f, n.Heading, 0f) * Vector3.forward : n.T.forward;
                Mover.Push(n.Rb, fwd * speed);
                return false;
            }
            catch (Exception e) { NpcaiRuntime.Error("Brain: " + e); return true; }
        }

        /// <summary>Rotate.DoRotate: the Rotate FSM's random yaw and the Attack FSM's run / rotate / turn states</summary>
        public static bool BeforeRotate(Rotate __instance)
        {
            if (_npcs.Count == 0 || !On) return true;      // (WW) no brain at all: nothing to check (NpcOf would say no)
            try
            {
                var fsm = __instance.Fsm;
                if (fsm == null) return true;
                string name = fsm.Name;
                if (name != "Rotate" && name != "Attack") return true;
                return NpcOf(fsm, name) == null;
            }
            catch (Exception e) { NpcaiRuntime.Error("Brain: " + e); return true; }
        }

        /// <summary>Raycast.DoRaycast: the Attack FSM's two 1.2 m bumper rays report "clear" (the feelers see 3-4 m)</summary>
        public static bool BeforeRaycast(Raycast __instance)
        {
            if (_npcs.Count == 0 || !On) return true;
            try
            {
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Attack" || __instance.repeatInterval == null || __instance.repeatInterval.Value == 0) return true;
                var n = NpcOf(fsm, "Attack");
                if (n == null) return true;
                if (__instance.storeDidHit != null && !__instance.storeDidHit.IsNone) __instance.storeDidHit.Value = false;
                return false;
            }
            catch (Exception e) { NpcaiRuntime.Error("Brain: " + e); return true; }
        }

        /// <summary>LookAt.DoLookAt: the Attack FSM's instant snap of the body toward the target (burst state) -&gt; our turn rate instead</summary>
        public static bool BeforeLookAt(LookAt __instance)
        {
            if (_npcs.Count == 0 || !On) return true;
            try
            {
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Attack") return true;
                if (__instance.gameObject == null || __instance.gameObject.OwnerOption != OwnerDefaultOption.UseOwner) return true;   // the sensor children: vanilla
                return NpcOf(fsm, "Attack") == null;
            }
            catch (Exception e) { NpcaiRuntime.Error("Brain: " + e); return true; }
        }

        /// <summary>SmoothLookAt.DoSmoothLookAt: the melee Attack FSMs (Scraffa, Spanna, the animals) turn the body toward the target every frame in the chase state itself (LateUpdate, after our rotation) - that is what kept a melee NPC running straight into the thing the feelers had told it to go around. Skipped while the brain steers (chase states only; the swing's own facing stays).</summary>
        public static bool BeforeSmoothLookAt(SmoothLookAt __instance)
        {
            if (_npcs.Count == 0 || !On) return true;
            try
            {
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Attack") return true;
                if (__instance.gameObject == null || __instance.gameObject.OwnerOption != OwnerDefaultOption.UseOwner) return true;
                var n = NpcOf(fsm, "Attack");
                if (n == null) return true;
                string st = fsm.ActiveStateName;
                if (Time.time < n.FrightUntil && n.Mode == Mode.Cover) return false;
                if (st == "hide") return false;     // the brain owns facing in cover, chase and ghost walks; the game's shelter turn must not compete
                return !ChaseState(st);
            }
            catch (Exception e) { NpcaiRuntime.Error("Brain: " + e); return true; }
        }

        /// <summary>SendEvent.OnEnter: Attack's Animal_Run while holding -&gt; Animal_Idle; Unstuck's Animal_rotateRandom -&gt; our stuck handling</summary>
        internal static bool BeforeDrivingHide(Fsm fsm, FsmEvent ev, FsmEventData data)
        {
            if (_npcs.Count == 0 || !On || ev == null || ev.Name != "hide" || fsm == null || fsm.Name != "Attack" || fsm.ActiveStateName != "trigger") return true;
            var n = Get(fsm.GameObject);
            return n == null || (Time.time >= n.CoverCooldownUntil && DrivingHide(n, Time.time) != null);
        }

        /// <summary>SendEvent.OnEnter prefix: an NPC holding its position does not run off on Animal_Run, and the Unstuck spin goes to the brain's stuck handling.</summary>
        public static bool BeforeSendEvent(SendEvent __instance)
        {
            if (_npcs.Count == 0 || !On) return true;      // (WW) SendEvent runs everywhere: a static check before the event name
            try
            {
                if (__instance.sendEvent == null) return true;
                string ev = __instance.sendEvent.Name;
                if (ev == "Animal_Attack" || ev == "Animal_Attack_Ranged")
                {
                    var frightened = NpcOf(__instance.Fsm, "Attack");
                    if (frightened != null && frightened.Mode == Mode.Cover && Time.time < frightened.FrightUntil)
                    { __instance.Finish(); return false; }
                }
                if (ev == "Animal_Run")
                {
                    var n = NpcOf(__instance.Fsm, "Attack");
                    if (n == null || (n.Mode != Mode.Hold && n.Mode != Mode.Rest && n.Mode != Mode.Search && n.Mode != Mode.Walk && !(n.Mode == Mode.Cover && n.CoverArrived))) return true;
                    if (n.Movement != null) n.Movement.SendEvent("Animal_Idle");
                    if (n.Mode == Mode.Hold || (n.Mode == Mode.Cover && n.Ranged)) Aim(n);
                    __instance.Finish();
                    return false;
                }
                if (ev == "Animal_rotateRandom")
                {
                    var n = NpcOf(__instance.Fsm, "Unstuck");
                    if (n == null) return true;
                    // the game's Unstuck ("moved < 0.2 m in 0.3 s" -> a random spin + a hop) is cancelled while the brain has the NPC; being stuck
                    // is judged by Blocked() (driven vs covered distance), which tells a deliberate turn on the spot from a real block
                    __instance.Finish();
                    return false;
                }
                return true;
            }
            catch (Exception e) { NpcaiRuntime.Error("Brain: " + e); return true; }
        }

        /// <summary>AddForce.DoAddForce (Unstuck): no hop</summary>
        public static bool BeforeAddForce(AddForce __instance)
        {
            if (_npcs.Count == 0 || !On) return true;
            try { return NpcOf(__instance.Fsm, "Unstuck") == null; }
            catch (Exception e) { NpcaiRuntime.Error("Brain: " + e); return true; }
        }

        /// <summary>For Aim: how far the body still has to turn to face its target, degrees; -1 when the brain is not steering this NPC (the game snaps it to the target itself then). A burst is held until this is within [NpcAim] FacingTolerance.</summary>
        internal static float FacingError(GameObject owner)
        {
            if (!On) return -1f;
            var n = Get(owner);
            if (n == null || n.Mode == Mode.Off || n.Target == null) return -1f;
            var t = n.Target.Value;
            if (t == null) return -1f;
            Vector3 to = t.transform.position - n.T.position; to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return 0f;
            return Vector3.Angle(n.T.forward, to);
        }
    }
}
