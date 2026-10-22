using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    // (2.2.7, 2.2.8 under VerboseLog) diagnostics for slow-machine reports. VerboseLog off: nothing is timed or written (Now returns 0, Add and
    // Frame return at once). On: the PC once, then every 10 s the FPS, the slowest frame, the stutters, Apocaplayer's own CPU time per frame split
    // by where it is spent, garbage collections, the camera cutaway's extra views and the cameras that render.
    internal static class Perf
    {
        internal const int Update = 0, Late = 1, Camera = 2, ModApi = 3, Gui = 4, EndFrame = 5;
        private static readonly string[] Names = { "update", "late", "camera+cutaway", "modapi", "gui", "endframe" };
        private static readonly long[] _ticks = new long[6];
        private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;
        private static bool _on, _systemLogged, _wasInCar;
        private static int _frames, _over33, _over50, _gc0, _gc2, _renders;
        private static float _start = -1f, _next, _worst;

        internal static long Now { get { return _on ? Stopwatch.GetTimestamp() : 0L; } }
        internal static void Add(int part, long since) { if (since != 0L) _ticks[part] += Stopwatch.GetTimestamp() - since; }

        // once a frame, from Runner.Update
        internal static void Frame()
        {
            _on = PlayerBodyModule.VerboseOn && Game.Ready;
            if (!_on) { _start = -1f; StopGc(); return; }
            GcFrame();
            if (!_systemLogged) { _systemLogged = true; LogSystem(); }
            float now = Time.unscaledTime;
            bool car = Game.InCar;
            if (_start < 0f || car != _wasInCar) { _wasInCar = car; Clear(now); return; }   // a fresh window (the first frame after a load / getting in or out)
            float dt = Time.unscaledDeltaTime;
            _frames++;
            if (dt > _worst) _worst = dt;
            if (dt > 1f / 30f) _over33++;
            if (dt > 1f / 20f) _over50++;
            if (now < _next) return;
            try { Write(now - _start, car); }
            finally { Clear(now); }
        }

        private static void Write(float span, bool car)
        {
            if (_frames <= 0 || span <= 0f) return;
            var sb = new StringBuilder(512);
            double total = 0; for (int i = 0; i < _ticks.Length; i++) total += _ticks[i] * MsPerTick;
            bool cut = PlayerBodyModule.OcclusionPrototype.Value && (!car || PlayerBodyModule.OcclusionInVehicle.Value);
            sb.Append("Perf (").Append(car ? "car" : "on foot").Append(ThirdPerson.On ? ", third person" : ", first person").Append(cut ? ", cutaway on" : ", cutaway off").Append("): ")
              .Append((_frames / span).ToString("0.0")).Append(" fps, avg ").Append((span * 1000f / _frames).ToString("0.0")).Append(" ms, worst ").Append((_worst * 1000f).ToString("0"))
              .Append(" ms, ").Append(_over33).Append(" frames >33 ms, ").Append(_over50).Append(" >50 ms | Apocaplayer ").Append((total / _frames).ToString("0.00")).Append(" ms/frame [");
            for (int i = 0; i < _ticks.Length; i++) sb.Append(i > 0 ? ", " : "").Append(Names[i]).Append(' ').Append((_ticks[i] * MsPerTick / _frames).ToString("0.00"));
            sb.Append("] | GC alloc ");
            if (_gcFrames > 0) sb.Append((_gcBytes / 1024.0 / _gcFrames).ToString("0.0")).Append(" KB/frame (").Append((_gcBytes >> 20)).Append(" MB/window)"); else sb.Append("n/a");
            sb.Append(" | GC ").Append(GC.CollectionCount(0) - _gc0).Append(" (gen2 ").Append(GC.CollectionCount(2) - _gc2).Append("), heap ").Append((GC.GetTotalMemory(false) >> 20)).Append(" MB");
            if (cut && Cave.Inside) sb.Append(" | in a cave: culling off");
            if (cut) sb.Append(" | cutaway: ").Append(((OcclusionCutaway.Renders - _renders) / (float)_frames).ToString("0.00")).Append(" extra views/frame, ").Append(OcclusionCutaway.Blockers).Append(" blockers");
            sb.Append(" | MapCheat_Camera ").Append(MapCamState());
            sb.Append(" | shadows ").Append(QualitySettings.shadowDistance.ToString("0")).Append(" m | cameras:");
            foreach (var c in UnityEngine.Camera.allCameras)
                if (c != null && c.isActiveAndEnabled) sb.Append(' ').Append(c.name).Append(c.targetTexture != null ? "(rt " + c.targetTexture.width + "x" + c.targetTexture.height + ")" : "");
            PlayerBodyModule.Info(sb.ToString());
        }

        private static void Clear(float now)
        {
            for (int i = 0; i < _ticks.Length; i++) _ticks[i] = 0;
            _frames = _over33 = _over50 = 0; _worst = 0f; _start = now; _next = now + 10f;
            _gcBytes = 0; _gcFrames = 0;
            _gc0 = GC.CollectionCount(0); _gc2 = GC.CollectionCount(2); _renders = OcclusionCutaway.Renders;
        }

        // (WW) Unity's "GC Allocated In Frame" counter (Unity.Profiling.ProfilerRecorder, Unity 2020.2+): summed per window. Only while
        // VerboseLog is on; where the player doesn't provide the counter (Valid false) the line says n/a.
        private static Unity.Profiling.ProfilerRecorder _gcRec;
        private static bool _gcTried;
        private static long _gcBytes; private static int _gcFrames;
        private static void GcFrame()
        {
            try
            {
                if (!_gcTried) { _gcTried = true; _gcRec = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame"); }
                if (_gcRec.Valid && _gcRec.Count > 0) { _gcBytes += _gcRec.LastValue; _gcFrames++; }
            }
            catch (Exception) { }
        }
        internal static void StopGc()
        {
            if (!_gcTried) return;
            _gcTried = false;
            try { _gcRec.Dispose(); } catch (Exception) { }
        }
        // the native map camera (QuestMap / CleanHUD): enabled or not, found among all cameras (also inactive ones) once and kept
        private static UnityEngine.Camera _mapCam;
        private static string MapCamState()
        {
            if (_mapCam == null)
                foreach (var c in Resources.FindObjectsOfTypeAll<UnityEngine.Camera>())
                    if (c != null && c.gameObject.scene.IsValid() && c.name == "MapCheat_Camera") { _mapCam = c; break; }
            if (_mapCam == null) return "absent";
            return (_mapCam.enabled ? "enabled" : "disabled") + (_mapCam.gameObject.activeInHierarchy ? ", active" : ", inactive") + (_mapCam.isActiveAndEnabled ? " (renders)" : " (idle)");
        }

        private static void LogSystem()
        {
            try
            {
                var sb = new StringBuilder(1024);
                sb.Append("PC: ").Append(SystemInfo.graphicsDeviceName).Append(", ").Append(SystemInfo.graphicsMemorySize).Append(" MB VRAM, ").Append(SystemInfo.graphicsDeviceVersion)
                  .Append(" | ").Append(SystemInfo.processorType).Append(" (").Append(SystemInfo.processorCount).Append(" threads), ").Append(SystemInfo.systemMemorySize).Append(" MB RAM | ")
                  .Append(SystemInfo.operatingSystem);
                PlayerBodyModule.Info(sb.ToString());
                sb.Length = 0;
                var r = Screen.currentResolution;
                sb.Append("Display: ").Append(Screen.width).Append('x').Append(Screen.height).Append(' ').Append(Screen.fullScreenMode).Append(", ").Append(r.refreshRate).Append(" Hz")
                  .Append(" | quality '").Append(QualitySettings.names.Length > QualitySettings.GetQualityLevel() ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "?")
                  .Append("', vsync ").Append(QualitySettings.vSyncCount).Append(", target fps ").Append(Application.targetFrameRate)
                  .Append(", shadows ").Append(QualitySettings.shadows).Append(' ').Append(QualitySettings.shadowResolution).Append(" to ").Append(QualitySettings.shadowDistance.ToString("0")).Append(" m, ")
                  .Append(QualitySettings.shadowCascades).Append(" cascades, AA ").Append(QualitySettings.antiAliasing).Append(", LOD bias ").Append(QualitySettings.lodBias.ToString("0.##"))
                  .Append(", pixel lights ").Append(QualitySettings.pixelLightCount).Append(", textures 1/").Append(1 << QualitySettings.masterTextureLimit);
                PlayerBodyModule.Info(sb.ToString());
                sb.Length = 0;
                sb.Append("Mods:");
                foreach (var kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
                    if (kv.Value != null && kv.Value.Metadata != null) sb.Append(' ').Append(kv.Value.Metadata.Name).Append(' ').Append(kv.Value.Metadata.Version).Append(',');
                sb.Append(" | Apocaplayer: third person ").Append(ThirdPerson.On).Append(", cutaway ").Append(PlayerBodyModule.OcclusionPrototype.Value).Append(" (in cars ").Append(PlayerBodyModule.OcclusionInVehicle.Value)
                  .Append("), body in first person ").Append(PlayerBodyModule.BodyFirstPerson.Value).Append(", replace driver ").Append(PlayerBodyModule.ReplaceDriver.Value);
                PlayerBodyModule.Info(sb.ToString());
            }
            catch (Exception e) { PlayerBodyModule.Warn("PC info: " + e.Message); }
        }
    }
}
