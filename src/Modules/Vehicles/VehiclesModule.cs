using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Modules.Patrols;
using WildWasteland.Modules.PlayerBody;
using WarRig = WildWasteland.Modules.Patrols.WarRig;             // refs-ok: Vehicles-owned namespace until the Wave C rename
using Motorcycles = WildWasteland.Modules.Patrols.Motorcycles;   // refs-ok: Vehicles-owned namespace until the Wave C rename

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// Vehicles module entry (README 3.1, 6.6): template-driven vehicle assembly and its build cache, the vehicle factory registry,
    /// motorcycle chassis, War Rig tanker, trailers, suspension tune, wreck rules, NPC ram damage, player ignition / cockpit controls
    /// and exit speed. Owns every config entry, job, hook, patch and page of these features (they lived in the Patrols and PlayerBody
    /// entries before Wave B). Assets: plugins/WildWasteland/Vehicles/ (War Rig model), data: config/WildWasteland/Vehicles/ (D13).
    /// </summary>
    public sealed class VehiclesModule : ModuleBase, IVehicleOwnerQuery
    {
        /// <summary>The module object (set at construction so logging and ownership work before Init and while disabled).</summary>
        internal static VehiclesModule Instance;
        /// <summary>True while the module is initialised and running (gates per-object components and shared paths).</summary>
        internal static bool Running;
        /// <summary>Vehicle factory registry provided through ModApi (README 6.6).</summary>
        internal static readonly VehicleFactory FactoryApi = new VehicleFactory();
        /// <summary>Player cockpit controls provided through ModApi.</summary>
        internal static readonly CockpitControls Cockpit = new CockpitControls();
        private static readonly WarRigShelter Shelter = new WarRigShelter();
        private static Job _ignitionJob;
        private static int _shelterId = -1;
        private static IShelterRegistry _shelterRegistry;

        /// <summary>Creates the module object (one instance, built by Core's ModuleRegistry).</summary>
        public VehiclesModule() { Instance = this; }

        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Vehicles"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Vehicles"; } }

        /// <summary>The standalone War Rig plugin is replaced by this module (it followed the War Rig code from Patrols).</summary>
        public override string[] ConflictsWith { get { return Conflicts; } }
        private static readonly string[] Conflicts = { "com.denis.apocalypter.warrig" };

        /// <summary>Binds every Vehicles entry (VehiclesSettings) and the live reaction of the ignition row switch.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            VehiclesSettings.Bind(cfg);
            VehiclesSettings.IgnitionKey.SettingChanged += IgnitionKeyChanged;
        }

        /// <summary>Re-runs the ignition row job at once when [Vehicles / Controls] IgnitionKey changes.</summary>
        private static void IgnitionKeyChanged(object sender, EventArgs e) { if (Running && _ignitionJob != null) _ignitionJob.RunNow(); }

        /// <summary>Installs hooks, patches and jobs in the order the Patrols and PlayerBody entries used, then marks the module running.</summary>
        public override void Init()
        {
            ModApi.Provide<IVehicleHudData>(this, new VehicleHudData());
            // Patrols part (old PatrolsModule.InitInner order): suspension hook, ram damage, motorcycle resolver, War Rig
            Suspension.Hook(this);
            Hooks.SetFsmFloatEnter.Postfix(this, Ram.ScaleNpcRamDamage);
            MotorcycleIntegration.Hook(this);
            WarRig.WarRigFeature.Start(this, WarRigDir());
            ExitSpeed.Restore();
            // build pipeline (WorldWork arbitrated; one driver per frame, see BuildQueue.Drive)
            Scheduler.Sliced(this, "Vehicles.Build cache", 500, VehicleBuildCache.DriveFromVehicles);
            Scheduler.EveryFrame(this, "Vehicles.Build cache loading", VehicleBuildCache.LoadBurstFromVehicles);
            LoadingProgress.Register(this, VehicleBuildCache.PreparationWork);
            Scheduler.Interval(this, "Vehicles.Exit speed", .5f, ExitSpeed.Update);
            Scheduler.Interval(this, "Vehicles.Factory warm", .5f, FactoryApi.Warm);
            // PlayerBody part (old InitVehicles order): ignition row, MousePickEvent patch, steering patch, ignition job, cockpit tick
            IgnitionControl.Load(Path.Combine(Assets.ModuleConfigDir(Id), "ignition-key.txt"), KeyCode.I);
            Car.Patch(this);
            WheelArms.PatchSteering(this);
            _ignitionJob = Scheduler.Interval(this, "Vehicles.Ignition control", .5f, IgnitionControl.Tick);
            Scheduler.EveryFrame(this, "Vehicles.Cockpit", Car.TickCockpit, Phase.Update);
            Hotkeys.Register(this, VehiclesSettings.CassetteKey, NoOp, Never);
            Hotkeys.Register(this, VehiclesSettings.VolumeDownKey, NoOp, Never);
            Hotkeys.Register(this, VehiclesSettings.VolumeUpKey, NoOp, Never);
            Running = true;
            Log.Main(this, "Vehicles ready (War Rig " + WarRig.WarRigFeature.Version + ", model folder " + WarRig.WarRigFeature.Dir + ")");
        }

        /// <summary>Hotkey stub: the cassette keys are polled inside the cockpit tick; the registration lists them for conflict checks.</summary>
        private static void NoOp() { }
        /// <summary>Hotkey gate that never fires (see NoOp).</summary>
        private static bool Never() { return false; }

        /// <summary>The War Rig model folder: plugins/WildWasteland/Vehicles/WarRig, or the pre-D13 Patrols/WarRig folder while the model files still live there.</summary>
        private string WarRigDir()
        {
            string dir = Path.Combine(Dir, "WarRig");
            string old = Path.Combine(Assets.ModuleDir("Patrols"), "WarRig");
            if (!File.Exists(Path.Combine(dir, WarRig.S.ModelFile)) && File.Exists(Path.Combine(old, WarRig.S.ModelFile)))
            {
                Log.Warn(this, "War Rig model found only in " + old + "; move WarRig.glb and WarRig_colliders.glb to " + dir + " (D13)");
                return old;
            }
            return dir;
        }

        /// <summary>Undoes every game-state change: ignition row, cockpit state, steering centring, exit speed, War Rig registrations and the shelter.</summary>
        public override void Shutdown()
        {
            Running = false;
            _ignitionJob = null;
            Try("ignition row", IgnitionControl.Remove);
            Try("cockpit", Car.Release);
            Try("vehicle input leases", VehicleDriveApi.ReleaseAll);
            Try("vehicle structure cache", VehicleBuildCache.Reset);
            Try("exit speed", ExitSpeed.Restore);
            Try("War Rig", WarRig.WarRigFeature.Stop);
            Try("shelter", RemoveShelter);
            ModApi.Changed -= ApiChanged;
            LoadingProgress.Remove(this);
        }

        /// <summary>Runs one shutdown step, logging (not throwing) its failure so the other steps still run.</summary>
        private void Try(string what, Action step)
        {
            try { step(); } catch (Exception e) { Log.Warn(this, what + ": " + e.Message); }
        }

        /// <summary>Scene change: per-scene caches of the cockpit, exit speed, War Rig and motorcycle chassis lookups are dropped.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            VehicleDriveApi.ReleaseAll();
            VehicleBuildCache.Reset();
            Car.Reset();
            ExitSpeed.Restore();
            WarRig.WarRigFeature.ResetForScene();
            Motorcycles.Factory.ResetForScene();
            FactoryApi.ResetForScene();
        }

        /// <summary>Provides the factory and cockpit APIs, claims module-built vehicles and registers the War Rig shelter with Survival.</summary>
        public override void ProvideApis()
        {
            ModApi.Provide<IWarRigWorld>(this,new WarRigWorldApi());
            ModApi.Provide<IVehicleAuthoring>(this,new VehicleAuthoringApi());
            ModApi.Provide<IVehicleAttachments>(this,new VehicleAttachmentsApi());
            ModApi.Provide<IVehicleDrive>(this,new VehicleDriveApi());
            ModApi.Provide<IVehicleAssembly>(this, new VehicleAssemblyApi());
            var ownership = ModApi.Get<IVehicleOwnership>();
            if (ownership != null) ownership.Register(this, this);
            ModApi.Provide<IVehicleFactory>(this, FactoryApi);
            ModApi.Provide<IVehicleControls>(this, Cockpit);
            FactoryApi.RegisterBuiltIns(this);
            ModApi.Changed -= ApiChanged; ModApi.Changed += ApiChanged;
            RegisterShelter();
        }

        /// <summary>Re-registers the War Rig shelter when Survival (re)provides or withdraws IShelterRegistry.</summary>
        private void ApiChanged(Type type)
        {
            if (type == typeof(IShelterRegistry) && Running) RegisterShelter();
        }

        /// <summary>Registers the War Rig shelter provider with the current IShelterRegistry (null-safe: no Survival = no registry).</summary>
        private void RegisterShelter()
        {
            var registry = ModApi.Get<IShelterRegistry>();
            if (ReferenceEquals(registry, _shelterRegistry) && _shelterId >= 0) return;
            RemoveShelter();
            if (registry == null) return;
            _shelterRegistry = registry; _shelterId = registry.Register(this, Shelter);
        }

        /// <summary>Removes the War Rig shelter registration (module shutdown or registry change).</summary>
        private static void RemoveShelter()
        {
            if (_shelterRegistry != null && _shelterId >= 0)
            {
                try { _shelterRegistry.Remove(_shelterId); } catch (Exception) { }
            }
            _shelterRegistry = null; _shelterId = -1;
        }

        /// <summary>Adds the Vehicles rows to the shared settings pages.</summary>
        public override void Settings(SettingsShell shell) { VehiclesSettings.Rows(this, shell); }

        /// <summary>The F3 tool pages: War Rig status and tools.</summary>
        public override IEnumerable<ToolPage> ToolPages()
        {
            return new ToolPage[] { new ActionPage(this, ToolGroups.Vehicles, "War Rig", VehiclesPages.DrawWarRig) { IsTool = true } };
        }

        /// <summary>Claims vehicles built or simulated by this module (War Rig, motorcycle chassis, suspension-tuned cars, factory builds).</summary>
        public bool Owns(GameObject vehicle)
        {
            if (vehicle == null) return false;
            return vehicle.GetComponent<WarRig.WarRigUnit>() != null
                || vehicle.GetComponent<Motorcycles.MotorcycleBalance>() != null
                || vehicle.GetComponent<MotorcycleRearAccess>() != null
                || vehicle.GetComponent<SuspensionTune>() != null
                || FactoryApi.IsFactoryVehicle(vehicle);
        }
    }
}
