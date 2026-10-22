using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Weather
{
    /// <summary>
    /// Debug part of the Weather entry (Wave A split, owner B03): the temporary render diagnostics (overlay label, snapshot
    /// capture/restore to config/WildWasteland/Weather/diagnostic-snapshot.bin, preview page diagnostic controls).
    /// </summary>
    public sealed partial class WeatherModule
    {
        /// <summary>Registers the diagnostic overlay label (Gui job, drawn only while WeatherDiagnostics wants it).</summary>
        partial void InitDebug()
        {
            Scheduler.Gui(this,"Weather diagnostic label",WeatherDiagnostics.DrawOverlay,WeatherDiagnostics.WantsOverlay);
        }

        /// <summary>Draws the diagnostic controls and snapshot buttons of the Weather preview page.</summary>
        partial void DrawDebugTools(ToolLayout l)
        {
            WeatherDiagnostics.Controls(l);
            if (WeatherDiagnostics.Enabled)
            {
                if (l.Button("Capture snapshot and freeze",_count > 0)) CaptureDiagnostic();
                if (l.Button("Restore diagnostic snapshot",_running && GameRefs.InPlay)) RestoreDiagnostic();
                if (l.Button("Select hurricane again (nearest in first region)",_count > 0))
                { WeatherDiagnostics.SelectedRegion = WeatherDiagnostics.SelectedColumn = -1; WeatherDiagnostics.Changed(); }
            }
        }

        /// <summary>Freezes the storms and writes the camera, regions and particle state to the diagnostic snapshot file.</summary>
        private void CaptureDiagnostic()
        {
            try
            {
                WeatherDiagnostics.Freeze = true; WeatherDiagnostics.Changed();
                var camera = GameRefs.MainCamera;
                var data = new WeatherDiagnostics.Snapshot { Clock = _clock, Width = _width.Value, Depth = _depth.Value, Density = _density.Value,
                    Slot = SaveBus.Slot, WorldSeed = WeatherDiagnostics.WorldSeed(), CapturedUtc = System.DateTime.UtcNow.ToString("o"),
                    SelectedRegion = WeatherDiagnostics.SelectedRegion, SelectedColumn = WeatherDiagnostics.SelectedColumn,
                    ResolutionX = Screen.width, ResolutionY = Screen.height, CameraName = camera.name, Fov = camera.fieldOfView,
                    CameraWorld = Origin.World(camera.worldToCameraMatrix.inverse.MultiplyPoint3x4(Vector3.zero)), CameraForward = camera.transform.forward,
                    Projection = camera.projectionMatrix, View = camera.worldToCameraMatrix,
                    Policy = Services.WeatherVisualPolicy != null ? Services.WeatherVisualPolicy.WeatherDetail.ToString() : "Balanced",
                    Regions = new WeatherRegion[_count], Ages = new float[_count] };
                System.Array.Copy(_regions,data.Regions,_count); System.Array.Copy(_ages,data.Ages,_count);
                _visuals.CaptureDiagnostic(data); WeatherDiagnostics.Write(data);
            }
            catch (System.Exception error) { Log.Error("[WeatherDiag] Snapshot capture failed",error); }
        }
        /// <summary>Restores a diagnostic snapshot taken in the same save/world/settings and freezes it (camera/player are not moved).</summary>
        private void RestoreDiagnostic()
        {
            try
            {
                var data = WeatherDiagnostics.Read();
                if (data == null || data.Version != 1 || data.Slot != SaveBus.Slot || data.WorldSeed != WeatherDiagnostics.WorldSeed() ||
                    data.Width != _width.Value || data.Depth != _depth.Value || data.Density != _density.Value ||
                    data.Regions == null || data.Ages == null || data.Regions.Length == 0 || data.Regions.Length > 4 || data.Ages.Length != data.Regions.Length ||
                    data.SelectedRegion < -1 || data.SelectedRegion >= data.Regions.Length || data.SelectedColumn < -1 || data.SelectedColumn >= 80)
                { Log.Warn("[WeatherDiag] Snapshot rejected: use the same save, world seed and Weather width/depth/density."); return; }
                string policy = Services.WeatherVisualPolicy != null ? Services.WeatherVisualPolicy.WeatherDetail.ToString() : "Balanced";
                if (data.Policy != policy || data.ResolutionX != Screen.width || data.ResolutionY != Screen.height)
                    Log.Warn("[WeatherDiag] Snapshot graphics settings differ: captured "+data.Policy+" at "+data.ResolutionX+"x"+data.ResolutionY+"; restore those manually before comparing.");
                if (_visuals == null) _visuals = new WeatherVisuals(_audio);
                if (!_visuals.Ready) return;
                Clear(); _audio.Stop(); UpdateLayout(); _clock = data.Clock; _count = data.Regions.Length;
                System.Array.Copy(data.Regions,_regions,_count); System.Array.Copy(data.Ages,_ages,_count);
                for (int i = 0; i < _count; i++) _criticalLogged[i] = _ages[i] >= 20;
                WeatherDiagnostics.Freeze = true; WeatherDiagnostics.SelectedRegion = data.SelectedRegion; WeatherDiagnostics.SelectedColumn = data.SelectedColumn;
                _visuals.RestoreDiagnostic(data); _draw.Enabled = true; WeatherDiagnostics.Changed();
                Log.Always("[WeatherDiag] Restored storm/particle snapshot; recorded camera world="+data.CameraWorld.ToString("F3")+"; camera/player were not moved. Native sky and shader time were not restored.");
            }
            catch (System.Exception error) { Log.Error("[WeatherDiag] Snapshot restore failed",error); }
        }
    }
}
