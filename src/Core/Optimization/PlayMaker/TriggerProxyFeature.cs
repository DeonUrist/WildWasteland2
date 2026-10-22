using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
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
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "PM6"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Trigger events"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Skip unhandled trigger/collision events"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.PlayMaker; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 6; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Trigger and collision callbacks no longer push an event nobody listens to through the whole FSM event path. Same result."; } }

        internal static bool On;
        public static long Calls, Skipped;

        static readonly Action<Fsm, string> SetTriggerName = AccessTools.MethodDelegate<Action<Fsm, string>>(AccessTools.PropertySetter(typeof(Fsm), "TriggerName"));
        static readonly Action<Fsm, string> SetCollisionName = AccessTools.MethodDelegate<Action<Fsm, string>>(AccessTools.PropertySetter(typeof(Fsm), "CollisionName"));

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            On = true;
        }
        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable() { On = false; }

        /// <summary>Registers one handler with Core's shared six-callback physics dispatcher.</summary>
        public override void SubscribeHooks() { Hooks.FsmPhysicsCallback.Prefix(Module, BeforePhysics); }

        /// <summary>3D physics callback replacement; inactive mode immediately yields to native PlayMaker.</summary>
        static bool BeforePhysics(ref Hooks.FsmPhysicsArgs args)
        {
            if (!On) return true;
            switch (args.Kind)
            {
                case Hooks.FsmPhysicsKind.TriggerEnter: return Shared.TriggerEnter(args.Fsm, args.Collider);
                case Hooks.FsmPhysicsKind.TriggerStay: return Shared.TriggerStay(args.Fsm, args.Collider);
                case Hooks.FsmPhysicsKind.TriggerExit: return Shared.TriggerExit(args.Fsm, args.Collider);
                case Hooks.FsmPhysicsKind.CollisionEnter: return Shared.CollisionEnter(args.Fsm, args.Collision);
                case Hooks.FsmPhysicsKind.CollisionStay: return Shared.CollisionStay(args.Fsm, args.Collision);
                case Hooks.FsmPhysicsKind.CollisionExit: return Shared.CollisionExit(args.Fsm, args.Collision);
                default: return true;
            }
        }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l) { l.Value("Callbacks / system events skipped", Calls + " / " + Skipped); }

        /// <summary>The tail shared by all six methods (Fsm.cs:3026-3036 etc.): the state's handler said "not switching".</summary>
        static void SendSystemEvent(Fsm fsm, FsmEvent e)
        {
            if (FsmEventSkip.Unhandled(fsm, e)) { FsmEventSkip.Apply(fsm); Skipped++; }
            else fsm.Event(e);
        }

        /// <summary>Vanilla-equivalent callback bodies selected by the shared dispatcher.</summary>
        static class Shared
        {
            // Fsm.OnTriggerEnter(Collider) (Fsm.cs:3076)

            /// <summary>3D trigger-entry replacement: native action callbacks and transitions run before unhandled system events are skipped.</summary>
            internal static bool TriggerEnter(Fsm __instance, Collider other)
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

            /// <summary>3D trigger-stay replacement: preserves native callback order while suppressing only an unhandled system event.</summary>
            internal static bool TriggerStay(Fsm __instance, Collider other)
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

            /// <summary>3D trigger-exit replacement: preserves native collider/name state and transition processing.</summary>
            internal static bool TriggerExit(Fsm __instance, Collider other)
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

            /// <summary>3D collision-entry replacement: preserves native collision/name state and transition processing.</summary>
            internal static bool CollisionEnter(Fsm __instance, Collision collisionInfo)
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

            /// <summary>3D collision-stay replacement: preserves native callbacks while suppressing only an unhandled system event.</summary>
            internal static bool CollisionStay(Fsm __instance, Collision collisionInfo)
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

            /// <summary>3D collision-exit replacement: preserves native callbacks and state-change processing.</summary>
            internal static bool CollisionExit(Fsm __instance, Collision collisionInfo)
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
