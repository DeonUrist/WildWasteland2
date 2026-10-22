using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;
namespace WildWasteland.Modules.Guns
{
    /// <summary>Module zoom policy; Core owns native camera matrix publication and restoration.</summary>
    internal static partial class Ironsights
    {
        static IModule _projectionOwner;
        /// <summary>Claims the projection channel only while this module's native aiming mode is active.</summary>
        private static bool WriteProjection(Camera cam, ref Matrix4x4 projection)
        {
            if (cam != _camera || !GunplaySettings.IronsightWheelZoom.Value || ThirdPerson || !GameRefs.InPlay || !Aiming || Mathf.Abs(_zoom - 1f) < .0001f) return false;
            projection.m00 /= _zoom; projection.m11 /= _zoom;
            return true;
        }
        /// <summary>Releases the current render override without removing the module's writer.</summary>
        private static void Restore() { CameraArbiter.Restore(_projectionOwner, "Gunplay.Ironsights"); }
    }
}
