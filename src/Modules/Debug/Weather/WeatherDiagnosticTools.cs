using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using static WildWasteland.Core.WeatherDiagnosticState;

namespace WildWasteland.Modules.Debug
{
    internal sealed class WeatherDiagnosticTools : IWeatherDiagnosticControls
    {
        internal static readonly string[] Presets = { "Normal", "Engine baseline", "Regional atmosphere", "Wall only", "Canopy only", "Wall and canopy", "One hurricane - shape", "One hurricane - full", "Local clouds - native tint", "Local clouds - brown tint", "Native fog scope OFF", "Native fog scope ON" };
        private static readonly string[] Outputs = { "Normal", "Layer IDs", "Opacity", "Entry/exit distances", "Linear scene depth", "World ray direction", "Density only" };
        private static string _caption = "";
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        /// <summary>Applies the chosen weather shader diagnostics and updates the overlay caption.</summary>
        internal static void OnStateChanged()
        {
            Apply();
            _caption = "WEATHER DIAGNOSTICS | " + Presets[Preset] + " | " + Outputs[Output] + (Frozen ? " | FROZEN" : " | RUNNING");
            if (Log.L1) Log.Main(DebugModule.Instance, "[WeatherDiag] " + (Enabled ? _caption : "OFF - normal renderer") + "; no cause established by changing this view.");
        }
        /// <summary>Sets diagnostic-only shader globals; disabled state restores normal weather rendering.</summary>
        private static void Apply()
        {
            if (Enabled) Shader.EnableKeyword("WW_DIAGNOSTICS_ON"); else Shader.DisableKeyword("WW_DIAGNOSTICS_ON");
            Shader.SetGlobalInt("_WWDiagOutput",Enabled ? Output : 0);
            Shader.SetGlobalFloat("_WWDiagShape",ShapeOnly ? 1 : 0);
            Shader.SetGlobalFloat("_WWDiagNoAtmosphere",Enabled && !Atmosphere ? 1 : 0);
            Shader.SetGlobalFloat("_WWDiagSmokeLayer",Enabled && Preset == 3 ? 1 : Enabled && Preset == 4 ? 2 : 0);
        }
                /// <summary>Requests GUI work only while diagnostics and an available weather view are active.</summary>
                internal static bool WantsOverlay() { return Enabled && OverlayVisible; }
        /// <summary>Draws the current weather preset/data/freeze caption without changing the scene.</summary>
        internal static void DrawOverlay() { GUI.Box(new Rect(12,85,780,30),_caption); }
        /// <summary>Draws the existing temporary weather layer, shader-output and freeze choices.</summary>
        internal static void Controls(ToolLayout l)
        {
            l.Heading("Weather diagnosis");
            bool enabled = l.Toggle("Enable diagnostic controls (temporary)",Enabled);
            if (enabled != Enabled) { Enabled = enabled; if (!enabled) Freeze = false; WeatherDiagnosticState.Changed(); }
            if (!Enabled) return;
            l.Value("Layer preset",Presets[Preset]);
            int hit = l.Buttons("Previous layer", "Next layer");
            if (hit >= 0) { Preset = (Preset+(hit == 0 ? Presets.Length-1 : 1))%Presets.Length; WeatherDiagnosticState.Changed(); }
            l.Value("Shader data view",Outputs[Output]);
            hit = l.Buttons("Previous data view", "Next data view");
            if (hit >= 0) { Output = (Output+(hit == 0 ? Outputs.Length-1 : 1))%Outputs.Length; WeatherDiagnosticState.Changed(); }
            bool freeze = l.Toggle("Freeze owned storm state / particle simulation",Freeze);
            if (freeze != Freeze) { Freeze = freeze; WeatherDiagnosticState.Changed(); }
            l.Text("Normal output is unchanged with diagnostics off. Freeze leaves the player/camera and native sky/time running; native sand shader UV time also continues. Local-cloud paired views share source parameters and differ only in tint. Shader data views apply to WW shaders, not the borrowed native sand material.");
            if (WeatherDiagnosticState.Single) l.Value("Selected hurricane",SelectedColumn < 0 ? "Waiting for a critical storm" : "region "+SelectedRegion+", column "+SelectedColumn);
        }
        /// <summary>Writes the requested camera/projection details only under the shared deep log policy.</summary>
        internal static void OnCameraRecord(Camera camera, float clock)
        {
            if (!Enabled || camera == null || !Log.L2) return;
            if (Log.L1) Log.Main(DebugModule.Instance, "[WeatherDiag] Camera "+camera.name+"; clock="+clock.ToString("R",Culture)+"; "+Screen.width+"x"+Screen.height+"; FOV="+camera.fieldOfView.ToString("R",Culture)+"; quality="+QualitySettings.GetQualityLevel()+"; colorSpace="+QualitySettings.activeColorSpace+"; MSAA="+QualitySettings.antiAliasing+"; projection="+camera.projectionMatrix.ToString("F6")+"; view="+camera.worldToCameraMatrix.ToString("F6"));
        }

        /// <summary>Draws diagnostic controls and delegates live capture/restore to Survival through Core.</summary>
        public void Draw(ToolLayout layout)
        {
            Controls(layout);
            if (!Enabled) return;
            var runtime = ModApi.Get<IWeatherDiagnosticRuntime>();
            if (layout.Button("Capture snapshot and freeze", runtime != null && runtime.CanCapture))
            {
                try { Freeze = true; WeatherDiagnosticState.Changed(); Write(runtime.Capture()); }
                catch (Exception error) { Log.Error(DebugModule.Instance, "Weather snapshot capture failed", error); }
            }
            if (layout.Button("Restore diagnostic snapshot", runtime != null && GameRefs.InPlay))
            {
                try
                {
                    string outcome;
                    if (!runtime.Restore(Read(), out outcome)) Log.Warn(DebugModule.Instance, outcome);
                    else if (Log.L1) Log.Main(DebugModule.Instance, outcome);
                }
                catch (Exception error) { Log.Error(DebugModule.Instance, "Weather snapshot restore failed", error); }
            }
            if (layout.Button("Select hurricane again (nearest in first region)", runtime != null && runtime.CanCapture))
            { SelectedRegion = SelectedColumn = -1; WeatherDiagnosticState.Changed(); }
        }
        /// <summary>Debug-owned version-1 diagnostic snapshot path.</summary>
        internal static string SnapshotPath { get { return Path.Combine(Assets.ModuleConfigDir("Debug"),"diagnostic-snapshot.bin"); } }
        /// <summary>Writes a same-directory temporary snapshot and publishes it through Core AtomicFile.</summary>
        internal static void Write(Snapshot snapshot)
        {
            string temporary = SnapshotPath + ".tmp";
            using (var writer = new BinaryWriter(File.Create(temporary))) WriteSnapshot(writer,snapshot);
            AtomicFile.Replace(temporary, SnapshotPath);
            if (Log.L1) Log.Main(DebugModule.Instance, "[WeatherDiag] Captured snapshot: "+SnapshotPath+"; slot="+snapshot.Slot+"; world seed="+snapshot.WorldSeed+"; camera world="+snapshot.CameraWorld.ToString("F3")+"; camera is recorded, never teleported.");
        }
        /// <summary>Reads and validates the existing binary weather snapshot header, version and bounded arrays.</summary>
        internal static Snapshot Read()
        {
            using (var reader = new BinaryReader(File.OpenRead(SnapshotPath)))
            {
                if (reader.ReadInt32() != 0x57574431) throw new InvalidDataException("Not a WW diagnostic snapshot");
                var d = new Snapshot(); d.Version=reader.ReadInt32();
                if (d.Version != 1) throw new InvalidDataException("Unsupported diagnostic snapshot");
                d.WorldSeed=reader.ReadInt32(); d.SelectedRegion=reader.ReadInt32(); d.SelectedColumn=reader.ReadInt32();
                d.ResolutionX=reader.ReadInt32(); d.ResolutionY=reader.ReadInt32();
                d.Slot=reader.ReadString(); d.CapturedUtc=reader.ReadString(); d.CameraName=reader.ReadString(); d.Policy=reader.ReadString();
                d.Clock=reader.ReadSingle(); d.Width=reader.ReadSingle(); d.Depth=reader.ReadSingle(); d.Density=reader.ReadSingle(); d.Fov=reader.ReadSingle();
                d.CameraWorld=ReadVector(reader); d.CameraForward=ReadVector(reader); d.ParticleRoot=ReadVector(reader);
                d.ParticleRotation=new Quaternion(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                d.Projection=ReadMatrix(reader); d.View=ReadMatrix(reader); d.ParticleSeed=reader.ReadUInt32();
                d.DustColor=ReadFour(reader); d.AirColor=ReadFour(reader); d.SunDirection=ReadFour(reader);
                int count=ReadCount(reader,4); d.Regions=new WeatherRegion[count]; d.Ages=new float[count];
                for (int i=0;i<count;i++)
                {
                    d.Regions[i]=new WeatherRegion { WorldCenter=ReadVector(reader), Radii=new Vector2(reader.ReadSingle(),reader.ReadSingle()),
                        Velocity=ReadVector(reader), Heading=reader.ReadSingle(), Hurricane=reader.ReadBoolean() };
                    d.Ages[i]=reader.ReadSingle();
                }
                count=ReadCount(reader,320); d.GroundOffsets=new float[count]; d.GroundValid=new bool[count];
                for (int i=0;i<count;i++) { d.GroundOffsets[i]=reader.ReadSingle(); d.GroundValid[i]=reader.ReadBoolean(); }
                if (reader.ReadBoolean())
                {
                    count=ReadCount(reader,1000); d.Particles=new Particle[count];
                    for (int i=0;i<count;i++) d.Particles[i]=new Particle { World=ReadVector(reader), Velocity=ReadVector(reader),
                        Size=ReadVector(reader), Rotation=ReadVector(reader), Color=new Color32(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),reader.ReadByte()),
                        Life=reader.ReadSingle(), StartLife=reader.ReadSingle(), Seed=reader.ReadUInt32() };
                }
                return d;
            }
        }
        /// <summary>Writes the unchanged version-1 camera/weather/particle value layout.</summary>
        private static void WriteSnapshot(BinaryWriter w, Snapshot d)
        {
            w.Write(0x57574431); w.Write(d.Version); w.Write(d.WorldSeed); w.Write(d.SelectedRegion); w.Write(d.SelectedColumn);
            w.Write(d.ResolutionX); w.Write(d.ResolutionY); w.Write(d.Slot ?? ""); w.Write(d.CapturedUtc ?? ""); w.Write(d.CameraName ?? ""); w.Write(d.Policy ?? "");
            w.Write(d.Clock); w.Write(d.Width); w.Write(d.Depth); w.Write(d.Density); w.Write(d.Fov);
            WriteVector(w,d.CameraWorld); WriteVector(w,d.CameraForward); WriteVector(w,d.ParticleRoot);
            w.Write(d.ParticleRotation.x); w.Write(d.ParticleRotation.y); w.Write(d.ParticleRotation.z); w.Write(d.ParticleRotation.w);
            WriteMatrix(w,d.Projection); WriteMatrix(w,d.View); w.Write(d.ParticleSeed);
            WriteFour(w,d.DustColor); WriteFour(w,d.AirColor); WriteFour(w,d.SunDirection);
            w.Write(d.Regions.Length);
            for (int i=0;i<d.Regions.Length;i++)
            {
                var r=d.Regions[i]; WriteVector(w,r.WorldCenter); w.Write(r.Radii.x); w.Write(r.Radii.y); WriteVector(w,r.Velocity);
                w.Write(r.Heading); w.Write(r.Hurricane); w.Write(d.Ages[i]);
            }
            w.Write(d.GroundOffsets.Length);
            for (int i=0;i<d.GroundOffsets.Length;i++) { w.Write(d.GroundOffsets[i]); w.Write(d.GroundValid[i]); }
            w.Write(d.Particles != null);
            if (d.Particles == null) return;
            w.Write(d.Particles.Length);
            foreach (var p in d.Particles)
            {
                WriteVector(w,p.World); WriteVector(w,p.Velocity); WriteVector(w,p.Size); WriteVector(w,p.Rotation);
                w.Write(p.Color.r); w.Write(p.Color.g); w.Write(p.Color.b); w.Write(p.Color.a); w.Write(p.Life); w.Write(p.StartLife); w.Write(p.Seed);
            }
        }
        /// <summary>Rejects negative or oversized persisted array lengths before allocation.</summary>
        private static int ReadCount(BinaryReader r, int max) { int count=r.ReadInt32(); if (count<0 || count>max) throw new InvalidDataException("Invalid snapshot array length"); return count; }
        /// <summary>Writes a three-float vector in the established binary field order.</summary>
        private static void WriteVector(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        /// <summary>Reads a three-float vector in the established binary field order.</summary>
        private static Vector3 ReadVector(BinaryReader r) { return new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()); }
        /// <summary>Writes a four-float vector in the established binary field order.</summary>
        private static void WriteFour(BinaryWriter w, Vector4 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); w.Write(v.w); }
        /// <summary>Reads a four-float vector in the established binary field order.</summary>
        private static Vector4 ReadFour(BinaryReader r) { return new Vector4(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()); }
        /// <summary>Writes matrix entries in Unity index order for version-1 snapshots.</summary>
        private static void WriteMatrix(BinaryWriter w, Matrix4x4 m) { for (int i=0;i<16;i++) w.Write(m[i]); }
        /// <summary>Reads matrix entries in Unity index order for version-1 snapshots.</summary>
        private static Matrix4x4 ReadMatrix(BinaryReader r) { var m=new Matrix4x4(); for (int i=0;i<16;i++) m[i]=r.ReadSingle(); return m; }
    }
}
