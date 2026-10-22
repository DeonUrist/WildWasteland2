using System;
using System.Collections.Generic;
using UnityEngine;

// Patrols contracts (core-modapi.md 8): patrol/crew queries, road routing + hint providers, camp status, culling protection,
// follower vehicles, merchant layouts (Developer). Provider: Patrols. Consumers: Core Optimization, Quests, HUD, POI, Saves,
// Developer. Interfaces only. FROZEN after W0.
namespace WildWasteland.Core
{
    // ------------------------------------------------------------------ moved unchanged from Services.cs

    /// <summary>Camp states (Unknown/Unloaded never mean empty).</summary>
    public enum CampState { Unknown, Unloaded, Spawning, Ready, Cleared }

    /// <summary>Camp state query (moves to POI IWorldLocations in Wave B; kept for its consumers).</summary>
    public interface ICampStatus
    {
        /// <summary>State of the camp at a stable MapMagic-relative position; Unknown/Unloaded never mean empty.</summary>
        CampState At(Vector3 world, float radius);
    }

    /// <summary>Crew seating services for Quests followers (Patrols). Release lives in <see cref="IFollowerRelease"/>.</summary>
    public interface IFollowerVehicles
    {
        /// <summary>True while the NPC sits in a vehicle seat.</summary>
        bool IsSeated(GameObject npc);
        /// <summary>Seated shooting distance, metres.</summary>
        float ShootDistance { get; }
        /// <summary>Applies a seat pose; returns the pose component.</summary>
        Component SeatPose(GameObject npc, Transform seat, string name, string armLike);
        /// <summary>Starts seated combat; returns the combat component.</summary>
        Component SeatCombat(GameObject npc, GameObject car, Transform seat);
        /// <summary>Removes a pose component.</summary>
        void ReleasePose(Component pose);
        /// <summary>True when the seated NPC may fire now.</summary>
        bool CanFire(GameObject npc);
        /// <summary>Legacy Quests binds these types by reflection; the provider owns the mapping.</summary>
        Type ResolveType(string name);
        /// <summary>Includes dismounted crew while their existing runtime tags are present.</summary>
        bool IsCrew(GameObject npc);
        /// <summary>Enables/disables seated animation.</summary>
        void Animated(GameObject npc, bool enabled);
        /// <summary>Assigns a seated combat target; fire blocked until <paramref name="blockedUntil"/>.</summary>
        void Combat(GameObject npc, GameObject target, float blockedUntil);
    }

    // ------------------------------------------------------------------ new (Wave B Patrols)

    /// <summary>Releases every Patrols component from a follower (replaces Hire.cs destroying Patrols components by type-name string).</summary>
    public interface IFollowerRelease
    {
        /// <summary>Removes crew/seat components Patrols added to the NPC.</summary>
        void Release(GameObject npc);
    }

    /// <summary>Patrol, journey and crew queries.</summary>
    public interface IPatrols
    {
        /// <summary>True for Patrols crew (seated or dismounted while tagged).</summary>
        bool IsCrew(GameObject npc);
        /// <summary>True while the NPC sits in a Patrols vehicle.</summary>
        bool IsSeated(GameObject npc);
        /// <summary>True while any Patrols vehicle fights near the player (autosave deferral).</summary>
        bool VehicleCombatActive { get; }
        /// <summary>Journey id of a Patrols vehicle; -1 when none.</summary>
        int JourneyOf(GameObject vehicle);
        /// <summary>True for vehicles spawned/managed by Patrols.</summary>
        bool IsPatrolVehicle(GameObject vehicle);
        /// <summary>Live patrol vehicles within a radius of a world (MapMagic-relative) point.</summary>
        int PatrolCount(Vector3 world, float radius);
        /// <summary>Total journeys (live + virtual).</summary>
        int JourneyCount { get; }
        /// <summary>True when the faction is hostile to the player.</summary>
        bool IsPlayerHostile(string faction);
    }

    /// <summary>Route hints of another module (POI towns: gates, blocked interiors) (README P5).</summary>
    public interface IRouteHintProvider
    {
        /// <summary>May insert/replace waypoints of a planned route (world space, in order); true when it changed the list.</summary>
        bool Adjust(Vector3 fromWorld, Vector3 toWorld, List<Vector3> routeWorld);
        /// <summary>True when vehicles must not drive through the world point.</summary>
        bool IsBlocked(Vector3 world);
        /// <summary>Increments when hints change (routes revalidate).</summary>
        int Revision { get; }
    }

    /// <summary>Road routing core (TrafficRoads, RoadPlanner, VehicleRoute, ElevationGrid).</summary>
    public interface IVehicleRouting
    {
        /// <summary>Plans a road route (world space, cleared first); false when none.</summary>
        bool TryRoute(Vector3 fromWorld, Vector3 toWorld, List<Vector3> into);
        /// <summary>Nearest road point and direction to a world point.</summary>
        bool TryNearestRoad(Vector3 world, float maxDistance, out Vector3 roadWorld, out Vector3 direction);
        /// <summary>Registers a hint provider (lower order first); returns an id.</summary>
        int RegisterHintProvider(IModule owner, IRouteHintProvider provider, int order);
        /// <summary>Removes a hint provider.</summary>
        void RemoveHintProvider(int id);
        /// <summary>Increments when the road network changes.</summary>
        int Revision { get; }
    }

    /// <summary>Protects module objects from Core culling/cleanup (replaces Performance PatrolsBridge reflection).</summary>
    public interface ICullingProtection
    {
        /// <summary>True when the culler must not deactivate/destroy the object.</summary>
        bool Protected(GameObject go);
        /// <summary>Called right before the culler deletes a far object, so the owner can save/release it.</summary>
        void BeforeFarDelete(GameObject go);
    }

    /// <summary>Developer: merchant item placement layouts.</summary>
    public interface IMerchantLayouts
    {
        /// <summary>Merchant types with layouts.</summary>
        void Types(List<string> into);
        /// <summary>Number of slots of a type.</summary>
        int SlotCount(string merchantType);
        /// <summary>Slot pose local to the merchant vehicle.</summary>
        bool TryGetSlot(string merchantType, int slot, out Vector3 localPosition, out Quaternion localRotation);
        /// <summary>Sets a slot pose live.</summary>
        void SetSlot(string merchantType, int slot, Vector3 localPosition, Quaternion localRotation);
        /// <summary>Writes layouts to disk.</summary>
        bool Save();
    }
}
