using WildWasteland.Core;
using WildWasteland.Modules.CleanHUD;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// <see cref="IDrivingTelemetry"/> provider fed by the ChaseCamera snapshot that <see cref="ChaseCameraHud"/> records (README 6.12).
    /// Values are those of the last snapshot; <see cref="Visible"/> is true only when a snapshot was recorded this frame.
    /// </summary>
    internal sealed class ChaseCameraTelemetry : IDrivingTelemetry
    {
        /// <summary>A snapshot was recorded this frame (player driving, mod present, HUD gates passed).</summary>
        public bool Visible { get { return ChaseCameraHud.Live; } }
        /// <summary>Speed, km/h.</summary>
        public float SpeedKph { get { return ChaseCameraHud.Speed; } }
        /// <summary>Engine rpm.</summary>
        public float Rpm { get { return ChaseCameraHud.Rpm; } }
        /// <summary>Rpm as a fraction of the redline.</summary>
        public float RpmFraction { get { return ChaseCameraHud.Fraction; } }
        /// <summary>Gear label ("N", "R", "3"); null when no snapshot is live.</summary>
        public string GearText { get { return ChaseCameraHud.Live ? ChaseCameraHud.Gear : null; } }
        /// <summary>Compass heading rendered by the chase camera, degrees (0 when unavailable).</summary>
        public float Heading { get { float yaw; return ChaseCameraHud.TryHeading(out yaw) ? yaw : 0f; } }
    }
}
