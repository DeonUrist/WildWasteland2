using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Den.Tools.Tasks;
using HarmonyLib;
using MapMagic.Core;
using NwhOrigin = NWH.Common.FloatingOrigin.FloatingOrigin;
using UnityEngine;
using UnityEngine.AzureSky;
using UnityEngine.Events;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;
using Object = UnityEngine.Object;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// RC5 (plan §8, research C §2 #6): MapMagic tile streaming budget. Vanilla: ThreadManager.autoMaxThreads -> processorCount-1
    /// generation threads at normal priority (Den.Tools ThreadManager.cs:L133-L141), MapMagic globals.objectsNumPerFrame 500
    /// (MapMagic.txt globals, ObjectsOutput.cs:L231: objects instantiated per frame when a tile is applied, each with its FSMs),
    /// CoroutineManager.timePerFrame 3 ms (main-thread apply budget). Preset: threads Low 1 / Balanced and Max clamp(cores/4,1,3),
    /// BelowNormal thread priority (prefix on ThreadManager.TaskThreadAction, runs on the worker), objects per frame 25/40/80,
    /// apply budget 2/2/3 ms. Tile size / mainRange are not touched (mainRange 0 would make 5 km neighbours objectless drafts).
    /// WW POI only edits the MapMagic graph (src/Modules/POI/PoiModule.cs), not these settings. All originals restored on disable.
    /// </summary>
    public sealed class MapMagicBudgetFeature : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "RC5"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "MapMagic budget"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Terrain streaming budget"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.World; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 10; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Fewer, lower-priority terrain generation threads and fewer objects placed per frame when a tile appears (smaller hitches, objects pop in a little later)."; } }

        Knob _threads, _objects, _applyMs;
        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
        public override void Bind(ModuleConfig cfg)
        {
            _threads = NewKnob(cfg, "Threads", "Terrain generation threads (0 = a quarter of the CPU threads, 1..3).", 1f, 0f, 0f, -2f);
            _objects = NewKnob(cfg, "Objects per frame", "Objects MapMagic instantiates per frame when applying a tile (vanilla 500).", 25f, 40f, 80f, 500f);
            _applyMs = NewKnob(cfg, "Apply budget (ms)", "Main-thread time MapMagic may use per frame to apply tiles (vanilla 3).", 2f, 2f, 3f, 3f);
        }

        internal static bool On;
        bool _saved; bool _autoOrig; int _maxOrig; float _timeOrig;
        MapMagicObject _mm; int _objOrig;

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            if (!EnsurePatched(typeof(Patch))) throw new Exception("ThreadManager.TaskThreadAction patch failed");
            On = true;
            if (!_saved) { _autoOrig = ThreadManager.autoMaxThreads; _maxOrig = ThreadManager.maxThreads; _timeOrig = CoroutineManager.timePerFrame; _saved = true; }
            Apply();
        }

        /// <summary>Applies this feature's current preset values to its captured native world/engine settings.</summary>
        void Apply()
        {
            int t = Mathf.RoundToInt(_threads.Value);
            if (t <= 0) t = Mathf.Clamp(SystemInfo.processorCount / 4, 1, 3);
            ThreadManager.autoMaxThreads = false;
            ThreadManager.maxThreads = t;
            CoroutineManager.timePerFrame = Mathf.Max(0.5f, _applyMs.Value);
            ApplyObjects();
        }

        /// <summary>Applies the configured MapMagic object-spawn budget to the current world's streaming settings.</summary>
        void ApplyObjects()
        {
            if (_mm == null)
            {
                _mm = Object.FindObjectOfType<MapMagicObject>();     // once per scene / enable
                if (_mm != null) _objOrig = _mm.globals.objectsNumPerFrame;
            }
            if (_mm != null) _mm.globals.objectsNumPerFrame = Mathf.Max(1, Mathf.RoundToInt(_objects.Value));
        }

        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged() { Apply(); }
        /// <summary>Drops scene-bound references and refreshes this feature for the newly loaded scene.</summary>
        public override void OnSceneLoaded() { _mm = null; }
        /// <summary>Refreshes this feature after native gameplay objects have finished loading.</summary>
        public override void OnWorldReady() { ApplyObjects(); }

        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable()
        {
            On = false;
            if (_saved) { ThreadManager.autoMaxThreads = _autoOrig; ThreadManager.maxThreads = _maxOrig; CoroutineManager.timePerFrame = _timeOrig; _saved = false; }
            if (_mm != null) _mm.globals.objectsNumPerFrame = _objOrig;
            _mm = null;
        }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l)
        {
            l.Value("Threads / objects per frame / apply ms", ThreadManager.maxThreads + (ThreadManager.autoMaxThreads ? " (auto)" : "") + " / " + (_mm != null ? _mm.globals.objectsNumPerFrame.ToString() : "-") + " / " + CoroutineManager.timePerFrame);
        }

        [HarmonyPatch]
        static class Patch
        {
            // runs on the worker thread that executes a generation task
            /// <summary>Native engine callback prefix: immediately yields while the enclosing feature is disabled and otherwise applies its documented per-thread/frame cadence.</summary>
            [HarmonyPatch(typeof(ThreadManager), "TaskThreadAction"), HarmonyPrefix]
            static void Pre()
            {
                if (!On) return;
                try { var th = Thread.CurrentThread; if (th.Priority != System.Threading.ThreadPriority.BelowNormal && !th.IsThreadPoolThread && th != _main) th.Priority = System.Threading.ThreadPriority.BelowNormal; } catch { }
            }
        }
        static Thread _main = Thread.CurrentThread;
    }
}
