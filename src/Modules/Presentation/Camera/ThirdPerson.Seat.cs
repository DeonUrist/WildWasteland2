using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    internal static partial class ThirdPerson
    {
        private const string SeatViewName = "Presentation.SeatView";
        private static bool _seatView;
        private static Transform _seatCaptureCar, _seatPathCar;
        private static int _seatCaptureFrame;
        private static Vector3 _seatPathStart, _seatPathEnd;
        private static Quaternion _seatPathRotationStart, _seatPathRotationEnd;
        internal static bool SeatViewActive => _seatView;

        internal static void CaptureSeatView(Transform car)
        {
            _seatCaptureCar = null;
            var camera = PlayerState.PlayerCamera;
            if (car == null || camera == null || !Game.FirstPersonCameraOn || On && !Peek
                || !CharacterSettings.FirstPersonCarEntryAnimations.Value) return;
            var eye = _seatView && _seatPathCar == car ? ViewPos
                : PlayerState.InCar && CharacterSettings.BodyInCar ? CarShiftEye(camera) : camera.position;
            var rotation = _seatView && _seatPathCar == car ? ViewRot : camera.rotation;
            _seatCaptureCar = car;
            _seatCaptureFrame = Time.frameCount;
            _seatPathStart = car.InverseTransformPoint(eye);
            _seatPathRotationStart = Quaternion.Inverse(car.rotation) * rotation;
        }

        internal static void SeatView(bool active)
        {
            if (_seatView == active) return;
            _seatView = active;
            _seatPathCar = null;
            if (active) CameraArbiter.AddView(PresentationModule.WorldOwner, SeatViewName, 300, WriteSeatView);
            else { CameraArbiter.Remove(PresentationModule.WorldOwner, SeatViewName); HasView = false; }
        }

        private static bool WriteSeatView(Camera cam, ref Matrix4x4 view)
        {
            bool playerCamera = Game.FirstPersonCameraOn;
            if (cam != (playerCamera ? PlayerState.Camera : GameRefs.MainCamera)) return false;
            var body = BodyRunner.PlayerBody;
            Vector3 eye;
            Quaternion rotation;
            if (body == null || !body.TrySeatEye(out eye, out rotation)) return false;

            var position = eye;
            if (!playerCamera || On && !Peek || body.SeatMotionNativeThird)
            {
                _seatPathCar = null;
                if (body.RollingFromCar) rotation = Quaternion.LookRotation(body.RollDirection, Vector3.up);
                bool seated = PlayerState.InCar;
                var offset = rotation * new Vector3(seated ? 0f : CameraSettings.ThirdShoulder.Value,
                    seated ? CameraSettings.ThirdCarHeight.Value : CameraSettings.ThirdHeight.Value, -Distance(seated));
                float distance = offset.magnitude;
                var direction = offset / distance;
                var car = body.SeatMotionVehicle;
                int count = Physics.SphereCastNonAlloc(eye, .2f, direction, _hits, distance, Mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = _hits[i];
                    if (hit.collider == null || Mine(hit.collider.transform) || car != null && hit.collider.transform.IsChildOf(car)) continue;
                    if (hit.distance > 0f) distance = Mathf.Min(distance, hit.distance);
                }
                position += direction * distance;
                rotation = Quaternion.LookRotation(-direction, rotation * Vector3.up);
            }
            else TrySeatPan(body, out position, out rotation);
            ViewPos = position; ViewRot = rotation; ViewFov = cam.fieldOfView; HasView = true;
            view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            return true;
        }

        internal static bool TrySeatPan(Body body, out Vector3 eye, out Quaternion rotation)
        {
            body.TrySeatEye(out eye, out rotation);
            var car = body.SeatMotionVehicle;
            if (car == null || _seatCaptureCar != car || !Game.FirstPersonCameraOn || On && !Peek
                || body.SeatMotionNativeThird || body.RollingFromCar
                || !body.TrySeatViewTarget(out var target, out var facing, out float progress)) return false;
            if (_seatPathCar != car)
            {
                if (Time.frameCount - _seatCaptureFrame > 2) return false;
                _seatPathCar = car;
                _seatPathEnd = car.InverseTransformPoint(target);
                _seatPathRotationEnd = Quaternion.Inverse(car.rotation) * facing;
                if (Log.L1) Log.Main(PresentationModule.WorldOwner, "First-person car camera pan: " + PlayerState.DriveUse?.ActiveStateName
                    + " from " + _seatPathStart.ToString("F3") + " to " + _seatPathEnd.ToString("F3") + " (car local)");
            }
            eye = car.TransformPoint(Vector3.Lerp(_seatPathStart, _seatPathEnd, progress));
            rotation = car.rotation * Quaternion.Slerp(_seatPathRotationStart, _seatPathRotationEnd, progress);
            return true;
        }

        internal static void FinishSeatPan(bool turnBody)
        {
            var holder = PlayerState.CameraHolder;
            var camera = PlayerState.PlayerCamera;
            if (_seatPathCar == null || holder == null || camera == null || !Game.FirstPersonCameraOn || On && !Peek) return;
            var rotation = _seatPathCar.rotation * _seatPathRotationEnd;
            if (turnBody)
            {
                var player = PlayerState.Transform;
                player.rotation = rotation;
                if (player.TryGetComponent<Rigidbody>(out var rb)) rb.rotation = rotation;
            }
            holder.rotation = rotation * Quaternion.Inverse(camera.localRotation);
            var shift = PlayerState.InCar && CharacterSettings.BodyInCar
                ? PlayerState.Transform.forward * CarSeatSettings.CarCameraForward.Value : Vector3.zero;
            holder.position += _seatPathCar.TransformPoint(_seatPathEnd) - shift - camera.position;
            FsmUtil.Find(PlayerState.Player, "Look")?.Fsm.SetState("Reset");
        }
    }
}
