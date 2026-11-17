using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    internal sealed partial class Body
    {
        private const int GroundMask = (1 << 0) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 12) | (1 << 14) | (1 << 16);
        private static readonly RaycastHit[] ExitGroundHits = new RaycastHit[16];
        private SendEvent _stepOut;
        private Transform _exitPoint, _rollCar;
        private Vector3 _rollVelocity;
        private bool _rollStarted, _rollNativeThird;
        private float _launchFixedTime, _standingHipOffset;
        internal bool RollingFromCar => _rollVelocity != Vector3.zero && SeatMotionActive;
        internal Vector3 RollDirection => _rollVelocity.normalized;

        internal static bool BeforeSeatExit(SendEvent action)
        {
            if (action.Fsm.Name == "DriveUse" && action.Fsm == PlayerState.DriveUse?.Fsm
                && action.State.Name == "enter" && action.sendEvent?.Name == "Activate" && action.eventTarget != null)
                ThirdPerson.CaptureSeatView(action.Fsm.GetOwnerDefaultTarget(action.eventTarget.gameObject)?.transform.parent);
            if (action.Fsm.Name != "DriveUse" || action.State.Name != "exit" || action.sendEvent?.Name != "Deactivate"
                || action.Fsm != PlayerState.DriveUse?.Fsm || !PlayerState.InCar) return true;
            return BodyRunner.PlayerBody?.PrepareSeatExit(action) != false;
        }

        internal static bool BeforeSeatExitUpdate(SendEvent action) => BodyRunner.PlayerBody?._stepOut != action;

        // Native Movement writes both horizontal velocity axes every physics step. Leave
        // the Rigidbody to handle carry, friction and contacts until the roll finishes.
        internal static bool BeforeRollMovement(SetVelocity action)
        {
            if (action.Fsm.Name != "Movement") return true;
            if (CrewSeatMotions.BlockMovement(action.Fsm.GameObject)) return false;
            var body = BodyRunner.PlayerBody;
            return body == null || body._rollVelocity == Vector3.zero
                || action.Fsm.GameObject != PlayerState.Player || PlayerState.Dead || !SeatAnimationsAllowed(body._rollNativeThird);
        }

        private bool PrepareSeatExit(SendEvent action)
        {
            var car = PlayerState.CarRoot;
            if (!Alive || !_mixamo || car == null || Pose.IsBike(car) || PlayerState.Dead
                || !CharacterSettings.ReplaceDriver.Value || !CarSeat.Attach(PlayerState.Transform)) return true;
            var trigger = car.Find("DriveTrigger");
            if (trigger == null || action.eventTarget == null
                || action.Fsm.GetOwnerDefaultTarget(action.eventTarget.gameObject) != trigger.gameObject) return true;
            var rb = PlayerState.CarRigidbody;
            var velocity = rb != null ? Vector3.ProjectOnPlane(rb.velocity, Vector3.up) : Vector3.zero;
            bool nativeThird = !Game.FirstPersonCameraOn;
            if (VehicleExitRoll.Required(velocity))
            {
                _rollVelocity = velocity;
                _rollCar = car;
                _rollStarted = false;
                _rollNativeThird = nativeThird;
                return true;
            }

            if (!SeatAnimationsAllowed(nativeThird)) return true;
            var exitPoint = trigger.Find("exitPos");
            if (exitPoint == null) return true;
            ThirdPerson.CaptureSeatView(car);
            StopSeatMotion();
            _seatMotion = SeatMotion.Start(_animator, false, false, false, _graph);
            if (_seatMotion == null) return true;
            _motionCar = _motionHost = car;
            _motionNativeThird = nativeThird;
            _motionStart = car.InverseTransformPoint(CarSeat.HipsPosition());
            _exitPoint = exitPoint;
            var outward = Vector3.Cross(Vector3.up, SeatMotion.Facing(car, Vector3.zero) * Vector3.forward);
            _exitFacing = Quaternion.LookRotation(outward
                * (car.InverseTransformPoint(exitPoint.position).x < car.InverseTransformPoint(PlayerState.Transform.position).x ? -1f : 1f));
            _stepOut = action;
            return false;
        }

        private void CompleteStepOut(bool exit)
        {
            var action = _stepOut;
            _stepOut = null;
            var point = _exitPoint;
            _exitPoint = null;
            if (action == null || PlayerState.DriveUse?.Fsm != action.Fsm || action.Fsm.ActiveStateName != "exit") return;
            if (PlayerState.InCar && !PlayerState.Dead)
            {
                if (!exit || point == null || PlayerState.CarRoot != _motionCar) { action.Fsm.SetState("inCar"); return; }
                var destination = StepOutPosition(point);
                action.Fsm.Event(action.eventTarget, action.sendEvent);
                if (!PlayerState.InCar && PlayerState.Player.TryGetComponent<Rigidbody>(out var body))
                {
                    body.position = destination;
                    ThirdPerson.FinishSeatPan(true);
                }
            }
            action.Finish();
        }

        internal void FinishStepOut()
        {
            if (_stepOut != null && _seatMotion != null && _seatMotion.Finished) CompleteStepOut(true);
        }

        private Vector3 StepOutPosition(Transform point)
        {
            var position = point.position;
            float halfHeight = PlayerState.StandingHeight * .5f;
            if (FindExitGround(position + Vector3.up * .15f, halfHeight + .45f, _motionCar, PlayerState.Transform, out var ground))
                position.y = ground.y + halfHeight + .025f;
            return position;
        }

        private bool RollGrounded()
        {
            var player = PlayerState.Player;
            var body = player.GetComponent<Rigidbody>();
            if (body == null || body.isKinematic) { _rollVelocity = Vector3.zero; return false; }
            bool animate = SeatAnimationsAllowed(_rollNativeThird);
            if (!_rollStarted)
            {
                if (animate) body.velocity += _rollVelocity * VehicleExitRoll.Carry;
                _launchFixedTime = Time.fixedTime;
                _rollStarted = true;
                DamageFromRoll(player, VehicleExitRoll.Damage(_rollVelocity));
                if (animate) return false;
            }
            if (!animate) { _rollVelocity = Vector3.zero; return false; }
            return Time.fixedTime > _launchFixedTime && body.velocity.y <= .5f
                && FindExitGround(player.transform.position, PlayerState.StandingHeight * .5f + .04f, _rollCar, player.transform, out _);
        }

        internal static bool FindExitGround(Vector3 from, float distance, Transform car, Transform actor, out Vector3 ground)
        {
            ground = default;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, ExitGroundHits, distance, GroundMask, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var hit = ExitGroundHits[i];
                if (hit.collider == null || hit.normal.y < .3f || hit.distance >= distance
                    || hit.transform.IsChildOf(actor) || car != null && hit.transform.IsChildOf(car)) continue;
                distance = hit.distance; ground = hit.point; found = true;
            }
            return found;
        }

        private void StartGroundRoll(Vector3 hips)
        {
            // Match WWW3: carry rises linearly with speed while the clip rate rises with its square root.
            float rate = VehicleExitRoll.Rate(_rollVelocity);
            _seatMotion = SeatMotion.Start(_animator, false, false, true, _graph, rate);
            if (_seatMotion == null) { _rollVelocity = Vector3.zero; return; }
            _motionHost = _rollCar;
            _motionNativeThird = _rollNativeThird;
            _exitFacing = Quaternion.LookRotation(_rollVelocity, Vector3.up);
            _motionStart = Origin.World(hips);
        }

        private static void DamageFromRoll(GameObject player, float amount)
        {
            foreach (var fsm in player.GetComponents<PlayMakerFSM>())
                if (fsm.enabled && fsm.Fsm.Initialized && fsm.ActiveStateName == "active"
                    && (fsm.FsmName == "GODMODE" || fsm.FsmName == "GODMODEMovement")) return;
            var health = FsmUtil.Find(player, "Health")?.FsmVariables.GetFsmFloat("Health");
            if (health == null) return;
            health.Value = Mathf.Max(0f, health.Value - amount);
            var effects = FsmUtil.Find(player, "DamageEffectSound");
            if (effects != null && effects.enabled && effects.Fsm.Initialized) effects.SendEvent("Damage");
            if (Log.L1) Log.Main(PresentationModule.Instance, "Vehicle rollout: player took " + amount.ToString("0.0") + " damage");
        }
    }
}
