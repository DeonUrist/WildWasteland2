using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Performance
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

        protected ProxyAction(FsmStateAction inner)
        {
            Inner = inner;
            Enabled = inner.Enabled;
            BlocksFinish = inner.BlocksFinish;
            Name = inner.Name;
        }

        public override void Init(FsmState state) { base.Init(state); Inner.Init(state); }

        public override void OnEnter()
        {
            Inner.Active = true; Inner.Finished = false; Inner.Entered = true;
            Enter();
            if (Inner.Finished) Finished = true;     // ActivateActions reads our flag right after OnEnter
        }

        public override void OnUpdate() { Tick(); Sync(); }
        protected virtual void Enter() { Inner.OnEnter(); }
        protected virtual void Tick() { Inner.OnUpdate(); }

        /// <summary>Mark the original finished from a replacement (same as its own Finish()).</summary>
        protected void FinishInner() { Inner.Finished = true; }

        void Sync() { if (Inner.Finished && !Finished) Finish(); }

        public override void OnLateUpdate() { Inner.OnLateUpdate(); Sync(); }
        public override void OnFixedUpdate() { Inner.OnFixedUpdate(); Sync(); }
        public override void OnExit() { Inner.OnExit(); Inner.Active = false; }
        public override bool Event(FsmEvent fsmEvent) { return Inner.Event(fsmEvent); }
        public override void OnGUI() { Inner.OnGUI(); }
        public override float GetProgress() { return Inner.GetProgress(); }
        public override string ErrorCheck() { return Inner.ErrorCheck(); }
        public override void DoCollisionEnter(Collision c) { Inner.DoCollisionEnter(c); }
        public override void DoCollisionStay(Collision c) { Inner.DoCollisionStay(c); }
        public override void DoCollisionExit(Collision c) { Inner.DoCollisionExit(c); }
        public override void DoTriggerEnter(Collider c) { Inner.DoTriggerEnter(c); }
        public override void DoTriggerStay(Collider c) { Inner.DoTriggerStay(c); }
        public override void DoTriggerExit(Collider c) { Inner.DoTriggerExit(c); }
        public override void DoParticleCollision(GameObject other) { Inner.DoParticleCollision(other); }
        public override void DoControllerColliderHit(ControllerColliderHit hit) { Inner.DoControllerColliderHit(hit); }
        public override void DoJointBreak(float force) { Inner.DoJointBreak(force); }
        // (2D physics callbacks are not forwarded: Physics2DModule is not referenced by the build and no wrapped state uses them)
        public override void DoAnimatorMove() { Inner.DoAnimatorMove(); }
        public override void DoAnimatorIK(int layerIndex) { Inner.DoAnimatorIK(layerIndex); }
    }
}
