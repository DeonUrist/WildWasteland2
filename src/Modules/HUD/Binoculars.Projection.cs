using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.CleanHUD
{
    /// <summary>Module zoom policy; Core owns native camera matrix publication and restoration.</summary>
    internal static partial class Binoculars
    {
        static IModule _projectionOwner;
        /// <summary>Claims the projection channel only while this module's native aiming mode is active.</summary>
        private static bool WriteProjection(Camera cam, ref Matrix4x4 projection)
        {
            if (cam == _camera && Scoped) { _scopeSlope = 1f / Mathf.Max(.0001f, Mathf.Abs(projection.m11)); return false; }
            if (cam != _camera || !Raised || !GameRefs.InPlay || _zoom <= 1f) return false;
            projection.m00 *= _zoom; projection.m11 *= _zoom;
            return true;
        }
        /// <summary>Releases the current render override without removing the module's writer.</summary>
        private static void Restore() { CameraArbiter.Restore(_projectionOwner, "HUD.Binoculars"); }
    }
}
