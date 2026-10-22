using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// <see cref="ICameraRig"/> provider of the Presentation World part (README P2): view mode, player view pose, the shared eye/aim ray of the
    /// frame, crosshair position and mouse-wheel ownership. Projection / view writes themselves go through Core <see cref="CameraArbiter"/>
    /// (ThirdPerson's writers). Also publishes <see cref="CameraModeChanged"/> (ThirdPerson.Tick calls <see cref="Publish"/> once per frame).
    /// </summary>
    internal sealed class CameraRigApi : ICameraRig
    {
        static CameraViewMode _published = CameraViewMode.FirstPerson;
        static int _aimFrame = -1; static bool _aimOk; static Ray _aimRay; static Vector3 _aimHit;
        /// <summary>The game's gun layers (the player guns' Raycast mask), as ThirdPerson's crosshair ray.</summary>
        const int GunMask = (1 << 0) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 13) | (1 << 14);

        /// <summary>Current view mode derived from the third-person state.</summary>
        public CameraViewMode Mode { get { return Current; } }

        /// <summary>Current view mode (static so ThirdPerson can compare without the provider).</summary>
        internal static CameraViewMode Current
        {
            get
            {
                if (!PresentationModule.WorldRunning || !ThirdPerson.On) return CameraViewMode.FirstPerson;
                if (ThirdPerson.Peek) return CameraViewMode.Peek;
                return ThirdPerson.Orbiting ? CameraViewMode.Orbit : CameraViewMode.ThirdPerson;
            }
        }

        /// <summary>True in third person (not while peeking through binoculars / a scope).</summary>
        bool ICameraRig.ThirdPerson { get { return ThirdPerson.On && !ThirdPerson.Peek; } }

        /// <summary>The drawn view: the third-person view of the last render, else the game camera.</summary>
        public bool TryGetPlayerView(out Vector3 position, out Quaternion rotation, out float fov)
        {
            if (ThirdPerson.On && ThirdPerson.HasView && !ThirdPerson.Peek)
            { position = ThirdPerson.ViewPos; rotation = ThirdPerson.ViewRot; fov = ThirdPerson.ViewFov; return true; }
            var cam = Game.Cam;
            if (cam == null) { position = Vector3.zero; rotation = Quaternion.identity; fov = 60f; return false; }
            var t = cam.transform; position = t.position; rotation = t.rotation; fov = cam.fieldOfView;
            return true;
        }

        /// <summary>The eye ray (game camera forward, where shots and picks start) and its first hit on the gun layers; cast at most once per frame.</summary>
        public bool TryGetAimRay(out Ray ray, out Vector3 hitPoint)
        {
            if (_aimFrame != Time.frameCount)
            {
                _aimFrame = Time.frameCount;
                var cam = Game.Cam;
                _aimOk = cam != null;
                if (_aimOk)
                {
                    var t = cam.transform;
                    _aimRay = new Ray(t.position, t.forward);
                    RaycastHit h;
                    _aimHit = Physics.Raycast(_aimRay, out h, 150f, GunMask, QueryTriggerInteraction.Ignore) ? h.point : t.position + t.forward * 1000f;
                }
            }
            ray = _aimRay; hitPoint = _aimHit;
            return _aimOk;
        }

        /// <summary>Crosshair position in screen pixels (the dynamic crosshair, else the screen centre).</summary>
        public Vector2 CrosshairScreen { get { return ThirdPerson.HasCrosshair ? ThirdPerson.CrosshairScreen : new Vector2(Screen.width * .5f, Screen.height * .5f); } }

        /// <summary>True while the third-person camera zooms with the mouse wheel (on foot or in a car, not peeking, not paused).</summary>
        public bool OwnsMouseWheel { get { return ThirdPerson.On && !ThirdPerson.Peek && !Game.Paused; } }

        /// <summary>Publishes <see cref="CameraModeChanged"/> when the view mode differs from the last published one (cheap enum compare per frame).</summary>
        internal static void Publish()
        {
            var now = Current;
            if (now == _published) return;
            var e = new CameraModeChanged { Old = _published, New = now };
            _published = now;
            ModEvents.Publish(ref e);
        }
    }
}
