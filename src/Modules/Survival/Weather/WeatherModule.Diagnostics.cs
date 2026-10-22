using System;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Weather
{
    /// <summary>Survival's value-only weather snapshot facade. Debug owns controls, shaders and files; this adapter alone reads
    /// and restores private live weather state. It preserves snapshot version 1 and does not move the player or camera.</summary>
    public sealed partial class WeatherModule : IWeatherDiagnosticRuntime
    {
        /// <summary>Registers live diagnostic operations without exposing renderer implementation types.</summary>
        public override void ProvideApis() { ModApi.Provide<IWeatherDiagnosticRuntime>(this, this); }

        /// <summary>A live weather region is available for a diagnostic snapshot.</summary>
        public bool CanCapture { get { return _running && _count > 0; } }

        /// <summary>Copies current camera, storm and particle values; capture freezes only the mod-owned weather state.</summary>
        public WeatherDiagnosticState.Snapshot Capture()
        {
            if (!CanCapture) throw new InvalidOperationException("No active weather region is available to capture.");
            var camera = GameRefs.MainCamera;
            if (camera == null || _visuals == null) throw new InvalidOperationException("Weather camera or renderer is not ready.");
            WeatherDiagnosticState.Freeze = true; WeatherDiagnosticState.Changed();
            var data = new WeatherDiagnosticState.Snapshot
            {
                Clock = _clock, Width = _width.Value, Depth = _depth.Value, Density = _density.Value,
                Slot = SaveBus.Slot, WorldSeed = DiagnosticWorldSeed(), CapturedUtc = DateTime.UtcNow.ToString("o"),
                SelectedRegion = WeatherDiagnosticState.SelectedRegion, SelectedColumn = WeatherDiagnosticState.SelectedColumn,
                ResolutionX = Screen.width, ResolutionY = Screen.height, CameraName = camera.name, Fov = camera.fieldOfView,
                CameraWorld = Origin.World(camera.worldToCameraMatrix.inverse.MultiplyPoint3x4(Vector3.zero)), CameraForward = camera.transform.forward,
                Projection = camera.projectionMatrix, View = camera.worldToCameraMatrix,
                Policy = Services.WeatherVisualPolicy != null ? Services.WeatherVisualPolicy.WeatherDetail.ToString() : "Balanced",
                Regions = new WeatherRegion[_count], Ages = new float[_count]
            };
            Array.Copy(_regions, data.Regions, _count); Array.Copy(_ages, data.Ages, _count);
            _visuals.CaptureDiagnostic(data);
            return data;
        }

        /// <summary>Validates/restores only matching save/world/settings snapshots; native sky and shader time remain live.</summary>
        public bool Restore(WeatherDiagnosticState.Snapshot data, out string message)
        {
            if (!_running || !GameRefs.InPlay || data == null || data.Version != 1 || data.Slot != SaveBus.Slot || data.WorldSeed != DiagnosticWorldSeed() ||
                data.Width != _width.Value || data.Depth != _depth.Value || data.Density != _density.Value ||
                data.Regions == null || data.Ages == null || data.Regions.Length == 0 || data.Regions.Length > 4 || data.Ages.Length != data.Regions.Length ||
                data.SelectedRegion < -1 || data.SelectedRegion >= data.Regions.Length || data.SelectedColumn < -1 || data.SelectedColumn >= 80)
            { message = "Weather snapshot rejected: use the same save, world seed and Weather width/depth/density."; return false; }
            string policy = Services.WeatherVisualPolicy != null ? Services.WeatherVisualPolicy.WeatherDetail.ToString() : "Balanced";
            if (data.Policy != policy || data.ResolutionX != Screen.width || data.ResolutionY != Screen.height)
                Log.Warn(this, "Weather snapshot graphics settings differ: captured " + data.Policy + " at " + data.ResolutionX + "x" + data.ResolutionY + "; restore those manually before comparing.");
            if (_visuals == null) _visuals = new WeatherVisuals(_audio);
            if (!_visuals.Ready) { message = "Weather renderer is not ready to restore the snapshot."; return false; }
            Clear(); _audio.Stop(); UpdateLayout(); _clock = data.Clock; _count = data.Regions.Length;
            Array.Copy(data.Regions, _regions, _count); Array.Copy(data.Ages, _ages, _count);
            for (int i = 0; i < _count; i++) _criticalLogged[i] = _ages[i] >= 20;
            WeatherDiagnosticState.Freeze = true; WeatherDiagnosticState.SelectedRegion = data.SelectedRegion; WeatherDiagnosticState.SelectedColumn = data.SelectedColumn;
            _visuals.RestoreDiagnostic(data); _draw.Enabled = true; WeatherDiagnosticState.Changed();
            message = "Restored storm/particle snapshot; recorded camera world=" + data.CameraWorld.ToString("F3") + "; camera/player were not moved. Native sky and shader time were not restored.";
            return true;
        }

        /// <summary>Reads the native world seed used by the existing snapshot validation.</summary>
        private static int DiagnosticWorldSeed()
        {
            var value = FsmIndex.Int(GameRefs.SaveLoadGame, "seed");
            return value != null ? value.Value : int.MinValue;
        }
    }
}
