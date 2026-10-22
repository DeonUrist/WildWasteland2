using UnityEngine;

// NPC AI events (publisher: NPCAI unless noted).
// Event payloads (core-modapi.md 2, 9): structs with ids, enums, numbers, strings and WORLD-space vectors only (Origin.World),
// never GameObject/Component references (use EntityId / Entities). [Broadcast] marks events a future multiplayer transport may
// send; the rest are local. Publish with ModEvents.Publish(ref evt); check ModEvents.HasListeners<T>() before building costly
// payloads. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core.Events
{
    /// <summary>Kind of noise for hearing.</summary>
    public enum NoiseKind { Shot, Shout, Engine, Horn, Thrown, Explosion, Melee, Footsteps }

    /// <summary>Why an NPC left the registry.</summary>
    public enum NpcRemovalReason { Despawned, Died, Culled, SceneUnload, Converted }

    /// <summary>Burrowing creature states.</summary>
    public enum BurrowState { Out, Burrowing, Underground, Rising }

    /// <summary>An NPC entered the world (NPCAI -> POI, Quests, Presentation, Survival).</summary>
    [Broadcast(Authority.Host)]
    public struct NpcSpawned : IModEvent
    {
        /// <summary>NPC entity.</summary>
        public EntityId Npc;
        /// <summary>Prefab name.</summary>
        public string Prefab;
        /// <summary>Faction tag.</summary>
        public string Faction;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Source ("spawner", "town", "patrol", "load"...).</summary>
        public string Reason;
        /// <summary>Owning spawner path (may be null).</summary>
        public string SpawnerPath;
    }

    /// <summary>An NPC left the world.</summary>
    [Broadcast(Authority.Host)]
    public struct NpcDespawned : IModEvent
    {
        /// <summary>NPC entity.</summary>
        public EntityId Npc;
        /// <summary>Prefab name.</summary>
        public string Prefab;
        /// <summary>Faction tag.</summary>
        public string Faction;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Why.</summary>
        public NpcRemovalReason Reason;
    }

    /// <summary>Behaviour mode changed (NPCAI -> HUD, Saves, Debug).</summary>
    [Broadcast(Authority.Host)]
    public struct NpcStateChanged : IModEvent
    {
        /// <summary>NPC entity.</summary>
        public EntityId Npc;
        /// <summary>Previous mode.</summary>
        public NpcMode Old;
        /// <summary>New mode.</summary>
        public NpcMode New;
        /// <summary>Target entity (None when none).</summary>
        public EntityId Target;
    }

    /// <summary>An NPC died (NPCAI -> Quests, Loot, HUD).</summary>
    [Broadcast(Authority.Host)]
    public struct NpcKilled : IModEvent
    {
        /// <summary>NPC entity.</summary>
        public EntityId Npc;
        /// <summary>Killer entity (None when unknown).</summary>
        public EntityId Killer;
        /// <summary>Saved corpse object name.</summary>
        public string CorpseName;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Faction/creature tag.</summary>
        public string Tag;
    }

    /// <summary>A noise NPCs may hear (NPCAI, Gunplay -> NPCAI).</summary>
    [Broadcast(Authority.Host)]
    public struct NoiseEmitted : IModEvent
    {
        /// <summary>Source entity.</summary>
        public EntityId Source;
        /// <summary>Noise kind.</summary>
        public NoiseKind Kind;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Audible radius, metres.</summary>
        public float Radius;
        /// <summary>Made by the player.</summary>
        public bool Player;
    }

    /// <summary>An NPC is frightened by a threat (Compatibility Weapons Pack / NPCAI -> NPCAI).</summary>
    [Broadcast(Authority.Host)]
    public struct NpcFrightened : IModEvent
    {
        /// <summary>NPC entity.</summary>
        public EntityId Npc;
        /// <summary>Threat entity (may be None).</summary>
        public EntityId Threat;
        /// <summary>Threat world position.</summary>
        public Vector3 ThreatWorld;
        /// <summary>Duration.</summary>
        public float Seconds;
        /// <summary>Cause ("fire", "chainsaw"...).</summary>
        public string Cause;
    }

    /// <summary>A spider nest was built.</summary>
    [Broadcast(Authority.Host)]
    public struct NestBuilt : IModEvent
    {
        /// <summary>Nest key (POI name + rounded position).</summary>
        public string NestKey;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A spider nest was freed/destroyed.</summary>
    [Broadcast(Authority.Host)]
    public struct NestFreed : IModEvent
    {
        /// <summary>Nest key.</summary>
        public string NestKey;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A spider nest raised the alarm.</summary>
    [Broadcast(Authority.Host)]
    public struct NestAlarm : IModEvent
    {
        /// <summary>Nest key.</summary>
        public string NestKey;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Intruder entity.</summary>
        public EntityId Intruder;
    }

    /// <summary>Something touched a web.</summary>
    [Broadcast(Authority.Host)]
    public struct WebTouched : IModEvent
    {
        /// <summary>Toucher entity.</summary>
        public EntityId Toucher;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Owning nest key (may be null).</summary>
        public string NestKey;
    }

    /// <summary>A burrowing creature changed state.</summary>
    [Broadcast(Authority.Host)]
    public struct BurrowChanged : IModEvent
    {
        /// <summary>NPC entity.</summary>
        public EntityId Npc;
        /// <summary>New state.</summary>
        public BurrowState State;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Target world position.</summary>
        public Vector3 TargetWorld;
    }

    /// <summary>A creature leapt at a target.</summary>
    [Broadcast(Authority.Host)]
    public struct PounceLeap : IModEvent
    {
        /// <summary>NPC entity.</summary>
        public EntityId Npc;
        /// <summary>Target entity.</summary>
        public EntityId Target;
        /// <summary>Start, world space.</summary>
        public Vector3 FromWorld;
        /// <summary>Aim point, world space.</summary>
        public Vector3 ToWorld;
    }

    /// <summary>A dog bit a target.</summary>
    [Broadcast(Authority.Host)]
    public struct DogBite : IModEvent
    {
        /// <summary>Dog entity.</summary>
        public EntityId Npc;
        /// <summary>Target entity.</summary>
        public EntityId Target;
        /// <summary>Bite position, world space.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A follower was hired (NPCAI/Quests -> Quests, HUD).</summary>
    [Broadcast(Authority.Host)]
    public struct FollowerHired : IModEvent
    {
        /// <summary>Follower entity.</summary>
        public EntityId Follower;
        /// <summary>Leader entity (player).</summary>
        public EntityId Leader;
        /// <summary>Follower faction.</summary>
        public string Faction;
    }

    /// <summary>A follower was dismissed.</summary>
    [Broadcast(Authority.Host)]
    public struct FollowerDismissed : IModEvent
    {
        /// <summary>Follower entity.</summary>
        public EntityId Follower;
        /// <summary>Leader entity.</summary>
        public EntityId Leader;
    }

    /// <summary>A follower died.</summary>
    [Broadcast(Authority.Host)]
    public struct FollowerDied : IModEvent
    {
        /// <summary>Follower entity.</summary>
        public EntityId Follower;
        /// <summary>Leader entity.</summary>
        public EntityId Leader;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A follower got lost (too far / unreachable).</summary>
    [Broadcast(Authority.Host)]
    public struct FollowerLost : IModEvent
    {
        /// <summary>Follower entity.</summary>
        public EntityId Follower;
        /// <summary>Leader entity.</summary>
        public EntityId Leader;
    }

    /// <summary>A follower boarded a vehicle.</summary>
    [Broadcast(Authority.Host)]
    public struct FollowerMounted : IModEvent
    {
        /// <summary>Follower entity.</summary>
        public EntityId Follower;
        /// <summary>Leader entity.</summary>
        public EntityId Leader;
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
    }

    /// <summary>A follower left a vehicle.</summary>
    [Broadcast(Authority.Host)]
    public struct FollowerDismounted : IModEvent
    {
        /// <summary>Follower entity.</summary>
        public EntityId Follower;
        /// <summary>Leader entity.</summary>
        public EntityId Leader;
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
    }
}
