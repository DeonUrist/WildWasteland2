using System;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Value-only weather diagnostic state shared by Debug controls and the Survival renderer. With Debug disabled all
    /// flags are reset and the normal renderer remains selected. No UI, file IO or module types are referenced here.</summary>
    public static class WeatherDiagnosticState
    {
        public static bool Enabled, Freeze, PendingCamera;
        public static int Preset, Output, SelectedRegion = -1, SelectedColumn = -1;
        public static bool OverlayVisible;

        /// <summary>Debug subscribes to update shader globals and its overlay caption when controls change.</summary>
        public static event Action StateChanged;
        /// <summary>Survival requests a camera diagnostic line; Debug owns whether and how it is logged.</summary>
        public static event Action<Camera, float> CameraRecordRequested;
        /// <summary>Owned weather and particle clocks are frozen only while diagnostics explicitly enable freezing.</summary>
        public static bool Frozen { get { return Enabled && Freeze; } }
        /// <summary>The normal complete renderer, including native-fog comparison presets.</summary>
        public static bool Full { get { return !Enabled || Preset == 0 || Preset >= 10; } }
        /// <summary>Shader depth/ray debug outputs hide weather geometry.</summary>
        private static bool ScreenData { get { return Enabled && (Output == 4 || Output == 5); } }
        /// <summary>Front wall layer visibility.</summary>
        public static bool Wall { get { return !ScreenData && (Full || Preset == 3 || Preset == 5); } }
        /// <summary>Front canopy layer visibility.</summary>
        public static bool Canopy { get { return !ScreenData && (Full || Preset == 4 || Preset == 5); } }
        /// <summary>Hurricane column layer visibility.</summary>
        public static bool Core { get { return !ScreenData && (Full || Preset == 6 || Preset == 7); } }
        /// <summary>Local dust cloud visibility.</summary>
        public static bool Clouds { get { return !ScreenData && (Full || Preset == 8 || Preset == 9); } }
        /// <summary>Regional atmosphere visibility.</summary>
        public static bool Atmosphere { get { return Full || Preset == 2; } }
        /// <summary>Sky darkening follows the selected atmosphere layer.</summary>
        public static bool SkyScope { get { return Atmosphere; } }
        /// <summary>Native fog comparison preset 10 suppresses the fog scope.</summary>
        public static bool FogScope { get { return SkyScope && (!Enabled || Preset != 10); } }
        /// <summary>The hurricane diagnostic presets display one selected column.</summary>
        public static bool Single { get { return Enabled && (Preset == 6 || Preset == 7); } }
        /// <summary>Hurricane shape-only comparison mode.</summary>
        public static bool ShapeOnly { get { return Enabled && Preset == 6; } }
        /// <summary>The two cloud comparison presets share the same source particle parameters.</summary>
        public static bool CloudPair { get { return Enabled && (Preset == 8 || Preset == 9); } }

        /// <summary>Restores the normal renderer selection and releases diagnostic freeze/selection state.</summary>
        public static void Reset()
        {
            Enabled = Freeze = PendingCamera = OverlayVisible = false;
            Preset = Output = 0; SelectedRegion = SelectedColumn = -1;
            var changed = StateChanged; if (changed != null) changed();
        }
        /// <summary>Notifies Debug that controls changed; the next weather camera render is eligible for a diagnostic line.</summary>
        public static void Changed()
        {
            PendingCamera = Enabled;
            var changed = StateChanged; if (changed != null) changed();
        }
        /// <summary>Survival declares whether a playing weather view is available for the Debug overlay.</summary>
        public static void Show(bool visible) { OverlayVisible = visible; }
        /// <summary>Passes a camera-record request to Debug without retaining the scene camera in persisted state.</summary>
        public static void CameraRecord(Camera camera, float clock)
        {
            if (!Enabled || !PendingCamera || camera == null) return;
            PendingCamera = false;
            var record = CameraRecordRequested; if (record != null) record(camera, clock);
        }

        [Serializable]
        public sealed class Snapshot
        {
            public int Version = 1, WorldSeed, SelectedRegion, SelectedColumn, ResolutionX, ResolutionY;
            public string Slot, CapturedUtc, CameraName, Policy;
            public float Clock, Width, Depth, Density, Fov;
            public WeatherRegion[] Regions;
            public float[] Ages, GroundOffsets;
            public bool[] GroundValid;
            public Vector3 CameraWorld, CameraForward, ParticleRoot;
            public Vector4 DustColor, AirColor, SunDirection;
            public Quaternion ParticleRotation;
            public Matrix4x4 Projection, View;
            public uint ParticleSeed;
            public Particle[] Particles;
        }
        [Serializable]
        public struct Particle
        {
            public Vector3 World, Velocity, Size, Rotation;
            public Color32 Color;
            public float Life, StartLife;
            public uint Seed;
        }
    }

    /// <summary>Survival's live weather snapshot provider. Debug owns snapshot storage and controls; the provider alone touches
    /// runtime weather/particle state. Snapshot version/validation and restoration semantics stay identical to WW's diagnostics.</summary>
    public interface IWeatherDiagnosticRuntime
    {
        /// <summary>True while at least one owned storm can be captured.</summary>
        bool CanCapture { get; }
        /// <summary>Captures copied value arrays from the live renderer. No scene references are retained.</summary>
        WeatherDiagnosticState.Snapshot Capture();
        /// <summary>Validates/restores the same-world snapshot without moving the camera/player; returns a user-facing outcome.</summary>
        bool Restore(WeatherDiagnosticState.Snapshot snapshot, out string message);
    }
    /// <summary>Debug-owned controls that Tools can include without referencing the Debug module.</summary>
    public interface IWeatherDiagnosticControls
    {
        /// <summary>Draws weather layer controls and snapshot capture/restore actions.</summary>
        void Draw(UI.ToolLayout layout);
    }

}
