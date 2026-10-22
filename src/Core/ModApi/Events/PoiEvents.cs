using UnityEngine;

// POI and town events (publisher: POI).
// Event payloads (core-modapi.md 2, 9): structs with ids, enums, numbers, strings and WORLD-space vectors only (Origin.World),
// never GameObject/Component references (use EntityId / Entities). [Broadcast] marks events a future multiplayer transport may
// send; the rest are local. Publish with ModEvents.Publish(ref evt); check ModEvents.HasListeners<T>() before building costly
// payloads. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core.Events
{
    /// <summary>A camp changed state.</summary>
    [Broadcast(Authority.Host)]
    public struct CampStateChanged : IModEvent
    {
        /// <summary>Camp key.</summary>
        public string CampId;
        /// <summary>Previous state.</summary>
        public CampState Old;
        /// <summary>New state.</summary>
        public CampState New;
        /// <summary>Faction.</summary>
        public string Faction;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A camp was cleared by the player.</summary>
    [Broadcast(Authority.Host)]
    public struct CampCleared : IModEvent
    {
        /// <summary>Camp key.</summary>
        public string CampId;
        /// <summary>Faction.</summary>
        public string Faction;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A camp changed owner (takeover).</summary>
    [Broadcast(Authority.Host)]
    public struct CampOwnershipChanged : IModEvent
    {
        /// <summary>Camp key.</summary>
        public string CampId;
        /// <summary>Previous faction.</summary>
        public string OldFaction;
        /// <summary>New faction.</summary>
        public string NewFaction;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A town was planned.</summary>
    [Broadcast(Authority.Host)]
    public struct TownPlanned : IModEvent
    {
        /// <summary>Town key.</summary>
        public string TownKey;
        /// <summary>Template id.</summary>
        public string TemplateId;
        /// <summary>Faction.</summary>
        public string Faction;
        /// <summary>Centre, world space.</summary>
        public Vector3 WorldCenter;
        /// <summary>Yaw, degrees.</summary>
        public float Yaw;
        /// <summary>Radius, metres.</summary>
        public float Radius;
        /// <summary>Walls.</summary>
        public bool HasWalls;
        /// <summary>Outline polygon, world space (may be null).</summary>
        public Vector3[] Outline;
        /// <summary>Gate centres, world space (may be null).</summary>
        public Vector3[] Gates;
    }

    /// <summary>A town was built in the scene.</summary>
    [Broadcast(Authority.Host)]
    public struct TownBuilt : IModEvent
    {
        /// <summary>Town key.</summary>
        public string TownKey;
        /// <summary>Centre, world space.</summary>
        public Vector3 WorldCenter;
        /// <summary>Lot count.</summary>
        public int Lots;
    }

    /// <summary>A town left the scene.</summary>
    [Broadcast(Authority.Host)]
    public struct TownStreamedOut : IModEvent
    {
        /// <summary>Town key.</summary>
        public string TownKey;
    }

    /// <summary>The player discovered a town.</summary>
    [Broadcast(Authority.Owner)]
    public struct TownDiscovered : IModEvent
    {
        /// <summary>Town key.</summary>
        public string TownKey;
        /// <summary>Display name.</summary>
        public string Name;
    }

    /// <summary>A town condition changed.</summary>
    [Broadcast(Authority.Host)]
    public struct TownConditionChanged : IModEvent
    {
        /// <summary>Town key.</summary>
        public string TownKey;
        /// <summary>Previous condition.</summary>
        public string Old;
        /// <summary>New condition.</summary>
        public string New;
    }

    /// <summary>A lot changed role.</summary>
    [Broadcast(Authority.Host)]
    public struct TownLotRoleChanged : IModEvent
    {
        /// <summary>Town key.</summary>
        public string TownKey;
        /// <summary>Lot index.</summary>
        public int Lot;
        /// <summary>New role.</summary>
        public string Role;
    }

    /// <summary>A custom POI site was generated.</summary>
    [Broadcast(Authority.Host)]
    public struct PoiSiteGenerated : IModEvent
    {
        /// <summary>Site key.</summary>
        public string SiteKey;
        /// <summary>POI type id.</summary>
        public string Type;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Yaw, degrees.</summary>
        public float Yaw;
    }

    /// <summary>A POI encounter spawned (e.g. War Rig).</summary>
    [Broadcast(Authority.Host)]
    public struct PoiEncounterSpawned : IModEvent
    {
        /// <summary>Site key.</summary>
        public string SiteKey;
        /// <summary>Encounter type.</summary>
        public string Type;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A gas station was placed.</summary>
    [Broadcast(Authority.Host)]
    public struct GasStationPlaced : IModEvent
    {
        /// <summary>Gas station key.</summary>
        public string SiteKey;
        /// <summary>desert/town/test.</summary>
        public string Kind;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Yaw, degrees.</summary>
        public float Yaw;
    }

    /// <summary>POI dressing finished (NPCAI may discover the structure).</summary>
    [Broadcast(Authority.Host)]
    public struct PoiDressingReady : IModEvent
    {
        /// <summary>Site/root key.</summary>
        public string SiteKey;
        /// <summary>POI type.</summary>
        public string Type;
        /// <summary>Root world position.</summary>
        public Vector3 WorldPos;
        /// <summary>Exit positions, world space (may be null).</summary>
        public Vector3[] Exits;
    }
}
