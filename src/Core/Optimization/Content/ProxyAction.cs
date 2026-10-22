using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// Stand-in for one action in a state's Actions array (swapped in by <see cref="FsmEdits.Replace"/>, swapped back on undo).
    /// By default it forwards every PlayMaker callback to the original action with the same lifecycle PlayMaker gives a real action
    /// (Active/Finished/Entered flags, Init with the state, Finish propagation), so OnEnter runs in its original slot of the state
    /// (an event fired by it still stops the remaining actions, exactly like vanilla). Subclasses override <see cref="Enter"/> /
    /// <see cref="Tick"/> to replace or gate the work; they must fall back to the original behaviour when their feature is off,
    /// because the stand-in can stay in ActiveActions until the state is left after the feature was switched off.
    /// </summary>
    internal abstract class ProxyAction : FsmStateAction
    {
        internal readonly FsmStateAction Inner;

        /// <summary>Retains the original native action and copies its observable execution flags into the wrapper.</summary>
        protected ProxyAction(FsmStateAction inner)
        {
            Inner = inner;
            Enabled = inner.Enabled;
            BlocksFinish = inner.BlocksFinish;
            Name = inner.Name;
        }

        /// <summary>Initializes both wrapper and native action with the same owner state.</summary>
        public override void Init(FsmState state) { base.Init(state); Inner.Init(state); }

        /// <summary>Dispatches wrapper entry behavior and mirrors the inner action's completion state.</summary>
        public override void OnEnter()
        {
            Inner.Active = true; Inner.Finished = false; Inner.Entered = true;
            Enter();
            if (Inner.Finished) Finished = true;     // ActivateActions reads our flag right after OnEnter
        }

        /// <summary>Runs the wrapper's update behavior and mirrors the inner action's completion state.</summary>
        public override void OnUpdate() { Tick(); Sync(); }
        /// <summary>Runs the original action's entry callback unless a specialized wrapper replaces it.</summary>
        protected virtual void Enter() { Inner.OnEnter(); }
        /// <summary>Runs the original action's update callback unless a specialized wrapper replaces it.</summary>
        protected virtual void Tick() { Inner.OnUpdate(); }

        /// <summary>Mark the original finished from a replacement (same as its own Finish()).</summary>
        protected void FinishInner() { Inner.Finished = true; }

        /// <summary>Finishes the wrapper when the retained native action finishes.</summary>
        void Sync() { if (Inner.Finished && !Finished) Finish(); }

        /// <summary>Forwards the native late-update callback without changing its timing.</summary>
        public override void OnLateUpdate() { Inner.OnLateUpdate(); Sync(); }
        /// <summary>Forwards the native physics-update callback without changing its timing.</summary>
        public override void OnFixedUpdate() { Inner.OnFixedUpdate(); Sync(); }
        /// <summary>Forwards native state-exit cleanup to the retained action.</summary>
        public override void OnExit() { Inner.OnExit(); Inner.Active = false; }
        /// <summary>Forwards the event name and payload handling to the retained native action.</summary>
        public override bool Event(FsmEvent fsmEvent) { return Inner.Event(fsmEvent); }
        /// <summary>Forwards the native action's IMGUI callback.</summary>
        public override void OnGUI() { Inner.OnGUI(); }
        /// <summary>Returns the retained native action's progress for PlayMaker inspection.</summary>
        public override float GetProgress() { return Inner.GetProgress(); }
        /// <summary>Preserves the original action's authoring validation errors.</summary>
        public override string ErrorCheck() { return Inner.ErrorCheck(); }
        /// <summary>Forwards collision entry to the original native action.</summary>
        public override void DoCollisionEnter(Collision c) { Inner.DoCollisionEnter(c); }
        /// <summary>Forwards collision stay to the original native action.</summary>
        public override void DoCollisionStay(Collision c) { Inner.DoCollisionStay(c); }
        /// <summary>Forwards collision exit to the original native action.</summary>
        public override void DoCollisionExit(Collision c) { Inner.DoCollisionExit(c); }
        /// <summary>Forwards trigger entry to the original native action.</summary>
        public override void DoTriggerEnter(Collider c) { Inner.DoTriggerEnter(c); }
        /// <summary>Forwards trigger stay to the original native action.</summary>
        public override void DoTriggerStay(Collider c) { Inner.DoTriggerStay(c); }
        /// <summary>Forwards trigger exit to the original native action.</summary>
        public override void DoTriggerExit(Collider c) { Inner.DoTriggerExit(c); }
        /// <summary>Forwards particle collisions to the original native action.</summary>
        public override void DoParticleCollision(GameObject other) { Inner.DoParticleCollision(other); }
        /// <summary>Forwards character-controller collisions to the original native action.</summary>
        public override void DoControllerColliderHit(ControllerColliderHit hit) { Inner.DoControllerColliderHit(hit); }
        /// <summary>Forwards joint-break callbacks to the original native action.</summary>
        public override void DoJointBreak(float force) { Inner.DoJointBreak(force); }
        // (2D physics callbacks are not forwarded: Physics2DModule is not referenced by the build and no wrapped state uses them)
        /// <summary>Forwards native root-motion callbacks to the original action.</summary>
        public override void DoAnimatorMove() { Inner.DoAnimatorMove(); }
        /// <summary>Forwards native animator IK callbacks to the original action.</summary>
        public override void DoAnimatorIK(int layerIndex) { Inner.DoAnimatorIK(layerIndex); }
    }
}
