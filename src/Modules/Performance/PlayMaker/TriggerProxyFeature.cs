using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// PM6 (plan §4, research A §1.8 / R7). Every trigger/collision callback of a PlayMakerTriggerEnter/Exit (823/486 baked
    /// proxies) or PlayMakerCollision* proxy calls Fsm.OnTriggerEnter/... (Fsm.cs:3022-3130) on each target FSM: push,
    /// store collider + name, run the active actions' DoTriggerEnter, and, unless that switched state, send the system
    /// event TRIGGER ENTER/EXIT (COLLISION ...) through the full Event -> ProcessEvent path. No game FSM has a transition on
    /// those events (only the TriggerEvent/CollisionEvent ACTIONS react, inside DoTriggerEnter), so that last dispatch
    /// normally changes nothing. These prefixes are exact copies of the six Fsm methods where the system event is skipped
    /// only when <see cref="FsmEventSkip.Unhandled"/> proves it a no-op; then only its side effect (EventData sender info)
    /// is applied. The UpdateStateChanges/PopFsm that follow are kept.
    /// NOT done (not exact): lazy TriggerName/CollisionName. They are public properties that other mods (inspectors) may read
    /// later, and the GameObject can be renamed or destroyed in between; computing the name lazily could return a different
    /// value, so the name is still read at callback time like vanilla.
    /// </summary>
    public sealed class TriggerProxyFeature : Feature
    {
        public override string PlanId { get { return "PM6"; } }
        public override string Key { get { return "Trigger events"; } }
        public override string Title { get { return "Skip unhandled trigger/collision events"; } }
        public override string Category { get { return Categories.PlayMaker; } }
        public override int Order { get { return 6; } }
        public override string Help { get { return "Trigger and collision callbacks no longer push an event nobody listens to through the whole FSM event path. Same result."; } }

        internal static bool On;
        public static long Calls, Skipped;

        static readonly Action<Fsm, string> SetTriggerName = AccessTools.MethodDelegate<Action<Fsm, string>>(AccessTools.PropertySetter(typeof(Fsm), "TriggerName"));
        static readonly Action<Fsm, string> SetCollisionName = AccessTools.MethodDelegate<Action<Fsm, string>>(AccessTools.PropertySetter(typeof(Fsm), "CollisionName"));

        protected override void OnEnable(FeatureMode mode)
        {
            if (!EnsurePatched(typeof(Patch))) throw new Exception("Fsm trigger/collision patches failed");
            On = true;
        }
        protected override void OnDisable() { On = false; }

        public override void DrawStats(ToolLayout l) { l.Value("Callbacks / system events skipped", Calls + " / " + Skipped); }

        /// <summary>The tail shared by all six methods (Fsm.cs:3026-3036 etc.): the state's handler said "not switching".</summary>
        static void SendSystemEvent(Fsm fsm, FsmEvent e)
        {
            if (FsmEventSkip.Unhandled(fsm, e)) { FsmEventSkip.Apply(fsm); Skipped++; }
            else fsm.Event(e);
        }

        [HarmonyPatch]
        static class Patch
        {
            // Fsm.OnTriggerEnter(Collider) (Fsm.cs:3076)
            [HarmonyPatch(typeof(Fsm), "OnTriggerEnter"), HarmonyPrefix]
            static bool TriggerEnter(Fsm __instance, Collider other)
            {
                if (!On) return true;
                Calls++;
                FsmExecutionStack.PushFsm(__instance);
                __instance.TriggerCollider = other;
                SetTriggerName(__instance, other.gameObject.name);
                if (__instance.ActiveState.OnTriggerEnter(other)) { __instance.UpdateStateChanges(); FsmExecutionStack.PopFsm(); }
                else { SendSystemEvent(__instance, FsmEvent.TriggerEnter); __instance.UpdateStateChanges(); FsmExecutionStack.PopFsm(); }
                return false;
            }

            // Fsm.OnTriggerStay(Collider) (Fsm.cs:3094)
            [HarmonyPatch(typeof(Fsm), "OnTriggerStay"), HarmonyPrefix]
            static bool TriggerStay(Fsm __instance, Collider other)
            {
                if (!On) return true;
                Calls++;
                FsmExecutionStack.PushFsm(__instance);
                __instance.TriggerCollider = other;
                SetTriggerName(__instance, other.gameObject.name);
                if (__instance.ActiveState.OnTriggerStay(other)) { __instance.UpdateStateChanges(); FsmExecutionStack.PopFsm(); }
                else { SendSystemEvent(__instance, FsmEvent.TriggerStay); __instance.UpdateStateChanges(); FsmExecutionStack.PopFsm(); }
                return false;
            }

            // Fsm.OnTriggerExit(Collider) (Fsm.cs:3112)
            [HarmonyPatch(typeof(Fsm), "OnTriggerExit"), HarmonyPrefix]
            static bool TriggerExit(Fsm __instance, Collider other)
            {
                if (!On) return true;
                Calls++;
                FsmExecutionStack.PushFsm(__instance);
                __instance.TriggerCollider = other;
                SetTriggerName(__instance, other.gameObject.name);
                if (__instance.ActiveState.OnTriggerExit(other)) { __instance.UpdateStateChanges(); FsmExecutionStack.PopFsm(); }
                else { SendSystemEvent(__instance, FsmEvent.TriggerExit); __instance.UpdateStateChanges(); FsmExecutionStack.PopFsm(); }
                return false;
            }

            // Fsm.OnCollisionEnter(Collision) (Fsm.cs:3022)
            [HarmonyPatch(typeof(Fsm), "OnCollisionEnter"), HarmonyPrefix]
            static bool CollisionEnter(Fsm __instance, Collision collisionInfo)
            {
                if (!On) return true;
                Calls++;
                FsmExecutionStack.PushFsm(__instance);
                __instance.CollisionInfo = collisionInfo;
                SetCollisionName(__instance, collisionInfo.gameObject.name);
                if (__instance.ActiveState.OnCollisionEnter(collisionInfo)) { __instance.UpdateStateChanges(); FsmExecutionStack.PopFsm(); }
                else { SendSystemEvent(__instance, FsmEvent.CollisionEnter); __instance.UpdateStateChanges(); FsmExecutionStack.PopFsm(); }
                return false;
            }

            // Fsm.OnCollisionStay(Collision) (Fsm.cs:3040)
            [HarmonyPatch(typeof(Fsm), "OnCollisionStay"), HarmonyPrefix]
            static bool CollisionStay(Fsm __instance, Collision collisionInfo)
            {
                if (!On) return true;
                Calls++;
                FsmExecutionStack.PushFsm(__instance);
                __instance.CollisionInfo = collisionInfo;
                SetCollisionName(__instance, collisionInfo.gameObject.name);
                if (__instance.ActiveState.OnCollisionStay(collisionInfo)) { __instance.UpdateStateChanges(); FsmExecutionStack.PopFsm(); }
                else { SendSystemEvent(__instance, FsmEvent.CollisionStay); __instance.UpdateStateChanges(); FsmExecutionStack.PopFsm(); }
                return false;
            }

            // Fsm.OnCollisionExit(Collision) (Fsm.cs:3058)
            [HarmonyPatch(typeof(Fsm), "OnCollisionExit"), HarmonyPrefix]
            static bool CollisionExit(Fsm __instance, Collision collisionInfo)
            {
                if (!On) return true;
                Calls++;
                FsmExecutionStack.PushFsm(__instance);
                __instance.CollisionInfo = collisionInfo;
                SetCollisionName(__instance, collisionInfo.gameObject.name);
                if (__instance.ActiveState.OnCollisionExit(collisionInfo)) { __instance.UpdateStateChanges(); FsmExecutionStack.PopFsm(); }
                else { SendSystemEvent(__instance, FsmEvent.CollisionExit); __instance.UpdateStateChanges(); FsmExecutionStack.PopFsm(); }
                return false;
            }
        }
    }
}
