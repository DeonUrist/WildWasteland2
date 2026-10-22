using System.Collections.Generic;
using UnityEngine;

// NPC AI contracts (core-modapi.md 8): behaviour control, navigation, followers, seated combat, population. Provider: NPCAI.
// Consumers: Patrols, POI, Quests, HUD, Tools, Compatibility. Interfaces only. FROZEN after W0.
namespace WildWasteland.Core
{
    // ------------------------------------------------------------------ moved unchanged from Services.cs

    /// <summary>A baked NPC navigation structure (camp/cave/building), ex NPCAI.Nav.Structure.</summary>
    public sealed class NavStructure
    {
        /// <summary>Root transform of the structure.</summary>
        public Transform Root;
        /// <summary>Baked bounds (scene space at bake time; re-validate with Box.Contains(Root.position) after origin jumps).</summary>
        public Bounds? Box;
        /// <summary>True once the grid is baked.</summary>
        public bool Baked;
        /// <summary>Floor height per cell (NaN = no floor), row-major with width <see cref="W"/>.</summary>
        public float[] FloorY;
        /// <summary>Grid width in cells.</summary>
        public int W;
        /// <summary>Cell size, metres.</summary>
        public float Cell;
        /// <summary>Grid frame (scene space).</summary>
        public Vector3 GridOrigin, GridRight, GridForward;
        /// <summary>Scene position of the floor of cell <paramref name="index"/>.</summary>
        public Vector3 FloorPoint(int index)
        {
            var p = GridOrigin + GridRight * ((index % W + 0.5f) * Cell) + GridForward * ((index / W + 0.5f) * Cell);
            p.y = FloorY[index]; return p;
        }
    }

    /// <summary>Mirror of NPCAI.Api v1 plus the nav structure list Apocapatrol read by reflection. New members live in
    /// <see cref="INpcControl"/> (an interface cannot grow without breaking its current implementer).</summary>
    public interface INpcApi
    {
        /// <summary>Contract version.</summary>
        int ContractVersion { get; }
        /// <summary>Marks an NPC as a simple convoy bandit (Patrols crews).</summary>
        void SetSimpleConvoyBandit(GameObject npc, bool enabled);
        /// <summary>Simple convoy bandit state.</summary>
        void SetSimpleConvoyBanditState(GameObject npc, bool enabled, bool combat);
        /// <summary>Simple convoy bandit target.</summary>
        void SetSimpleConvoyBanditTarget(GameObject npc, GameObject target);
        /// <summary>weaponKind 0..5 = Pistol, Smg, Rifle, Sniper, Shotgun, Crossbow (shared with Guns).</summary>
        void Shot(GameObject shooter, Vector3 position, int weaponKind, bool player);
        /// <summary>An NPC was hurt by an attacker.</summary>
        void Hurt(GameObject victim, GameObject attacker);
        /// <summary>NPC aim spread factor at a distance.</summary>
        float SpreadFactor(float distance);
        /// <summary>Tells an NPC where a target is.</summary>
        void Inform(GameObject npc, GameObject target);
        /// <summary>Hands the NPC's movement to a follower driver (Quests hire).</summary>
        void SetFollowerDriven(GameObject npc, bool enabled, float reloadUntil = 0f);
        /// <summary>Fills <paramref name="into"/> (cleared first) with the current nav structures.</summary>
        void Structures(List<NavStructure> into);
        /// <summary>Temporary map diagnostic: highlight a POI type with no captured persistent bake.</summary>
        bool HighlightMissingPoiBake(string prefabName);
        /// <summary>Walks an NPC to a scene point with baked-map pathfinding and feelers (an Idle travel leg); null cancels.
        /// Combat takes over while the NPC has a target; the walk resumes once it is idle again. Works for simple convoy bandits too.</summary>
        void Travel(GameObject npc, Vector3? scenePoint, float arriveRadius);
        /// <summary>0 = no travel order, 1 = walking (or paused by combat), 2 = arrived, 3 = failed (gets nowhere / no NPC module brain).</summary>
        int TravelState(GameObject npc);
        /// <summary>True while the NPC has no target, ghost or search (simple convoy bandits: no target).</summary>
        bool IsIdle(GameObject npc);
        /// <summary>Awareness memory: the NPC knows where <paramref name="target"/> is for <paramref name="seconds"/> (it fought it),
        /// so losing sight does not make it forget. Refreshed by the NPC module itself while they keep fighting.</summary>
        void Remember(GameObject npc, GameObject target, float seconds);
        /// <summary>Assigns a crew's known attacker, or suspends combat while it walks back to board. Null/false releases the order.</summary>
        bool CrewOrder(GameObject npc, GameObject enemy, bool boarding);
    }

    // ------------------------------------------------------------------ new (Wave B NPCAI)

    /// <summary>High-level behaviour mode of an NPC (published with NpcStateChanged).</summary>
    public enum NpcMode { Idle, Travel, Investigate, Search, Combat, Cover, Flee, Follow, Seated, Hide, Dead }   // refs-ok: enum values / field names, not module types

    /// <summary>Perception parameters of a player-sight query (patrol crews, traffic agents, sentries).</summary>
    public struct PerceptionProfile
    {
        /// <summary>Maximum sight distance, metres.</summary>
        public float SightRange;
        /// <summary>Full field-of-view angle, degrees.</summary>
        public float FovDegrees;
        /// <summary>Hearing distance for player noise, metres (0 = deaf).</summary>
        public float HearingRange;
        /// <summary>Scale sight by day light / storms (NPCAI senses rules).</summary>
        public bool UseLight;
        /// <summary>Layer mask of occluders for the line-of-sight ray (0 = NPCAI default).</summary>
        public int OccluderMask;
    }

    /// <summary>Additional NPC controls of the NPCAI module (README 6.2, 6.5): seating, fright, perception, hostility, enumeration.</summary>
    public interface INpcControl
    {
        /// <summary>Mutes/unmutes the AI of a seated crew member (replaces Patrols Crew.MuteAi).</summary>
        void SetSeated(GameObject npc, bool seated);
        /// <summary>Makes an NPC flee/avoid a threat for <paramref name="seconds"/> (Weapons Pack fire/chainsaw).</summary>
        void Frighten(GameObject npc, Vector3 threatScene, float seconds, string cause);
        /// <summary>Player-perception query from an arbitrary eye (traffic agents, patrol visibility): true when the player is seen or heard.</summary>
        bool CanPerceivePlayer(Vector3 fromScene, Vector3 forward, PerceptionProfile profile);
        /// <summary>Current behaviour mode of an NPC.</summary>
        NpcMode ModeOf(GameObject npc);
        /// <summary>Current target of an NPC (player, NPC or vehicle).</summary>
        bool TryGetTarget(GameObject npc, out GameObject target);
        /// <summary>Hearing policy for interior/town populations (deaf = ignores noise; interior = only hears inside its structure).</summary>
        void SetHearingPolicy(GameObject npc, bool deaf, bool interior);
        /// <summary>Makes an NPC hostile/friendly to the player (replaces direct FSM writes from Quests).</summary>
        void SetPlayerHostile(GameObject npc, bool hostile);
        /// <summary>Fills <paramref name="into"/> (cleared first) with live NPCs within <paramref name="radius"/> of a scene point.</summary>
        void NpcsNear(Vector3 scene, float radius, List<GameObject> into);
        /// <summary>True when the object is an NPC known to NPCAI.</summary>
        bool IsNpc(GameObject go);
        /// <summary>Faction tag of an NPC (null when unknown).</summary>
        string FactionOf(GameObject npc);
        /// <summary>Boss health adjustment applied to a native boss (from Guns Bosses).</summary>
        bool IsBoss(GameObject npc);
    }

    /// <summary>Summary of a navigation template (vanilla/custom/baked POI map).</summary>
    public struct NavTemplateInfo
    {
        /// <summary>POI prefab name.</summary>
        public string Prefab;
        /// <summary>Bake signature (mesh-name SHA).</summary>
        public string Signature;
        /// <summary>From the custom folder.</summary>
        public bool Custom;
        /// <summary>A persistent bake exists.</summary>
        public bool Baked;
        /// <summary>Number of PoiNav links (ladders, climbs, drops).</summary>
        public int Links;
    }

    /// <summary>Instance overlay patching walkable cells of one structure (town lots, dressing).</summary>
    public interface INavOverlay
    {
        /// <summary>True when the overlay touches the given scene bounds.</summary>
        bool Affects(Bounds sceneBounds);
        /// <summary>True when the scene point is blocked by the overlay.</summary>
        bool IsBlocked(Vector3 scenePoint);
        /// <summary>Overrides the floor height at a scene point.</summary>
        bool TryFloorHeight(Vector3 scenePoint, out float y);
    }

    /// <summary>Waypoint provider (town gates/streets) consulted before grid routing.</summary>
    public interface IWaypointProvider
    {
        /// <summary>Next waypoint for an NPC going from <paramref name="fromScene"/> to <paramref name="goalScene"/>; false = no opinion.</summary>
        bool TryNext(GameObject npc, Vector3 fromScene, Vector3 goalScene, out Vector3 waypointScene);
    }

    /// <summary>One navigation service: grid baseline, PoiNav graph, town overlays and waypoint providers (README 6.2, P6).</summary>
    public interface INavApi
    {
        /// <summary>Fills <paramref name="into"/> (cleared first) with the current nav structures.</summary>
        void Structures(List<NavStructure> into);
        /// <summary>Structure containing a scene point, or null.</summary>
        NavStructure StructureAt(Vector3 scene);
        /// <summary>Asks NPCAI to discover/bake the structure under <paramref name="root"/> (town buildings, custom POIs).</summary>
        void Discover(Transform root);
        /// <summary>Drops the structure of <paramref name="root"/> (streamed out / rebuilt).</summary>
        void ForgetStructure(Transform root);
        /// <summary>Bakes a POI prefab's map in isolation (editor/tools); false when not possible now.</summary>
        bool BakeIsolated(string prefabName);
        /// <summary>Template info for a POI prefab.</summary>
        bool TryGetTemplate(string prefabName, out NavTemplateInfo info);
        /// <summary>Path of scene points from one point to another (cleared first); false when unreachable.</summary>
        bool TryPath(Vector3 fromScene, Vector3 toScene, List<Vector3> into);
        /// <summary>Cover spot near <paramref name="scene"/> hidden from <paramref name="threatScene"/>.</summary>
        bool TryFindCover(Vector3 scene, Vector3 threatScene, float radius, out Vector3 coverScene);
        /// <summary>Registers an overlay for one structure root; returns an id for <see cref="RemoveOverlay"/>.</summary>
        int RegisterOverlay(IModule owner, Transform root, INavOverlay overlay);
        /// <summary>Removes an overlay.</summary>
        void RemoveOverlay(int id);
        /// <summary>Registers a waypoint provider (lower order = consulted first); returns an id.</summary>
        int RegisterWaypointProvider(IModule owner, IWaypointProvider provider, int order);
        /// <summary>Removes a waypoint provider.</summary>
        void RemoveWaypointProvider(int id);
        /// <summary>"Setup POI pathfinding" developer mode (ex Services.PoiNavSetup): POI/Towns plan no new towns while true.</summary>
        bool WorldGenerationSuspended { get; set; }
        /// <summary>Increments whenever structures, overlays or providers change.</summary>
        int Revision { get; }
    }

    /// <summary>Follower AI (ex Quests Hire/QuestFollower): NPCs that follow and fight for the player.</summary>
    public interface IFollowers
    {
        /// <summary>Makes <paramref name="npc"/> follow <paramref name="leader"/> (the player when null).</summary>
        bool Follow(GameObject npc, GameObject leader);
        /// <summary>Releases a follower to normal AI.</summary>
        void Dismiss(GameObject npc);
        /// <summary>Boards a follower into a vehicle seat.</summary>
        bool Mount(GameObject npc, GameObject vehicle, Transform seat);
        /// <summary>Gets a follower out of its vehicle.</summary>
        void Dismount(GameObject npc);
        /// <summary>True for current followers.</summary>
        bool IsFollower(GameObject npc);
        /// <summary>Fills <paramref name="into"/> (cleared first) with current followers.</summary>
        void Followers(List<GameObject> into);
        /// <summary>Number of followers.</summary>
        int Count { get; }
    }

    /// <summary>Combat from vehicle seats (ex Patrols PassengerGuard): NPCAI drives the seated attack.</summary>
    public interface ISeatedCombat
    {
        /// <summary>Starts seated combat for a passenger at <paramref name="seat"/> of <paramref name="vehicle"/>.</summary>
        bool Engage(GameObject npc, GameObject vehicle, Transform seat);
        /// <summary>Stops seated combat.</summary>
        void Release(GameObject npc);
        /// <summary>True while engaged.</summary>
        bool IsEngaged(GameObject npc);
        /// <summary>True when the seated NPC has a clear shot now.</summary>
        bool CanFire(GameObject npc);
        /// <summary>Assigns a target; fire is blocked until <paramref name="blockedUntil"/> (Time.time).</summary>
        void SetTarget(GameObject npc, GameObject target, float blockedUntil);
        /// <summary>Maximum seated shooting distance, metres.</summary>
        float ShootDistance { get; }
    }

    /// <summary>Population rules (interior population, native POI population, town residents).</summary>
    public interface IPopulation
    {
        /// <summary>Registers an interior root populated by NPCAI rules; <paramref name="locationKey"/> is the stable POI/town key.</summary>
        void RegisterInterior(IModule owner, Transform root, string locationKey, bool deafOutside);
        /// <summary>Removes an interior registration.</summary>
        void UnregisterInterior(Transform root);
        /// <summary>Registers which NPC prefabs a spawner catalog entry may produce.</summary>
        void RegisterCatalog(IModule owner, string spawnerKey, IList<string> npcPrefabs);
        /// <summary>Live NPCs of a faction within a radius (scene space); null faction = all.</summary>
        int CountNear(Vector3 scene, float radius, string faction);
        /// <summary>True once the root's population has been rolled.</summary>
        bool IsPopulated(Transform root);
    }
}
