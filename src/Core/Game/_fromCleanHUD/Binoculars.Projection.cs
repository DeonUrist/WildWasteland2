using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    /// <summary>Projection-matrix zoom of the player camera while binoculars are raised and the scope slope capture (Wave A split; B01 folds it into CameraArbiter with Ironsights and ThirdPerson). Subscribed to Camera.onPreCull/onPostRender in Binoculars.Init; the wheel/lerp target stays in Binoculars.Tick (HUD).</summary>
    internal static partial class Binoculars
    {
        private static void PreCull(Camera camera)
        {
            if (camera != _camera) return;
            if (Scoped) { _scopeSlope = 1f / Mathf.Max(.0001f, Mathf.Abs(camera.projectionMatrix.m11)); return; }
            if (!Raised || !GameRefs.InPlay || _zoom <= 1f) return;
            Restore(); _renderCamera = camera;
            _transparentProjection = camera.useJitteredProjectionMatrixForTransparentRendering;
            var projection = camera.projectionMatrix; projection.m00 *= _zoom; projection.m11 *= _zoom;
            camera.projectionMatrix = projection; camera.useJitteredProjectionMatrixForTransparentRendering = true;
            camera.ResetCullingMatrix();
        }
        private static void PostRender(Camera camera) { if (camera == _renderCamera) Restore(); }
        private static void Restore()
        {
            if (_renderCamera == null) return;
            _renderCamera.useJitteredProjectionMatrixForTransparentRendering = _transparentProjection;
            _renderCamera.ResetProjectionMatrix(); _renderCamera.ResetCullingMatrix(); _renderCamera = null;
        }
    }
}
