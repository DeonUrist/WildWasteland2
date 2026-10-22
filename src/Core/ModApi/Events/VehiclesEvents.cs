using UnityEngine;

// Vehicle events (publisher: Vehicles/Patrols/Compatibility as noted).
// Event payloads (core-modapi.md 2, 9): structs with ids, enums, numbers, strings and WORLD-space vectors only (Origin.World),
// never GameObject/Component references (use EntityId / Entities). [Broadcast] marks events a future multiplayer transport may
// send; the rest are local. Publish with ModEvents.Publish(ref evt); check ModEvents.HasListeners<T>() before building costly
// payloads. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core.Events
{
    /// <summary>Who changed the ignition.</summary>
    public enum IgnitionSource { Player, Ai, EngineSim, Script }

    /// <summary>Why a vehicle left the world.</summary>
    public enum VehicleRemovalReason { Cleanup, TrafficUnload, Scene, Destroyed, Captured }

    /// <summary>A module vehicle entered the world (Vehicles/Patrols -> HUD, Presentation, POI).</summary>
    [Broadcast(Authority.Host)]
    public struct VehicleSpawned : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>Factory kind.</summary>
        public string Kind;
        /// <summary>Template name.</summary>
        public string Template;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Rotation.</summary>
        public Quaternion Rotation;
        /// <summary>Trailer entity (None when none).</summary>
        public EntityId Trailer;
        /// <summary>Cargo/loot key.</summary>
        public string CargoKey;
        /// <summary>Paint key.</summary>
        public string PaintKey;
        /// <summary>Patrols journey id (-1 none).</summary>
        public int JourneyId;
    }

    /// <summary>A module vehicle left the world.</summary>
    [Broadcast(Authority.Host)]
    public struct VehicleDespawned : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>Why.</summary>
        public VehicleRemovalReason Reason;
    }

    /// <summary>A vehicle exploded (Patrols/Vehicles -> NPCAI, HUD, Quests). Id-based successor of Services.VehicleExploded.</summary>
    [Broadcast(Authority.Host)]
    public struct VehicleExploded : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Wreck kind ("truck", "chassis"...).</summary>
        public string WreckKind;
    }

    /// <summary>Ignition state changed (Vehicles/Compatibility -> HUD, Patrols).</summary>
    [Broadcast(Authority.Owner)]
    public struct VehicleIgnition : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>Key on.</summary>
        public bool On;
        /// <summary>Engine running.</summary>
        public bool Running;
        /// <summary>Starter cranking.</summary>
        public bool Cranking;
        /// <summary>Who.</summary>
        public IgnitionSource Source;
    }

    /// <summary>Cockpit controls changed (Vehicles -> HUD).</summary>
    [Broadcast(Authority.Owner)]
    public struct VehicleControlsState : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>Cassette playing.</summary>
        public bool CassettePlaying;
        /// <summary>Volume label.</summary>
        public string Volume;
        /// <summary>Handbrake on.</summary>
        public bool Handbrake;
        /// <summary>Headlights on.</summary>
        public bool Headlights;
    }

    /// <summary>A trailer was attached (Vehicles -> Patrols).</summary>
    [Broadcast(Authority.Host)]
    public struct TrailerAttached : IModEvent
    {
        /// <summary>Truck entity.</summary>
        public EntityId Truck;
        /// <summary>Trailer entity.</summary>
        public EntityId Trailer;
    }

    /// <summary>A trailer was detached.</summary>
    [Broadcast(Authority.Host)]
    public struct TrailerDetached : IModEvent
    {
        /// <summary>Truck entity.</summary>
        public EntityId Truck;
        /// <summary>Trailer entity.</summary>
        public EntityId Trailer;
    }
}
