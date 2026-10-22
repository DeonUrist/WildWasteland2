using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Senses part: the Detection / Attack FSM hooks (SensorGetDetections, the burst line of sight) and the brain-facing state queries.</summary>
    internal static partial class Senses
    {
        // ---------- the Detection FSM ----------
        /// <summary>Harmony prefix on SensorGetDetections.DoAction: for our NPCs the result is what the NPC knows (its seen target, or its ghost), written to the action's own variable and announced with the state's own events.</summary>
        public static bool BeforeGetDetections(SensorGetDetections __instance)
        {
            try
            {
                if (!On) return true;
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Detection") return true;
                var a = Of(fsm.GameObject);
                if (a == null) return true;
                GameObject want = Known(a);
                if (__instance.storeDetected != null && !__instance.storeDetected.IsNone) __instance.storeDetected.Value = want;
                if (__instance.storeDetectionCount != null && !__instance.storeDetectionCount.IsNone) __instance.storeDetectionCount.Value = want != null ? 1 : 0;
                string st = fsm.ActiveStateName;
                if (st == "searchRange") { if (__instance.noneDetectedEvent != null) fsm.Event(__instance.noneDetectedEvent); }   // straight on to checkLOS, no 30 s hunt through walls
                else if (want != null) { if (__instance.detectedEvent != null) fsm.Event(__instance.detectedEvent); }
                else if (__instance.noneDetectedEvent != null) fsm.Event(__instance.noneDetectedEvent);
                return false;
            }
            catch (Exception e) { NpcaiRuntime.Error("Senses: " + e); return true; }
        }

        /// <summary>the burst's line-of-sight check (Attack FSM "ranged" state): a target the NPC sees right now, never a ghost</summary>
        public static bool BeforeLosResult(SensorGetLineOfSightResult __instance)
        {
            try
            {
                if (!On) return true;
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Attack") return true;
                if(__instance.targetObject==null||__instance.targetObject.Value==null)
                {if(__instance.storeVisibility!=null&&!__instance.storeVisibility.IsNone)__instance.storeVisibility.Value=0f;if(__instance.storeIsVisible!=null&&!__instance.storeIsVisible.IsNone)__instance.storeIsVisible.Value=false;if(!__instance.everyFrame)__instance.Finish();return false;}
                var a = Get(fsm.GameObject);
                if (a == null) return true;
                // seen within the last two looks AND a clear line right now: no bursts through the wall you just stepped behind
                bool vis = SeesNow(a);
                if (__instance.storeVisibility != null && !__instance.storeVisibility.IsNone) __instance.storeVisibility.Value = vis ? 1f : 0f;
                if (__instance.storeIsVisible != null && !__instance.storeIsVisible.IsNone) __instance.storeIsVisible.Value = vis;
                return false;
            }
            catch (Exception e) { NpcaiRuntime.Error("Senses: " + e); return true; }
        }

        /// <summary>the target is in sight right now: seen within the last two looks and a clear line from the eyes to its head or body</summary>
        private static bool SeesNow(Agent a)
        {
            if ((a.State != State.Combat && a.State != State.Hide) || a.Target == null || a.UnseenFor > 0.35f) return false;
            bool isPlayer = a.Target == _player;
            if (isPlayer && a.Target.layer == 5) return false;
            // (perf) the burst LOS action asks every frame: the two rays are cast at most every 0.1 s per NPC and target (a burst can start
            // or go on at most 0.1 s after the line opened / closed)
            float now = Time.time;
            if (ReferenceEquals(a.SeesTarget, a.Target) && now - a.SeesAt < 0.1f) return a.Sees;
            Vector3 head, body;
            Points(a.Target, isPlayer, out head, out body);
            Vector3 eye = Eye(a); Transform troot = a.Target.transform.root;
            a.Sees = Clear(eye, head, troot) || Clear(eye, body, troot);
            a.SeesAt = now; a.SeesTarget = a.Target;
            return a.Sees;
        }

        /// <summary>What the Detection FSM is told the NPC detects: its combat target, its flank point or ghost while investigating / searching, else nothing.</summary>
        private static GameObject Known(Agent a)
        {
            if(a.CrewBoarding)return null;
            if(a.Tag=="MutantSanity"&&(a.State!=State.Combat||a.Target!=_player))SimpleLook(a,Time.time);
            switch (a.State)
            {
                case State.Combat:
                case State.Hide: if (a.Target == null) { a.State = State.Idle; return null; } return a.Target;
                case State.Investigate: return a.Flanking && a.FlankObj != null ? a.FlankObj : a.Ghost != null ? a.Ghost.Obj : null;
                case State.Search: return a.Ghost != null ? a.Ghost.Obj : null;
                default: return null;
            }
        }

        /// <summary>for the brain and Aim</summary>
        internal static int KindOf(GameObject owner)
        {
            if (!On) return 0;
            var a = Get(owner);
            if (a == null) return 0;
            switch (a.State) { case State.Combat: case State.Hide: return 1; case State.Investigate: return 2; case State.Search: return 3; default: return 0; }
        }
        /// <summary>True while the NPC goes to or searches a ghost (not a real target).</summary>
        internal static bool IsGhostTarget(GameObject owner) { int k = KindOf(owner); return k == 2 || k == 3; }
        /// <summary>The brain / Idle reached the NPC's goal: the end of a flank detour or the arrival at the ghost.</summary>
        internal static void ArrivedAt(GameObject owner)
        {
            var a = Get(owner);
            if (a == null || a.State != State.Investigate) return;
            if (a.Flanking) { EndFlank(a); return; }        // that was the flank point: the ghost itself is next
            Arrived(a, Time.time);
        }

        /// <summary>(1.1.0) the brain took the NPC into cover / out of it: Hide keeps the target and ignores every ghost until the cover ends</summary>
        internal static void SetHiding(GameObject owner, bool hiding)
        {
            var a = Get(owner);
            if (a == null) return;
            if (hiding)
            {
                if (a.State == State.Hide) return;
                if (a.State != State.Combat) { Release(a); a.Target = a.Target ?? _player; }   // covering from a ghost / the game's hide: the player is what it hides from
                a.State = State.Hide; a.Pursue = null; a.Pursuits = 0;
                Log(a, "takes cover (keeps its target, ignores ghosts)");
            }
            else if (a.State == State.Hide)
            {
                a.State = a.Target != null ? State.Combat : State.Idle;
                if (WildWasteland.Core.Log.L2) Log(a, "leaves cover" + (a.UnseenFor > 0.5f ? " (target not in sight for " + a.UnseenFor.ToString("0") + " s)" : ""));
            }
        }

    }
}
