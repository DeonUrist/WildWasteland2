using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    internal sealed partial class Body
    {
        private SeatMotion _seatMotion;
        private Transform _motionCar;
        private Vector3 _previousHips, _motionStart;
        private Quaternion _exitFacing;
        private bool _motionReady, _motionEntering, _previousNativeThird, _motionNativeThird;
        internal bool SeatMotionActive => _seatMotion != null;
        internal bool SeatMotionNativeThird => SeatMotionActive && _motionNativeThird;
        internal Transform SeatMotionVehicle => _motionHost;
        private Transform _motionHost;
        private Vector3 _stepOutPosition;

        private static bool SeatAnimationsAllowed(bool nativeThird) => CharacterSettings.FirstPersonCarEntryAnimations.Value
            || nativeThird || ThirdPerson.On && !ThirdPerson.Peek;

        internal bool TrySeatEye(out Vector3 eye, out Quaternion rotation)
        {
            eye = Vector3.zero; rotation = Quaternion.identity;
            Transform head;
            if (!SeatMotionActive || !Bones.TryGetValue("mixamorig:Head", out head) || head == null) return false;
            rotation = head.rotation;
            eye = head.position + rotation * new Vector3(0f, .075f, .08f);
            return true;
        }

        internal bool TrySeatViewTarget(out Vector3 target, out Quaternion facing, out float progress)
        {
            var camera = PlayerState.PlayerCamera;
            progress = Mathf.SmoothStep(0f, 1f, _seatMotion.ViewProgress);
            facing = PlayerState.Transform.rotation;
            target = CharacterSettings.BodyInCar ? ThirdPerson.CarShiftEye(camera) : camera.position;
            if (_stepOut != null)
            {
                // Native InCar raises PCameraPos from .25 m to .75 m on return to OnFoot.
                facing = _exitFacing;
                var offset = PlayerState.Transform.InverseTransformVector(camera.position - PlayerState.Transform.position) + Vector3.up * .5f;
                target = _stepOutPosition + facing * offset;
            }
            return _stepOut != null || _motionEntering;
        }

        internal void StepSeatMotion(Transform car, bool seated, bool animate)
        {
            Transform hips;
            if (!_mixamo || !Bones.TryGetValue("mixamorig:Hips", out hips)) return;
            Vector3 normal = seated && CarSeat.Seat != null ? CarSeat.HipsPosition() : hips.position;
            if (car != _motionCar)
            {
                CompleteStepOut(false);
                StopSeatMotion();
                _motionEntering = car != null;
                var host = _motionEntering ? car : _motionCar;
                if (_motionEntering && _motionReady && animate && SeatAnimationsAllowed(_previousNativeThird) && !Pose.IsBike(host))
                {
                    _seatMotion = SeatMotion.Start(_animator, true, false, false, _graph);
                    _motionHost = host;
                    _motionNativeThird = _previousNativeThird;
                    _motionStart = car.InverseTransformPoint(Origin.Scene(_previousHips));
                }
                _motionCar = car;
            }
            _motionReady = true;
            _previousNativeThird = !Game.FirstPersonCameraOn;
            _previousHips = Origin.World(normal);
            if (seated) _rollVelocity = Vector3.zero;
            if (!seated && car == null)
            {
                if (_rollVelocity == Vector3.zero) _standingHipOffset = normal.y - PlayerState.Transform.position.y;
                else if (!animate) _rollVelocity = Vector3.zero;
                else if (_seatMotion == null && RollGrounded()) StartGroundRoll(normal);
            }
            if (_seatMotion == null) return;
            try
            {
                if (!animate || !SeatAnimationsAllowed(_motionNativeThird) || !StepSeatClip(normal))
                {
                    if (_seatMotion.Finished && _motionEntering && _stepOut == null && seated) ThirdPerson.FinishSeatPan(false);
                    CompleteStepOut(true);
                    StopSeatMotion();
                    _rollVelocity = Vector3.zero;
                }
                else
                {
                    ThirdPerson.SeatView(true);
                    if (ThirdPerson.TrySeatPan(this, out var eye, out var rotation))
                    {
                        var head = Bones["mixamorig:Head"];
                        var yaw = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
                        Root.transform.RotateAround(head.position, Vector3.up, Mathf.DeltaAngle(Root.transform.eulerAngles.y, yaw.eulerAngles.y));
                        head.rotation = rotation;
                        Root.transform.position += eye - head.position - Vector3.up * .075f - yaw * Vector3.forward * CharacterSettings.BodyBack.Value;
                    }
                    _wheelPoseFrame = -1; ShowProp(false); CapturePose(null);
                }
            }
            catch (System.Exception e)
            {
                StopSeatMotion();
                CompleteStepOut(true);
                _rollVelocity = Vector3.zero;
                Log.Warn(PresentationModule.Instance, "Player seat animation: " + e.Message);
            }
        }

        private bool StepSeatClip(Vector3 target)
        {
            if (_stepOut != null)
            {
                if (_exitPoint == null || _motionHost == null) return false;
                _stepOutPosition = StepOutPosition(_exitPoint);
                return _seatMotion.Step(_motionHost.TransformPoint(_motionStart),
                    _stepOutPosition + Vector3.up * _standingHipOffset, SeatMotion.Facing(_motionHost, Vector3.zero), false);
            }
            if (!_motionEntering) return _seatMotion.Step(Origin.Scene(_motionStart), target, _exitFacing);
            if (_motionCar == null) return false;
            return _seatMotion.Step(_motionCar.TransformPoint(_motionStart), target, SeatMotion.Facing(_motionCar, Vector3.zero));
        }

        internal void StopSeatMotion()
        {
            _seatMotion?.Dispose();
            _seatMotion = null;
            _motionHost = null;
            ThirdPerson.SeatView(false);
        }

        internal void ResetSeatMotion()
        {
            StopSeatMotion();
            CompleteStepOut(false);
            _rollVelocity = Vector3.zero;
            _rollStarted = false;
            _motionReady = false;
            _motionCar = null;
        }
    }
}
