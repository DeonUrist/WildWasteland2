using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    /// <summary>Projection-matrix zoom of the player camera while aiming down sights (Wave A split; B01 folds it into CameraArbiter, shared with Binoculars and ThirdPerson). Subscribed to Camera.onPreCull/onPostRender in Ironsights.Init.</summary>
    internal static partial class Ironsights
    {
        private static void PreCull(Camera cam)
        {
            if (cam != _camera || !GunsModule.IronsightWheelZoom.Value || ThirdPerson || !GameRefs.InPlay || !Aiming || Mathf.Abs(_zoom - 1f) < .0001f) return;
            Restore();
            _renderCamera = cam; _transparentProjection = cam.useJitteredProjectionMatrixForTransparentRendering;
            var projection = cam.projectionMatrix;
            projection.m00 /= _zoom; projection.m11 /= _zoom;
            cam.projectionMatrix = projection;
            cam.useJitteredProjectionMatrixForTransparentRendering = true;
            cam.ResetCullingMatrix();
        }
        private static void PostRender(Camera cam) { if (cam == _renderCamera) Restore(); }
        private static void Restore()
        {
            if (_renderCamera == null) return;
            _renderCamera.useJitteredProjectionMatrixForTransparentRendering = _transparentProjection;
            _renderCamera.ResetProjectionMatrix(); _renderCamera.ResetCullingMatrix(); _renderCamera = null;
        }
    }
}
