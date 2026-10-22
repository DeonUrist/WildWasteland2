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

namespace WildWasteland.Modules.Performance
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
        public override string PlanId { get { return "RC5"; } }
        public override string Key { get { return "MapMagic budget"; } }
        public override string Title { get { return "Terrain streaming budget"; } }
        public override string Category { get { return Categories.World; } }
        public override int Order { get { return 10; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Fewer, lower-priority terrain generation threads and fewer objects placed per frame when a tile appears (smaller hitches, objects pop in a little later)."; } }

        Knob _threads, _objects, _applyMs;
        public override void Bind(ModuleConfig cfg)
        {
            _threads = NewKnob(cfg, "Threads", "Terrain generation threads (0 = a quarter of the CPU threads, 1..3).", 1f, 0f, 0f, -2f);
            _objects = NewKnob(cfg, "Objects per frame", "Objects MapMagic instantiates per frame when applying a tile (vanilla 500).", 25f, 40f, 80f, 500f);
            _applyMs = NewKnob(cfg, "Apply budget (ms)", "Main-thread time MapMagic may use per frame to apply tiles (vanilla 3).", 2f, 2f, 3f, 3f);
        }

        internal static bool On;
        bool _saved; bool _autoOrig; int _maxOrig; float _timeOrig;
        MapMagicObject _mm; int _objOrig;

        protected override void OnEnable(FeatureMode mode)
        {
            if (!EnsurePatched(typeof(Patch))) throw new Exception("ThreadManager.TaskThreadAction patch failed");
            On = true;
            if (!_saved) { _autoOrig = ThreadManager.autoMaxThreads; _maxOrig = ThreadManager.maxThreads; _timeOrig = CoroutineManager.timePerFrame; _saved = true; }
            Apply();
        }

        void Apply()
        {
            int t = Mathf.RoundToInt(_threads.Value);
            if (t <= 0) t = Mathf.Clamp(SystemInfo.processorCount / 4, 1, 3);
            ThreadManager.autoMaxThreads = false;
            ThreadManager.maxThreads = t;
            CoroutineManager.timePerFrame = Mathf.Max(0.5f, _applyMs.Value);
            ApplyObjects();
        }

        void ApplyObjects()
        {
            if (_mm == null)
            {
                _mm = Object.FindObjectOfType<MapMagicObject>();     // once per scene / enable
                if (_mm != null) _objOrig = _mm.globals.objectsNumPerFrame;
            }
            if (_mm != null) _mm.globals.objectsNumPerFrame = Mathf.Max(1, Mathf.RoundToInt(_objects.Value));
        }

        public override void OnSettingsChanged() { Apply(); }
        public override void OnSceneLoaded() { _mm = null; }
        public override void OnWorldReady() { ApplyObjects(); }

        protected override void OnDisable()
        {
            On = false;
            if (_saved) { ThreadManager.autoMaxThreads = _autoOrig; ThreadManager.maxThreads = _maxOrig; CoroutineManager.timePerFrame = _timeOrig; _saved = false; }
            if (_mm != null) _mm.globals.objectsNumPerFrame = _objOrig;
            _mm = null;
        }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("Threads / objects per frame / apply ms", ThreadManager.maxThreads + (ThreadManager.autoMaxThreads ? " (auto)" : "") + " / " + (_mm != null ? _mm.globals.objectsNumPerFrame.ToString() : "-") + " / " + CoroutineManager.timePerFrame);
        }

        [HarmonyPatch]
        static class Patch
        {
            // runs on the worker thread that executes a generation task
            [HarmonyPatch(typeof(ThreadManager), "TaskThreadAction"), HarmonyPrefix]
            static void Pre()
            {
                if (!On) return;
                try { var th = Thread.CurrentThread; if (th.Priority != System.Threading.ThreadPriority.BelowNormal && !th.IsThreadPoolThread && th != _main) th.Priority = System.Threading.ThreadPriority.BelowNormal; } catch { }
            }
        }
        static Thread _main = Thread.CurrentThread;
    }

    /// <summary>
    /// RC7 (plan §8, research C §2 #10): items keep Continuous CCD forever (LockPhysics / CheckTag set it on every pickup or part
    /// removal, bumper_10.md:L33, L98). A 1 s sliced sweep over ArrayList_Items roots on the Item, Weapon and Food layers switches
    /// non-kinematic bodies slower than 2 m/s from Continuous to ContinuousSpeculative and back to Continuous above 8 m/s.
    /// Only bodies this feature switched are touched again; the game setting Continuous itself (pickup) is respected. Disable puts
    /// Continuous back on every switched body that is not kinematic (Unity rejects Continuous on kinematic bodies with a warning).
    /// </summary>
    public sealed class CcdSweepFeature : Feature
    {
        public override string PlanId { get { return "RC7"; } }
        public override string Key { get { return "CCD sweep"; } }
        public override string Title { get { return "Resting items use cheaper CCD"; } }
        public override string Category { get { return Categories.World; } }
        public override int Order { get { return 11; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Loose items lying still use speculative continuous collision; fast-moving ones keep full continuous collision."; } }

        internal static bool On;
        Job _job;
        readonly Dictionary<Rigidbody, bool> _switched = new Dictionary<Rigidbody, bool>(RefComparer<Rigidbody>.Instance);
        readonly List<Rigidbody> _tmp = new List<Rigidbody>();
        PlayMakerArrayListProxy _items; GameObject _arraysGo;
        int _cursor; float _passStart;
        internal static long ToSpec, ToCont;

        protected override void OnEnable(FeatureMode mode) { On = true; _job = Scheduler.Sliced(Module, "Performance.CcdSweep", 300, Step); }

        protected override void OnDisable()
        {
            On = false;
            if (_job != null) { _job.Remove(); _job = null; }
            foreach (var kv in _switched)
            {
                var rb = kv.Key;
                if (rb != null && !rb.isKinematic && rb.collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative) rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
            _switched.Clear();
        }

        public override void OnSceneLoaded() { _switched.Clear(); _items = null; _arraysGo = null; _cursor = 0; }

        bool Step()
        {
            if (!On || Time.timeScale <= 0f) return false;
            if (_cursor == 0)
            {
                if (Time.time - _passStart < 1f) return false;
                _passStart = Time.time;
                if (!Resolve()) return false;
            }
            var list = _items.arrayList;
            for (int k = 0; k < 32; k++)
            {
                if (_cursor >= list.Count) { _cursor = 0; Prune(); return false; }
                var go = list[_cursor++] as GameObject;
                if (go == null || !go.activeInHierarchy) continue;
                int layer = go.layer;
                if (layer != 9 && layer != 12 && layer != 21) continue;
                var rb = go.GetComponent<Rigidbody>();
                if (rb == null || rb.isKinematic) continue;
                var mode = rb.collisionDetectionMode;
                float v2 = rb.velocity.sqrMagnitude;
                if (mode == CollisionDetectionMode.Continuous && v2 < 4f) { rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; _switched[rb] = true; ToSpec++; }
                else if (mode == CollisionDetectionMode.ContinuousSpeculative && v2 > 64f && _switched.ContainsKey(rb)) { rb.collisionDetectionMode = CollisionDetectionMode.Continuous; _switched.Remove(rb); ToCont++; }
                else if (mode != CollisionDetectionMode.ContinuousSpeculative && _switched.ContainsKey(rb)) _switched.Remove(rb);   // the game changed it
            }
            return true;
        }

        bool Resolve()
        {
            var go = GameRefs.NewGOArrayList;
            if (go == null) return false;
            if (go != _arraysGo || _items == null)
            {
                _arraysGo = go; _items = null;
                var ps = go.GetComponents<PlayMakerArrayListProxy>();
                for (int i = 0; i < ps.Length; i++) if (ps[i] != null && ps[i].referenceName == "ArrayList_Items") { _items = ps[i]; break; }
            }
            return _items != null;
        }

        void Prune()
        {
            _tmp.Clear();
            foreach (var kv in _switched) if (kv.Key == null) _tmp.Add(kv.Key);
            for (int i = 0; i < _tmp.Count; i++) _switched.Remove(_tmp[i]);
            _tmp.Clear();
        }

        public override void DrawStats(ToolLayout l) { l.Value("Bodies on speculative CCD now / switches", _switched.Count + " / " + ToSpec + " -> " + ToCont); }
    }

    /// <summary>
    /// EN6 (plan §5, research C §2 #10): Time.maximumDeltaTime 0.333 -> 0.1 (Max 0.15), so one slow frame no longer runs up to 16
    /// physics steps; Physics.sleepThreshold (the default for new bodies) 0.005 -> 0.14 from load, the value NWH FloatingOrigin
    /// gives every existing body after its first jump anyway (FloatingOrigin.cs AfterJump). Originals restored on disable.
    /// </summary>
    public sealed class TimePhysicsFeature : Feature
    {
        public override string PlanId { get { return "EN6"; } }
        public override string Key { get { return "Time and sleep"; } }
        public override string Title { get { return "Physics step cap and sleep threshold"; } }
        public override string Category { get { return Categories.World; } }
        public override int Order { get { return 20; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Caps physics catch-up after a slow frame (the game slows down instead of stalling) and lets resting bodies sleep from the start."; } }

        Knob _maxDt, _sleep;
        public override void Bind(ModuleConfig cfg)
        {
            _maxDt = NewKnob(cfg, "Max delta time", "Time.maximumDeltaTime (vanilla 0.333).", 0.1f, 0.1f, 0.15f, 0.3333333f);
            _sleep = NewKnob(cfg, "Sleep threshold", "Physics.sleepThreshold for new bodies (vanilla 0.005; NWH sets 0.14 on all bodies after the first origin jump).", 0.14f, 0.14f, 0.14f, 0.005f);
        }

        bool _saved; float _dtOrig, _sleepOrig;
        protected override void OnEnable(FeatureMode mode)
        {
            if (!_saved) { _dtOrig = Time.maximumDeltaTime; _sleepOrig = Physics.sleepThreshold; _saved = true; }
            Apply();
        }
        void Apply() { Time.maximumDeltaTime = Mathf.Max(Time.fixedDeltaTime, _maxDt.Value); Physics.sleepThreshold = Mathf.Max(0f, _sleep.Value); }
        public override void OnSettingsChanged() { Apply(); }
        public override void OnSceneLoaded() { Apply(); }
        protected override void OnDisable() { if (_saved) { Time.maximumDeltaTime = _dtOrig; Physics.sleepThreshold = _sleepOrig; _saved = false; } }
        public override void DrawStats(ToolLayout l) { l.Value("maximumDeltaTime / sleepThreshold", Time.maximumDeltaTime.ToString("0.###") + " / " + Physics.sleepThreshold.ToString("0.###")); }
    }

    /// <summary>
    /// EN6 (origin part, research C §0): every NWH FloatingOrigin jump runs FindObjectsOfType&lt;Rigidbody&gt;().ToList() in its
    /// BeforeJump and again in AfterJump listener (FloatingOrigin.cs). Nothing between the two (moving scene roots, pausing/playing
    /// world-space particles) creates, destroys or (de)activates a body, so both find the same set: the listeners are replaced by
    /// two that share one FindObjectsOfType array and skip the List copies (same sleepThreshold writes, same Physics.SyncTransforms).
    /// The ParticleSystem scan inside Jump is unchanged. No Harmony patch (WW Core owns FloatingOrigin.Jump through Hooks.OriginJump);
    /// listeners are swapped on the live FloatingOrigin.Instance (checked once a second) and swapped back on disable.
    /// </summary>
    public sealed class OriginJumpFeature : Feature
    {
        public override string PlanId { get { return "EN6"; } }
        public override string Key { get { return "Origin jump"; } }
        public override string Title { get { return "Floating-origin jump body scan"; } }
        public override string Category { get { return Categories.World; } }
        public override int Order { get { return 21; } }
        public override string Help { get { return "Floating-origin jumps scan the scene's rigidbodies once instead of twice (exact)."; } }

        static Rigidbody[] _bodies;
        NwhOrigin _fo; UnityAction _origBefore, _origAfter;
        static readonly UnityAction _myBefore = MyBefore, _myAfter = MyAfter;
        Job _job;
        internal static long Jumps;

        protected override void OnEnable(FeatureMode mode) { Swap(); _job = Scheduler.Interval(Module, "Performance.OriginJump", 1f, Swap); }
        protected override void OnDisable()
        {
            if (_job != null) { _job.Remove(); _job = null; }
            Unswap();
        }

        void Swap()
        {
            var fo = NwhOrigin.Instance;
            if (fo == _fo && _fo != null) return;
            if (_fo != null) Unswap();
            if (fo == null) return;
            var before = AccessTools.Method(typeof(NwhOrigin), "BeforeJump");
            var after = AccessTools.Method(typeof(NwhOrigin), "AfterJump");
            if (before == null || after == null) { Log.Warn("[Performance] EN6 origin: BeforeJump/AfterJump not found - left vanilla"); return; }
            _origBefore = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), fo, before);
            _origAfter = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), fo, after);
            fo.onBeforeJump.RemoveListener(_origBefore); fo.onAfterJump.RemoveListener(_origAfter);
            fo.onBeforeJump.AddListener(_myBefore); fo.onAfterJump.AddListener(_myAfter);
            _fo = fo;
        }

        void Unswap()
        {
            if (_fo != null)
            {
                _fo.onBeforeJump.RemoveListener(_myBefore); _fo.onAfterJump.RemoveListener(_myAfter);
                _fo.onBeforeJump.AddListener(_origBefore); _fo.onAfterJump.AddListener(_origAfter);
            }
            _fo = null; _bodies = null;
        }

        // FloatingOrigin.BeforeJump: every body sleepThreshold = MaxValue
        static void MyBefore()
        {
            _bodies = Object.FindObjectsOfType<Rigidbody>();
            for (int i = 0; i < _bodies.Length; i++) _bodies[i].sleepThreshold = float.MaxValue;
            Jumps++;
        }

        // FloatingOrigin.AfterJump: every body sleepThreshold = 0.14, then SyncTransforms
        static void MyAfter()
        {
            var b = _bodies ?? Object.FindObjectsOfType<Rigidbody>();
            for (int i = 0; i < b.Length; i++) if (b[i] != null) b[i].sleepThreshold = 0.14f;
            _bodies = null;
            Physics.SyncTransforms();
        }

        public override void DrawStats(ToolLayout l) { l.Value("Jumps handled", Jumps + (_fo != null ? "" : " (no FloatingOrigin)")); }
    }

    /// <summary>
    /// EN7 (sun part, research C §2 #11): AzureTimeController.FixedUpdate recomputes the celestial coordinates and the sun/moon
    /// light rotation on every physics step (AzureTimeController.cs:L286-L290). Its inputs (timeline, date, location) change only in
    /// its Update, so 2-3 physics steps in one frame compute the same result: the prefix lets only the first step of a frame run. Exact.
    /// </summary>
    public sealed class AzureSunFeature : Feature
    {
        public override string PlanId { get { return "EN7"; } }
        public override string Key { get { return "Azure sun"; } }
        public override string Title { get { return "Sky sun position once per frame"; } }
        public override string Category { get { return Categories.World; } }
        public override int Order { get { return 30; } }
        public override string Help { get { return "The sky's sun position is computed once per rendered frame instead of on every physics step (exact)."; } }
        internal static bool On; static int _frame = -1; internal static long Skipped;
        protected override void OnEnable(FeatureMode mode) { if (!EnsurePatched(typeof(Patch))) throw new Exception("AzureTimeController.FixedUpdate patch failed"); On = true; }
        protected override void OnDisable() { On = false; }
        public override void DrawStats(ToolLayout l) { l.Value("Duplicate steps skipped", Skipped.ToString()); }
        [HarmonyPatch]
        static class Patch
        {
            [HarmonyPatch(typeof(AzureTimeController), "FixedUpdate"), HarmonyPrefix]
            static bool Pre()
            {
                if (!On) return true;
                int f = Time.frameCount;
                if (f == _frame) { Skipped++; return false; }
                _frame = f; return true;
            }
        }
    }

    /// <summary>
    /// EN7 (weather part): AzureWeatherController.FixedUpdate re-evaluates every weather profile curve and writes ~44 override
    /// properties by reflection (boxing) on every physics step (AzureWeatherController.cs:L115-L118, L211-L277). Outside a weather
    /// transition the result only depends on the slowly moving time of day, so it is evaluated at the preset interval (0.2 s
    /// Balanced); during a transition (m_isWeatherChanging, 7 s fades) it runs once per frame so the fade stays smooth.
    /// </summary>
    public sealed class AzureWeatherFeature : Feature
    {
        public override string PlanId { get { return "EN7"; } }
        public override string Key { get { return "Azure weather"; } }
        public override string Title { get { return "Sky weather evaluation rate"; } }
        public override string Category { get { return Categories.World; } }
        public override int Order { get { return 31; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Sky/fog weather curves are re-evaluated a few times per second instead of on every physics step (every frame during weather changes)."; } }

        Knob _interval;
        public override void Bind(ModuleConfig cfg) { _interval = NewKnob(cfg, "Interval (s)", "Weather evaluation interval outside transitions (vanilla: every physics step).", 0.25f, 0.2f, 0.1f, 0f); }

        internal static bool On; static float _every, _next; static int _frame = -1; internal static long Skipped;
        static readonly Func<AzureWeatherController, bool> _changing = Getter();
        static Func<AzureWeatherController, bool> Getter()
        {
            // compiled once (mcs cannot dereference HarmonyX FieldRef returns)
            try
            {
                var fi = AccessTools.Field(typeof(AzureWeatherController), "m_isWeatherChanging");
                if (fi == null) return null;
                return FastReflect.Getter<AzureWeatherController, bool>(typeof(AzureWeatherController), "m_isWeatherChanging");
            }
            catch (Exception e) { Log.Warn("[Performance] EN7 weather: transition flag not readable (" + e.Message + ") - weather left vanilla"); return null; }
        }
        protected override void OnEnable(FeatureMode mode)
        {
            if (_changing == null) throw new Exception("AzureWeatherController.m_isWeatherChanging not readable");
            if (!EnsurePatched(typeof(Patch))) throw new Exception("AzureWeatherController.FixedUpdate patch failed");
            _every = Mathf.Max(0f, _interval.Value); On = true;
        }
        public override void OnSettingsChanged() { _every = Mathf.Max(0f, _interval.Value); }
        protected override void OnDisable() { On = false; }
        public override void DrawStats(ToolLayout l) { l.Value("Evaluations skipped", Skipped.ToString()); }
        [HarmonyPatch]
        static class Patch
        {
            [HarmonyPatch(typeof(AzureWeatherController), "FixedUpdate"), HarmonyPrefix]
            static bool Pre(AzureWeatherController __instance)
            {
                if (!On) return true;
                int f = Time.frameCount;
                if (f == _frame) { Skipped++; return false; }                // never twice in one frame
                if (_changing != null && !_changing(__instance))
                {
                    float now = Time.unscaledTime;
                    if (now < _next) { Skipped++; return false; }
                    _next = now + _every;
                }
                _frame = f; return true;
            }
        }
    }
}
