using System;
using System.Collections.Generic;
using UnityEngine;

// World contracts (core-modapi.md 8): custom POIs, towns geometry/templates, world locations registry, world-generation facts
// (Vistas). Provider: POI (IWorldGenFacts: Compatibility). Consumers: Patrols, NPCAI, Quests, HUD, Tools, Loot. Interfaces only.
// Ids are stable strings (town key "v1/&lt;seed&gt;/&lt;x&gt;/&lt;z&gt;", site keys, marker keys - inventory/world.md 7). FROZEN after W0.
namespace WildWasteland.Core
{
    // ------------------------------------------------------------------ moved unchanged from Services.cs

    /// <summary>A custom POI type registered with the POI module.</summary>
    public sealed class PoiDefinition
    {
        /// <summary>Unique POI type id.</summary>
        public string Id;
        /// <summary>Footprint radius, metres.</summary>
        public float Radius;
        /// <summary>Whether the POI may generate now.</summary>
        public Func<bool> Enabled;
        /// <summary>Spawns the POI at a scene pose and reports the root.</summary>
        public Action<Vector3, Quaternion, Action<GameObject>> Spawn;
        /// <summary>Marks an existing root (after load).</summary>
        public Action<GameObject> Mark;
    }

    /// <summary>Custom POI registry.</summary>
    public interface IPoiApi
    {
        /// <summary>Registers or replaces a POI type.</summary>
        void Register(PoiDefinition definition);
        /// <summary>Removes a POI type.</summary>
        void Remove(string id);
    }

    // ------------------------------------------------------------------ new (Wave B POI)

    /// <summary>Summary of one planned/built town.</summary>
    public struct TownInfo
    {
        /// <summary>Stable town key "v1/&lt;seed&gt;/&lt;x&gt;/&lt;z&gt;".</summary>
        public string Key;
        /// <summary>Town template id (Animals/Mutants/Scrapyard/Coyotes or custom).</summary>
        public string TemplateId;
        /// <summary>Owning faction.</summary>
        public string Faction;
        /// <summary>Condition ("intact", "raided"...).</summary>
        public string Condition;
        /// <summary>Centre, world (MapMagic-relative) space.</summary>
        public Vector3 WorldCenter;
        /// <summary>Yaw, degrees.</summary>
        public float Yaw;
        /// <summary>Radius, metres.</summary>
        public float Radius;
        /// <summary>Has walls.</summary>
        public bool HasWalls;
        /// <summary>Buildings exist in the scene now.</summary>
        public bool Built;
        /// <summary>Number of lots.</summary>
        public int LotCount;
    }

    /// <summary>A town gate (world space).</summary>
    public struct TownGate
    {
        /// <summary>Gate centre.</summary>
        public Vector3 WorldPos;
        /// <summary>Outward direction.</summary>
        public Vector3 Outward;
        /// <summary>Gate width, metres.</summary>
        public float Width;
    }

    /// <summary>A parking slot of a town (inside or outside the walls).</summary>
    public struct ParkingSlotInfo
    {
        /// <summary>Slot centre, world space.</summary>
        public Vector3 WorldPos;
        /// <summary>Yaw, degrees.</summary>
        public float Yaw;
        /// <summary>Outside the walls (external parking lot).</summary>
        public bool Outside;
        /// <summary>Lot index (-1 for street parking).</summary>
        public int Lot;
        /// <summary>Slot index within the town.</summary>
        public int Index;
    }

    /// <summary>Town geometry, roles and templates (README 6.7, P6). Templates are exchanged as JSON so the contract stays module-free.</summary>
    public interface ITownsApi
    {
        /// <summary>Increments whenever a town is planned, built, streamed out or changed.</summary>
        int Revision { get; }
        /// <summary>Fills <paramref name="into"/> (cleared first) with known towns.</summary>
        void Towns(List<TownInfo> into);
        /// <summary>Town containing a world point.</summary>
        bool TryTownAt(Vector3 world, out TownInfo town);
        /// <summary>Town by key.</summary>
        bool TryGet(string key, out TownInfo town);
        /// <summary>Outline polygon (world space, cleared first).</summary>
        bool TryOutline(string key, List<Vector3> worldPolygon);
        /// <summary>Gates of a town (cleared first).</summary>
        void Gates(string key, List<TownGate> into);
        /// <summary>Street graph: node positions (world) and edges as index pairs (both cleared first).</summary>
        void Streets(string key, List<Vector3> nodes, List<int> edgePairs);
        /// <summary>Free parking slot nearest a world point (outsideOnly = external lots).</summary>
        bool TryParkingSlot(string key, Vector3 nearWorld, bool outsideOnly, out ParkingSlotInfo slot);
        /// <summary>Role of a lot ("merchant", "garrison", "parking"...).</summary>
        string LotRole(string key, int lot);
        /// <summary>True when the root belongs to a town building.</summary>
        bool IsTownBuilding(Transform root);
        /// <summary>Town template ids (cleared first).</summary>
        void Templates(List<string> into);
        /// <summary>Template data as JSON (Towns editor).</summary>
        bool TryGetTemplate(string templateId, out string json);
        /// <summary>Writes a template (Towns editor); false when invalid.</summary>
        bool SaveTemplate(string templateId, string json);
    }

    /// <summary>Kinds of world locations.</summary>
    public enum WorldLocationKind { Camp, Poi, Town, GasStation, Custom }

    /// <summary>One registered world location.</summary>
    public struct WorldLocation
    {
        /// <summary>Stable key (camp key "Name@x,z", site key, town key...).</summary>
        public string Key;
        /// <summary>Kind.</summary>
        public WorldLocationKind Kind;
        /// <summary>Display name.</summary>
        public string Name;
        /// <summary>Owning faction (null = none).</summary>
        public string Faction;
        /// <summary>Centre, world space.</summary>
        public Vector3 WorldPos;
        /// <summary>Radius, metres.</summary>
        public float Radius;
        /// <summary>Camp state (camps only).</summary>
        public CampState State;
    }

    /// <summary>Camps/POIs/towns registry + state (from Patrols TrafficLocations/TrafficCamps + Towns markers).</summary>
    public interface IWorldLocations
    {
        /// <summary>Increments on every change.</summary>
        int Revision { get; }
        /// <summary>All locations (cleared first).</summary>
        void All(List<WorldLocation> into);
        /// <summary>Location by key.</summary>
        bool TryGet(string key, out WorldLocation location);
        /// <summary>Nearest location of a kind within a radius of a world point.</summary>
        bool TryNearest(Vector3 world, float radius, WorldLocationKind kind, out WorldLocation location);
        /// <summary>Camp state at a world point (replaces ICampStatus.At).</summary>
        CampState StateAt(Vector3 world, float radius);
        /// <summary>Registers/updates a location (other modules' custom places).</summary>
        void Register(IModule owner, WorldLocation location);
        /// <summary>Removes a location.</summary>
        void Remove(string key);
    }

    /// <summary>World-generation facts of Vistas World Gen (provider: Compatibility). Null when Vistas is absent.</summary>
    public interface IWorldGenFacts
    {
        /// <summary>Vistas present and resolved.</summary>
        bool Present { get; }
        /// <summary>Name of the place the player is in (null = none).</summary>
        string CurrentPlace { get; }
        /// <summary>Kind of that place ("town", "camp"...).</summary>
        string CurrentPlaceKind { get; }
        /// <summary>Road network revision (changes when roads are rebuilt).</summary>
        int RoadRevision { get; }
        /// <summary>Road network identity (world seed + version).</summary>
        string RoadIdentity { get; }
        /// <summary>Road polylines, world space (cleared first).</summary>
        void RoadLines(List<Vector3[]> into);
        /// <summary>Height of Vistas place banners (HUD layout).</summary>
        float BannerHeight { get; }
    }
}
