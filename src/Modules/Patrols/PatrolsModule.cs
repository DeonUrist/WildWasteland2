using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Patrols
{
    // Patrols, traffic & War Rig (ex Apocapatrol 2.14.4 + War Rig 1.1.3 + the debug MerchantSpotter 1.0.2 plugin).
    // AI-driven raider cars: templates assembled from the game's own frames and parts, live crews with a driving AI, ranged
    // passengers, human turrets, ram damage, loot trucks, a convoy spawner driven by the game's Distance Travelled, persistent world
    // traffic (camps, routes, faction battles, merchants), the editors, and the War Rig tanker trailer. Save data stays verbatim
    // (ES3 key "Apocapatrol.Patrol", schema-14 embedded record, motorcycle / War Rig ES3 prefab ids).
    //
    // Runner: the old Apocapatrol.Runner MonoBehaviours (Patrol, TemplateMenu, TemplateExporter, TurretSpotter, BusPassengerPoseEditor,
    // Convoy, WorldTraffic, Cleanup + the MerchantEditor) sit on a module-owned child of Core.Runner.Host; their Update / LateUpdate /
    // OnGUI are scheduler jobs registered in that order (PORTING.md pattern 2). Per-object components (Crew, Pilot, Pose, ...) keep their
    // Unity loops and stand down while Running is false.
    public sealed partial class PatrolsModule : ModuleBase
    {
        public override string Id { get { return "Patrols"; } }
        public override string DisplayName { get { return "Patrols, traffic & War Rig"; } }
        static readonly string[] Conflicts = { "com.denis.apocalypter.apocapatrol", "com.denis.apocalypter.warrig", "com.denis.apocalypter.merchantspotter" };
        public override string[] ConflictsWith { get { return Conflicts; } }

        internal const string NAME = "Wild Wasteland Patrols";
        internal static string VERSION { get { return WildWasteland.Plugin.VERSION; } }

        internal static PatrolsModule Instance;
        // false = the module is off: every per-object loop (crews, pilots, poses, turrets, merchants, rigs) returns at once
        internal static bool Running;
        // BepInEx\plugins\WildWasteland\Patrols (templates, layouts, poses, textures, sounds, War Rig models)
        internal static string DataDir = ".";

        GameObject _host;
        Patrol _patrol;   // editor components: PatrolsModule.Tools.cs (Tools) and PatrolsModule.Developer.cs (Developer)
        Convoy _convoy; WorldTraffic _traffic; Cleanup _cleanup;
        SaveBus.StateChange _onSaveState;

        // ---------------------------------------------------------------- lifecycle

        public override void Init()
        {
            try { InitInner(); }
            catch { Shutdown(); throw; }   // a failed start leaves nothing running (ModuleHost then removes hooks / jobs / patches)
            Running = true;   // last: per-object loops only run once everything is in place
            Services.FollowerVehicles = new QuestFollowers();
            Services.Camps = new TrafficCamps.Status();
            Services.NpcHurt-=Crew.Attacked;Services.NpcHurt+=Crew.Attacked;
            Services.VehiclePartHit-=Crew.PartHit;Services.VehiclePartHit+=Crew.PartHit;
            SaveBus.CleanupSettings -= DrawSaveCleanupSettings; SaveBus.CleanupSettings += DrawSaveCleanupSettings;
            LoadingProgress.Register(this, VehiclePreparation.LoadWork);
        }

        static void DrawSaveCleanupSettings(ToolLayout l)
        {
            l.Heading("Existing abandoned-patrol cleanup");
            l.Text("Runs during gameplay. Player-entered cars are protected. Traffic unloads separately and retains its virtual state.");
            l.Toggle(CleanupEnabled, "Abandoned patrol vehicles"); l.Entry(CleanupDistance); l.Entry(CleanupMinutes); l.Entry(CleanupMaxCars);
        }

        /// <summary>Shares current combat eligibility with the typed patrol query provider.</summary>
        internal static bool CombatActive { get { return Running && VehicleCombatActive(); } }

        static bool VehicleCombatActive()
        {
            foreach (var marker in PatrolMarker.All)
                if (marker != null && marker.gameObject.activeInHierarchy && !marker.Exploded && !marker.SetupOnly
                    && marker.FactionTarget != null && marker.FactionTarget.activeInHierarchy && HasCombatCrew(marker)
                    && ((PlayerRef.Player == null || marker.FactionTarget != PlayerRef.Player.gameObject) || (Core.GameRefs.Player != null ? Vector3.Distance(marker.transform.position, Core.GameRefs.Player.transform.position) : float.MaxValue) <= 600f)) return true;
            return false;
        }

        static bool HasCombatCrew(PatrolMarker marker)
        {
            if (marker.Driver != null && !marker.DriverDied && !marker.DriverBailed || marker.Passenger != null) return true;
            foreach (var rider in marker.Riders) if (rider != null) return true;
            return false;
        }

        void InitInner()
        {
            Instance = this;
            DataDir = Dir;
            try { Directory.CreateDirectory(DataDir); } catch (Exception e) { Log.Warn("[Patrols] data folder: " + e.Message); }

            // Derived road cache only; world state is embedded in the ES3 save.
            var cfgDir = Assets.ModuleConfigDir(Id);
            TrafficRoads.CacheDir = Path.Combine(cfgDir, "roads");

            // hooks (Core dispatchers) and module patches, each in its own try like the old Awake
            WarRig.PatrolRetention.InstallRetention(this);
            Try("Traffic roads", TrafficRoads.Install);
            Scheduler.Sliced(this, "Traffic final terrain", 500, TrafficRoads.GroundStep);
            Try("Camp status", () => TrafficCamps.Install(this));
            Try("Suspension", () => Suspension.Install(this));
            Prefabs.Install(this);
            PatrolLoadPreparation.Install(this);
            Core.Hooks.SetFsmFloatEnter.Postfix(this, Ram.AfterSetFsmFloat);
            Core.Hooks.SetFsmFloatDo.Prefix(this, Ram.BeforeSetFsmFloat);   // a car never rams its own (trigger-only) crew
            Core.Hooks.SendEventEnter.Prefix(this, Ram.BeforeSendEvent);
            Try("Motorcycle save hook (saved motorcycles may not load)", () => MotorcycleIntegration.Install(this));

            EditorStore.Load(DataDir);
            Core.ModApi.Changed-=EditorStore.CargoApiChanged;Core.ModApi.Changed+=EditorStore.CargoApiChanged;EditorStore.CargoApiChanged(null);
            CarTemplates.Init(DataDir);
            CarTemplates.Load();
            Try("War Rig", () => WarRig.WarRigFeature.Install(this, Path.Combine(DataDir, "WarRig")));

            // per-scene state (fresh start, or a restart after Shutdown)
            Patrol.ResetForScene(); PlayerRef.Reset(); PatrolPersistence.ResetForScene(); PickupCatalog.Invalidate();
            _onSaveState = PatrolPersistence.OnStateChanged;
            SaveBus.StateChanged += _onSaveState;

            EnsureRunner();
            RegisterJobs();
            RegisterHotkeys();
            Log.Always("[Patrols] " + DisplayName + " ready (" + CarTemplates.Summary + ")");
        }

        public override void Shutdown()
        {
            Core.ModApi.Changed-=EditorStore.CargoApiChanged;
            Services.NpcHurt-=Crew.Attacked;
            Services.VehiclePartHit-=Crew.PartHit;
            CrewReturn.Reset();
            SaveBus.CleanupSettings -= DrawSaveCleanupSettings;
            Running = false;
            LoadingProgress.Remove(this);
            WarRig.PatrolRetention.Clear();
            VehicleRoutingApi.Reset();
            Try("build queue", BuildQueue.Reset);
            Try("vehicle build cache", VehiclePreparation.Reset);
            PatrolLoadPreparation.Reset();
            if (Services.FollowerVehicles is QuestFollowers) Services.FollowerVehicles = null;
            if (Services.Camps is TrafficCamps.Status) Services.Camps = null;
            TrafficCompass.Clear();
            Try("foot patrols", WorldTraffic.ReleaseFootAll);
            if (_onSaveState != null) { SaveBus.StateChanged -= _onSaveState; _onSaveState = null; }
            Try("persistence reset", PatrolPersistence.Uninstall);
            Try("road cache flush", () => { TrafficRoads.Flush(); TrafficRoads.Uninstall(); });
            Try("War Rig", WarRig.WarRigFeature.Uninstall);
            Try("coroutines", StopObjectCoroutines);
            PatrolPersistence.Runner = null;
            // destroying the runner object closes the editors (their OnDestroy releases the pause), the spot / merchant markers and the
            // debug-map textures; its coroutines (builds, restores, traffic materialisation) stop with it
            if (_host != null) UnityEngine.Object.Destroy(_host);
            _host = null; _patrol = null; _convoy = null; _traffic = null; _cleanup = null;
            ShutdownTools();
            ShutdownDeveloper();
            ModVehicles.Clear();
            Prefabs.Invalidate();
            Instance = null;
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            WarRig.PatrolRetention.Clear();
            TrafficCompass.Clear();
            PickupCatalog.Invalidate(); PatrolVisibility.Reset(); EnsureRunner(); Patrol.ResetForScene(); PlayerRef.Reset(); PatrolPersistence.ResetForScene(); Convoy.ResetForScene();
            WarRig.WarRigFeature.OnSceneLoaded();
        }

        // Core.Origin (NWH FloatingOrigin.Jump): legacy physics caches of the pilots and PlayerRef move with the world
        public override void OnOriginShift(Vector3 delta) { TrafficSpace.OriginShift(delta); }
        public override void OnWorldReady() { WarRig.PatrolRetention.Prepare(); Prefabs.Refresh(); WarRig.PoiEncounter.Prepare(); WorldTraffic.RestoreTownGuards(); }
        internal void SpawnPoi(CarTemplate template, Vector3 position, Quaternion rotation, Action<GameObject> onDone)
        { if (_patrol != null && Running) _patrol.SpawnAt(template, position, rotation, true, onDone); else onDone(null); }

        // ---------------------------------------------------------------- runner object + jobs

        void EnsureRunner()
        {
            // the game destroys the plugin's GameObject on scene load; the logic lives on a child of Core's hidden runner object
            if (_host != null) return;
            var parent = Core.Runner.Host;
            _host = new GameObject("WW.Patrols") { hideFlags = HideFlags.HideAndDontSave };
            if (parent != null) _host.transform.SetParent(parent.transform, false); else UnityEngine.Object.DontDestroyOnLoad(_host);
            _patrol = _host.AddComponent<Patrol>();
            AddToolsComponents();
            AddDeveloperSeatComponents();
            _convoy = _host.AddComponent<Convoy>();
            _traffic = _host.AddComponent<WorldTraffic>();
            _cleanup = _host.AddComponent<Cleanup>();
            AddDeveloperMerchantComponent();
            PatrolPersistence.Runner = _patrol;
        }

        // the old runner components' order: Patrol, TemplateMenu, TemplateExporter, TurretSpotter, BusPassengerPoseEditor, Convoy,
        // WorldTraffic, Cleanup (then the absorbed MerchantSpotter editor). Each job re-reads the component (recreated after a scene load).
        void RegisterJobs()
        {
            Scheduler.Interval(this, "Patrols.Prepare authored structures", .5f, VehiclePreparation.PublishWarm);
            Scheduler.Interval(this, "Patrols.Prefab cache", .5f, Prefabs.Refresh).RunNow();
            Scheduler.Sliced(this, "Patrols.Build", 1000, BuildQueue.Step);
            TrafficCompass.Clear();
            Scheduler.Interval(this, "Compass markers", .5f, TrafficCompass.Tick);
            Scheduler.EveryFrame(this, "Patrols.Road publication", RoadPlanner.PublishCompleted);
            Scheduler.EveryFrame(this, "Patrols.Runtime", TickPatrol);
            Scheduler.Interval(this, "Patrols.CombatActivity", .25f, PublishCombatActivity);
            RegisterToolsTicks();
            RegisterDeveloperSeatTicks();
            Scheduler.Interval(this, "Patrols.Progression", .25f, TickConvoy);
            Scheduler.EveryFrame(this, "Patrols.Traffic", TickTraffic);
            Scheduler.EveryFrame(this, "Patrols.Locations", TickLocations);
            Scheduler.Interval(this, "Patrols.Camps", .25f, TickCamps);
            Scheduler.Interval(this, "Patrols.Roads", .25f, TickRoads);
            Scheduler.Interval(this, "Patrols.Cleanup", .25f, TickCleanup);
            RegisterDeveloperMerchantTick();

            RegisterToolsLateTicks();

            // (WW perf) each with a predicate (built once here): IMGUI stops when none of them (and no other module) has anything to draw
            RegisterToolsGui();
            RegisterDeveloperSeatGui();
            Core.ModApi.Provide<Core.ITrafficMapSource>(this, this);
            RegisterDeveloperMerchantGui();
        }

        void RegisterHotkeys()
        {
            RegisterToolsHotkeys();
            RegisterDeveloperHotkeys();
        }

        // per-object coroutines (overheat, revive, bail-out timers, merchant setup) end with the module; the objects themselves stay
        static void StopObjectCoroutines()
        {
            var types = new[] { typeof(Crew), typeof(Pilot), typeof(PassengerGuard), typeof(RiderGuard), typeof(MerchantHost), typeof(MerchantGuardHealth), typeof(TrafficActor) };
            foreach (var t in types)
                foreach (var o in UnityEngine.Object.FindObjectsOfType(t))
                {
                    var mb = o as MonoBehaviour;
                    if (mb != null) mb.StopAllCoroutines();
                }
        }

        static void Try(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { Log.Error("[Patrols] " + what + ": " + e); }
        }

        // ---------------------------------------------------------------- small helpers the old Plugin class provided

        internal static void Verbose(string msg) { if (Log.Verbose) Log.Info("[Patrols] " + msg); }

        // ---------------------------------------------------------------- per-target parts (Wave A split; implemented in the target module folders)
        /// <summary>Drops the patrol editor / template recorder component references on Shutdown (the runner object owning them is destroyed). Implemented in Tools.</summary>
        partial void ShutdownTools();
        /// <summary>Drops the numpad editor component references and the bus pose target on Shutdown. Implemented in Developer.</summary>
        partial void ShutdownDeveloper();
        /// <summary>Adds the patrol editor and template recorder components to the runner object (after Patrol, old component order). Implemented in Tools.</summary>
        partial void AddToolsComponents();
        /// <summary>Adds the turret spot and bus passenger pose editors to the runner object (old component order). Implemented in Developer.</summary>
        partial void AddDeveloperSeatComponents();
        /// <summary>Adds the merchant item editor to the runner object (last, old component order). Implemented in Developer.</summary>
        partial void AddDeveloperMerchantComponent();
        /// <summary>Registers the editor/recorder Update jobs right after Patrol.Tick (old job order). Implemented in Tools.</summary>
        partial void RegisterToolsTicks();
        /// <summary>Registers the turret spot / bus pose editor Update jobs before Convoy.Tick (old job order). Implemented in Developer.</summary>
        partial void RegisterDeveloperSeatTicks();
        /// <summary>Registers the merchant editor Update job after Cleanup.Tick (old job order). Implemented in Developer.</summary>
        partial void RegisterDeveloperMerchantTick();
        /// <summary>Registers the editor/recorder LateUpdate jobs. Implemented in Tools.</summary>
        partial void RegisterToolsLateTicks();
        /// <summary>Registers the editor/recorder Gui jobs after Patrol.Gui (old order). Implemented in Tools.</summary>
        partial void RegisterToolsGui();
        /// <summary>Registers the turret spot / bus pose editor Gui jobs before WorldTraffic.Gui (old order). Implemented in Developer.</summary>
        partial void RegisterDeveloperSeatGui();
        /// <summary>Registers the merchant editor Gui job last (old order). Implemented in Developer.</summary>
        partial void RegisterDeveloperMerchantGui();
        /// <summary>Registers the patrol editor and template recorder hotkeys. Implemented in Tools.</summary>
        partial void RegisterToolsHotkeys();
        /// <summary>Registers the bus passenger pose and merchant item editor hotkeys. Implemented in Developer.</summary>
        partial void RegisterDeveloperHotkeys();
    }
}
