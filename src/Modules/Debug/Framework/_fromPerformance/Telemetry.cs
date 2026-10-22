using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using Unity.Profiling;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// Phase 0 measurement (plan §3): frame times (avg, p95, p99, spikes tagged with what happened that frame), engine counters
    /// (ProfilerRecorder, where the release player provides them), GC counts, FSM census and event counters fed by the
    /// PlayMaker features. Frame stats are one EarlyUpdate job (ring buffer, no allocation); the census runs only on request
    /// (tab button / periodic when [Performance / Telemetry] Periodic census is on).
    /// </summary>
    public static class Telemetry
    {
        // ---- counters incremented by features (read on the tab, reset with "Reset counters") ----
        public static long SendGo, SendGoChildren, SendGoFsm, Broadcasts, FastRemovals, Sleeps, Wakes, PickQueries, PickCasts;

        static ConfigEntry<bool> _periodic;
        static PerformanceModule _module;
        const int N = 1024;
        static readonly float[] _dt = new float[N];
        static readonly float[] _sorted = new float[N];
        static int _count, _head;
        static int _spikes; static float _worst; static string _worstTag = "";
        static string _tag;   // what happened this frame (tile/save/load/origin/culling)
        static int _gcStartCount; static float _gcStartTime;
        static readonly List<string> _spikeLog = new List<string>();
        static ProfilerRecorder _gcAlloc, _gcUsed, _drawCalls, _setPass, _shadowCasters, _tris;
        static bool _recorders;

        internal static void Bind(ModuleConfig cfg)
        {
            _periodic = cfg.Tool("Telemetry", "Periodic census", false, "Write an FSM/scene census to BepInEx\\config\\WildWasteland\\Performance\\ every 60 s (for measurements; off for normal play).");
        }

        internal static void Init(PerformanceModule m)
        {
            _module = m;
            Scheduler.EveryFrame(m, "Performance.Frames", Frame, Phase.EarlyUpdate);
            Scheduler.Interval(m, "Performance.PeriodicCensus", 60f, Periodic);
            _gcStartCount = GC.CollectionCount(0); _gcStartTime = Time.realtimeSinceStartup;
            StartRecorders();
        }

        internal static void Shutdown()
        {
            StopRecorders();
        }

        static void StartRecorders()
        {
            try
            {
                _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                _gcUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
                _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                _shadowCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
                _tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                _recorders = true;
            }
            catch (Exception e) { Log.Info("[Performance] profiler recorders unavailable: " + e.Message); }
        }

        static void StopRecorders()
        {
            if (!_recorders) return;
            _recorders = false;
            try { _gcAlloc.Dispose(); _gcUsed.Dispose(); _drawCalls.Dispose(); _setPass.Dispose(); _shadowCasters.Dispose(); _tris.Dispose(); } catch { }
        }

        internal static void OnSceneLoaded() { Mark("scene"); }

        /// <summary>Tag the current frame (shown with spikes): "tile", "save", "load", "origin", "cull", "spawn" ...</summary>
        public static void Mark(string tag) { _tag = _tag == null ? tag : (_tag.IndexOf(tag, StringComparison.Ordinal) >= 0 ? _tag : _tag + "+" + tag); }

        static void Frame()
        {
            float dt = Time.unscaledDeltaTime;
            if (Time.timeScale > 0 && Time.frameCount > 10)
            {
                _dt[_head] = dt; _head = (_head + 1) % N; if (_count < N) _count++;
                if (dt > 0.0334f)
                {
                    _spikes++;
                    if (dt > _worst) { _worst = dt; _worstTag = _tag ?? ""; }
                    if (_spikeLog.Count >= 20) _spikeLog.RemoveAt(0);
                    _spikeLog.Add((dt * 1000f).ToString("0", CultureInfo.InvariantCulture) + " ms" + (_tag != null ? " [" + _tag + "]" : ""));
                }
            }
            _tag = null;
        }

        static void Periodic()
        {
            if (_periodic == null || !_periodic.Value || !GameRefs.InGame) return;
            WriteCensus();
        }

        public static void Reset()
        {
            _count = 0; _head = 0; _spikes = 0; _worst = 0; _worstTag = ""; _spikeLog.Clear();
            SendGo = SendGoChildren = SendGoFsm = Broadcasts = FastRemovals = Sleeps = Wakes = PickQueries = PickCasts = 0;
            _gcStartCount = GC.CollectionCount(0); _gcStartTime = Time.realtimeSinceStartup;
            if (_module != null) foreach (var f in _module.Features) f.Mismatches = 0;
        }

        static float Percentile(float p)
        {
            if (_count == 0) return 0;
            Array.Copy(_dt, _sorted, _count);
            Array.Sort(_sorted, 0, _count);
            return _sorted[Mathf.Clamp((int)(p * (_count - 1)), 0, _count - 1)];
        }

        static string Ms(float s) { return (s * 1000f).ToString("0.0", CultureInfo.InvariantCulture); }
        static string I(long v) { return v.ToString(CultureInfo.InvariantCulture); }

        internal static void Draw(ToolLayout l)
        {
            float sum = 0; for (int i = 0; i < _count; i++) sum += _dt[i];
            float avg = _count > 0 ? sum / _count : 0;
            l.Value("Frames (last " + _count + ")", "avg " + Ms(avg) + " ms (" + (avg > 0 ? (1f / avg).ToString("0", CultureInfo.InvariantCulture) : "-") + " fps)  p95 " + Ms(Percentile(0.95f)) + "  p99 " + Ms(Percentile(0.99f)));
            l.Value("Spikes > 33 ms", I(_spikes) + (_worst > 0 ? "   worst " + Ms(_worst) + " ms" + (_worstTag.Length > 0 ? " [" + _worstTag + "]" : "") : ""));
            if (_spikeLog.Count > 0) l.Text("Recent spikes: " + string.Join(", ", _spikeLog.ToArray()));
            float minutes = Mathf.Max(0.01f, (Time.realtimeSinceStartup - _gcStartTime) / 60f);
            l.Value("GC collections", I(GC.CollectionCount(0) - _gcStartCount) + " (" + ((GC.CollectionCount(0) - _gcStartCount) / minutes).ToString("0.0", CultureInfo.InvariantCulture) + " / min)");
            l.Value("Mono heap used / reserved", (UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() >> 20) + " / " + (UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() >> 20) + " MB");
            if (_recorders)
            {
                if (_gcAlloc.Valid) l.Value("GC allocated last frame", (_gcAlloc.LastValue / 1024).ToString(CultureInfo.InvariantCulture) + " KB");
                if (_drawCalls.Valid) l.Value("Draw calls / SetPass", I(_drawCalls.LastValue) + " / " + (_setPass.Valid ? I(_setPass.LastValue) : "-"));
                if (_shadowCasters.Valid) l.Value("Shadow casters", I(_shadowCasters.LastValue));
                if (_tris.Valid) l.Value("Triangles", I(_tris.LastValue));
            }
            l.Value("Enabled FSMs (FsmList)", I(PlayMakerFSM.FsmList.Count));
            l.Value("Targeted events (GO / children / GO+FSM)", I(SendGo) + " / " + I(SendGoChildren) + " / " + I(SendGoFsm));
            l.Value("Broadcasts / fast list removals", I(Broadcasts) + " / " + I(FastRemovals));
            l.Value("FSM sleeps / wakes", I(Sleeps) + " / " + I(Wakes));
            l.Value("Camera picks (queries / raycasts)", I(PickQueries) + " / " + I(PickCasts));
            int b = l.Buttons("Reset counters", "Write census file");
            if (b == 0) Reset();
            else if (b == 1) { var p = WriteCensus(); if (p != null) Log.Always("[Performance] census written: " + p); }
        }

        /// <summary>FSM census (count + active actions per FSM name and state) and scene counters into a text file. Allocates; on request only.</summary>
        public static string WriteCensus()
        {
            try
            {
                var by = new Dictionary<string, int[]>();
                var list = PlayMakerFSM.FsmList;
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i]; if (f == null) continue;
                    var fsm = f.Fsm; if (fsm == null) continue;
                    string key = fsm.Name + " | " + (fsm.ActiveStateName ?? "-");
                    int[] c; if (!by.TryGetValue(key, out c)) { c = new int[3]; by[key] = c; }
                    c[0]++; var st = fsm.ActiveState; if (st != null) c[1] += st.ActiveActions.Count;
                    if (fsm.ManualUpdate) c[2]++;
                }
                var rows = new List<KeyValuePair<string, int[]>>(by);
                rows.Sort((a, b) => b.Value[0].CompareTo(a.Value[0]));
                var sb = new StringBuilder();
                sb.Append("Wild Wasteland Performance census ").Append(DateTime.Now.ToString("s", CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("FsmList ").Append(list.Count).Append(", preset ").Append(PerformanceModule.Preset).Append('\n');
                if (_module != null) foreach (var f in _module.Features) sb.Append(f.PlanId).Append(' ').Append(f.Key).Append(": ").Append(f.Running).Append(f.Mismatches > 0 ? " mismatches " + f.Mismatches : "").Append('\n');
                sb.Append("lights ").Append(UnityEngine.Object.FindObjectsOfType<Light>().Length).Append(", rigidbodies ").Append(UnityEngine.Object.FindObjectsOfType<Rigidbody>().Length).Append('\n');
                sb.Append("\ncount\tactive actions\tsleeping\tfsm | state\n");
                foreach (var r in rows) sb.Append(r.Value[0]).Append('\t').Append(r.Value[1]).Append('\t').Append(r.Value[2]).Append('\t').Append(r.Key).Append('\n');
                string dir = Path.Combine(Assets.ConfigDir, "Performance");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "census-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt");
                File.WriteAllText(path, sb.ToString());
                return path;
            }
            catch (Exception e) { Log.Error("[Performance] census", e); return null; }
        }
    }
}
