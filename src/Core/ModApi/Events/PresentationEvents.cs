using UnityEngine;

// Character presentation events (publisher: Presentation unless noted).
// Event payloads (core-modapi.md 2, 9): structs with ids, enums, numbers, strings and WORLD-space vectors only (Origin.World),
// never GameObject/Component references (use EntityId / Entities). [Broadcast] marks events a future multiplayer transport may
// send; the rest are local. Publish with ModEvents.Publish(ref evt); check ModEvents.HasListeners<T>() before building costly
// payloads. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core.Events
{
    /// <summary>A character is now driven by the presentation pipeline.</summary>
    [Broadcast(Authority.Host)]
    public struct CharacterAttached : IModEvent
    {
        /// <summary>Character entity.</summary>
        public EntityId Character;
        /// <summary>Rig kind.</summary>
        public CharacterKind Kind;
        /// <summary>Model/identity id (null = native).</summary>
        public string ModelId;
    }

    /// <summary>A character is no longer driven (death, despawn, detach).</summary>
    [Broadcast(Authority.Host)]
    public struct CharacterDetached : IModEvent
    {
        /// <summary>Character entity.</summary>
        public EntityId Character;
        /// <summary>Rig kind.</summary>
        public CharacterKind Kind;
    }

    /// <summary>A driver asks a character to play an action (NPCAI/Patrols/player input -> Presentation).</summary>
    [Broadcast(Authority.Owner)]
    public struct CharacterAction : IModEvent
    {
        /// <summary>Character entity.</summary>
        public EntityId Character;
        /// <summary>Action.</summary>
        public CharacterActionKind Action;
        /// <summary>Weapon key (may be null).</summary>
        public string WeaponKey;
        /// <summary>Duration for timed actions (0 = clip length).</summary>
        public float Seconds;
        /// <summary>Rounds for reloads (0 = full).</summary>
        public int Rounds;
    }

    /// <summary>A character model/variant changed (Presentation -> HUD, Quests).</summary>
    [Broadcast(Authority.Owner)]
    public struct CharacterModelChanged : IModEvent
    {
        /// <summary>Character entity.</summary>
        public EntityId Character;
        /// <summary>New model id.</summary>
        public string ModelId;
        /// <summary>Variant name (may be null).</summary>
        public string Variant;
    }

    /// <summary>A character started a traversal (ladder, climb, drop, crawl).</summary>
    [Broadcast(Authority.Owner)]
    public struct TraversalStarted : IModEvent
    {
        /// <summary>Character entity.</summary>
        public EntityId Character;
        /// <summary>Traversal kind.</summary>
        public TraversalKind Kind;
        /// <summary>Start, world space.</summary>
        public Vector3 StartWorld;
        /// <summary>Planned end, world space.</summary>
        public Vector3 EndWorld;
    }

    /// <summary>A traversal ended.</summary>
    [Broadcast(Authority.Owner)]
    public struct TraversalEnded : IModEvent
    {
        /// <summary>Character entity.</summary>
        public EntityId Character;
        /// <summary>Traversal kind.</summary>
        public TraversalKind Kind;
        /// <summary>Final position, world space.</summary>
        public Vector3 EndWorld;
        /// <summary>Finished (false = cancelled).</summary>
        public bool Completed;
    }

    /// <summary>A character took or left a seat pose (Presentation -> Vehicles, Patrols).</summary>
    [Broadcast(Authority.Owner)]
    public struct SeatPoseChanged : IModEvent
    {
        /// <summary>Character entity.</summary>
        public EntityId Character;
        /// <summary>Vehicle entity (None when leaving).</summary>
        public EntityId Vehicle;
        /// <summary>Seat kind.</summary>
        public SeatKind Seat;
        /// <summary>Seat index on the vehicle (-1 unknown).</summary>
        public int SeatIndex;
        /// <summary>True = seated, false = released.</summary>
        public bool Seated;
    }

    /// <summary>The player camera view mode changed (Presentation camera -> Gunplay, HUD, Quests). Local only.</summary>
    public struct CameraModeChanged : IModEvent
    {
        /// <summary>Previous mode.</summary>
        public CameraViewMode Old;
        /// <summary>New mode.</summary>
        public CameraViewMode New;
    }

    /// <summary>A character voiced a cue (pain/death/shout).</summary>
    [Broadcast(Authority.Owner)]
    public struct CharacterVoice : IModEvent
    {
        /// <summary>Character entity.</summary>
        public EntityId Character;
        /// <summary>Voice clip/cue id.</summary>
        public string ClipId;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }
}
