using UnityEngine;

// Vehicle contracts (core-modapi.md 8): factory registry (template assembly + build cache), ignition, cockpit controls, ownership
// queries for Core Optimization, driving telemetry. Provider: Vehicles (+Compatibility for ignition/telemetry). Consumers: Patrols,
// POI, HUD, Core Optimization, Tools. Interfaces only. FROZEN after W0.
namespace WildWasteland.Core
{
    // ------------------------------------------------------------------ moved unchanged from Services.cs

    /// <summary>Engine ignition (EngineSim through Compatibility; null = vanilla behaviour).</summary>
    public interface IVehicleIgnition
    {
        /// <summary>Player ignition key input for <paramref name="car"/>; outputs the resulting state.</summary>
        bool TryPlayer(GameObject car, bool down, bool held, out bool keyOn, out bool running, out bool cranking);
        /// <summary>Engine running state.</summary>
        bool TryRunning(GameObject car, out bool running);
        /// <summary>Starts the engine.</summary>
        bool TryStart(GameObject car);
        /// <summary>Stops the engine.</summary>
        bool TryStop(GameObject car);
    }

    // ------------------------------------------------------------------ new (Wave B Vehicles)

    /// <summary>Options of a factory build.</summary>
    public struct VehicleBuildOptions
    {
        /// <summary>Faction (paint, crew appearance).</summary>
        public string Faction;
        /// <summary>Loot/cargo key (container texture, cargo roll).</summary>
        public string CargoKey;
        /// <summary>Attach the template's trailer.</summary>
        public bool WithTrailer;
        /// <summary>Register the vehicle as saved (ItemSpawner.RegisterSaved).</summary>
        public bool Saved;
        /// <summary>Use a prepared build-cache instance when available (never clone it).</summary>
        public bool UseCache;
    }

    /// <summary>Builder registered per vehicle kind (motorcycle, War Rig, template car, mod vehicles).</summary>
    public interface IVehicleBuilder
    {
        /// <summary>Builds the vehicle at a scene pose; null on failure.</summary>
        GameObject Build(string template, Vector3 scenePosition, Quaternion rotation, VehicleBuildOptions options);
        /// <summary>Prepares (warms) an instance for <paramref name="template"/> in sliced work; true when ready.</summary>
        bool Prepare(string template);
    }

    /// <summary>Vehicle factory registry + build cache (README 6.6, P4).</summary>
    public interface IVehicleFactory
    {
        /// <summary>Registers the builder of a kind (Compatibility registers mod vehicles).</summary>
        void Register(IModule owner, string kind, IVehicleBuilder builder);
        /// <summary>Removes a kind.</summary>
        void Unregister(string kind);
        /// <summary>True when a builder exists for the kind.</summary>
        bool CanBuild(string kind);
        /// <summary>Builds from a template name (kind resolved from the template); null on failure.</summary>
        GameObject Build(string template, Vector3 scenePosition, Quaternion rotation, VehicleBuildOptions options);
        /// <summary>Queues preparation of a template in the build cache.</summary>
        void Prepare(string template);
        /// <summary>True when a prepared instance of the template is ready.</summary>
        bool Prepared(string template);
        /// <summary>True for vehicles produced by the factory (module-built).</summary>
        bool IsFactoryVehicle(GameObject vehicle);
        /// <summary>Template a factory vehicle was built from (null otherwise).</summary>
        string TemplateOf(GameObject vehicle);
    }

    /// <summary>Cockpit state of a vehicle (cassette, volume, handbrake, lights, ignition).</summary>
    public struct CockpitState
    {
        /// <summary>Ignition key on.</summary>
        public bool KeyOn;
        /// <summary>Engine running.</summary>
        public bool Running;
        /// <summary>Starter cranking.</summary>
        public bool Cranking;
        /// <summary>Handbrake engaged.</summary>
        public bool Handbrake;
        /// <summary>Headlights on.</summary>
        public bool Headlights;
        /// <summary>Cassette playing.</summary>
        public bool CassettePlaying;
        /// <summary>Radio/cassette volume label.</summary>
        public string Volume;
    }

    /// <summary>Ignition/cockpit controls of the player's vehicle (ex PlayerBody Car/IgnitionControl).</summary>
    public interface IVehicleControls
    {
        /// <summary>Cockpit state of a vehicle.</summary>
        bool TryGetCockpit(GameObject vehicle, out CockpitState state);
        /// <summary>True while the player's cockpit controls are active (seated as driver).</summary>
        bool PlayerControlsActive { get; }
        /// <summary>Sets the headlights.</summary>
        bool SetHeadlights(GameObject vehicle, bool on);
        /// <summary>Sets the handbrake.</summary>
        bool SetHandbrake(GameObject vehicle, bool on);
    }

    /// <summary>Query a module or bridge registers to claim vehicles it drives.</summary>
    public interface IVehicleOwnerQuery
    {
        /// <summary>True when the registering module drives/simulates this vehicle itself.</summary>
        bool Owns(GameObject vehicle);
    }

    /// <summary>"Is this vehicle module/third-party driven?" for Core Optimization CarLod (replaces namespace sniffing).</summary>
    public interface IVehicleOwnership
    {
        /// <summary>Registers an owner query (Patrols, Vehicles, Compatibility bridges).</summary>
        void Register(IModule owner, IVehicleOwnerQuery query);
        /// <summary>True when any registered query owns the vehicle; <paramref name="owner"/> names it.</summary>
        bool IsOwned(GameObject vehicle, out IModule owner);
    }

    /// <summary>Driving readings for the HUD (Compatibility ChaseCamera or Vehicles).</summary>
    public interface IDrivingTelemetry
    {
        /// <summary>Readings are valid now (player driving, source present).</summary>
        bool Visible { get; }
        /// <summary>Speed, km/h.</summary>
        float SpeedKph { get; }
        /// <summary>Engine rpm.</summary>
        float Rpm { get; }
        /// <summary>Rpm as a fraction of the redline.</summary>
        float RpmFraction { get; }
        /// <summary>Gear label ("N", "R", "3").</summary>
        string GearText { get; }
        /// <summary>Vehicle heading, degrees.</summary>
        float Heading { get; }
    }
}
