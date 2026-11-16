using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    internal static partial class ThirdPerson
    {
        private const string SeatViewName = "Presentation.SeatView";
        private static bool _seatView;
        internal static bool SeatViewActive => _seatView;

        internal static void SeatView(bool active)
        {
            if (_seatView == active) return;
            _seatView = active;
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
            ViewPos = position; ViewRot = rotation; ViewFov = cam.fieldOfView; HasView = true;
            view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            return true;
        }
    }
}
