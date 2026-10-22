using UnityEngine;

// Save/load lifecycle events (publisher: SaveBus in Core for LoadBegin/WorldReady/SaveBegin/SaveEnd; Saves for the rest).
// Event payloads (core-modapi.md 2, 9): structs with ids, enums, numbers, strings and WORLD-space vectors only (Origin.World),
// never GameObject/Component references (use EntityId / Entities). [Broadcast] marks events a future multiplayer transport may
// send; the rest are local. Publish with ModEvents.Publish(ref evt); check ModEvents.HasListeners<T>() before building costly
// payloads. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core.Events
{
    /// <summary>Phase of a save/load presentation.</summary>
    public enum SavePresentationPhase { Begin, Progress, End }

    /// <summary>Kind of combat activity for autosave deferral.</summary>
    public enum CombatActivityKind { Shot, Hit, Detected, Attack, VehicleCombat }

    /// <summary>SaveLoadGame entered LoadGame: a load starts (cancel pending restores). Local only.</summary>
    public struct LoadBegin : IModEvent
    {
        /// <summary>Slot file name or null.</summary>
        public string Slot;
        /// <summary>SaveBus generation after the increment.</summary>
        public int Generation;
    }

    /// <summary>First isPlay after a load / new game / scene reset. Local only.</summary>
    public struct WorldReady : IModEvent
    {
        /// <summary>Slot file name or null (new game).</summary>
        public string Slot;
        /// <summary>SaveBus generation.</summary>
        public int Generation;
        /// <summary>Reached after a LoadGame (false = new game / scene).</summary>
        public bool FromLoad;
    }

    /// <summary>SaveLoadGame entered SaveGame. Local only.</summary>
    public struct SaveBegin : IModEvent
    {
        /// <summary>Slot file name or null.</summary>
        public string Slot;
        /// <summary>SaveBus generation.</summary>
        public int Generation;
        /// <summary>Manual/auto/exit (Manual when unknown to Core).</summary>
        public SaveKind Kind;
    }

    /// <summary>Right before the game serializes (revert visual edits on saved objects). Published by Saves. Local only.</summary>
    public struct BeforeSerialize : IModEvent
    {
        /// <summary>Slot file name.</summary>
        public string Slot;
        /// <summary>SaveBus generation.</summary>
        public int Generation;
        /// <summary>Save kind.</summary>
        public SaveKind Kind;
    }

    /// <summary>Right after the game serialized (re-apply visual edits). Published by Saves. Local only.</summary>
    public struct AfterSerialize : IModEvent
    {
        /// <summary>Slot file name.</summary>
        public string Slot;
        /// <summary>SaveBus generation.</summary>
        public int Generation;
        /// <summary>Save kind.</summary>
        public SaveKind Kind;
        /// <summary>Serialization succeeded.</summary>
        public bool Ok;
    }

    /// <summary>Native serialization returned to isPlay; embedded records are still verified before save publication. Local only.</summary>
    public struct SaveEnd : IModEvent
    {
        /// <summary>Slot file name or null.</summary>
        public string Slot;
        /// <summary>SaveBus generation.</summary>
        public int Generation;
        /// <summary>Save kind.</summary>
        public SaveKind Kind;
        /// <summary>True unless a failure was reported (Saves sets it; Core publishes true).</summary>
        public bool Ok;
    }

    /// <summary>Loading/saving screen state (Saves -> HUD). Local only.</summary>
    public struct SavePresentation : IModEvent
    {
        /// <summary>Begin/progress/end.</summary>
        public SavePresentationPhase Phase;
        /// <summary>Saving or loading.</summary>
        public SavePhase Operation;
        /// <summary>Progress 0..1.</summary>
        public float Fraction;
        /// <summary>Detail text.</summary>
        public string Text;
    }

    /// <summary>Combat happened (Gunplay/NPCAI/Patrols -> Saves autosave deferral). Local only.</summary>
    public struct CombatActivity : IModEvent
    {
        /// <summary>Actor entity (player, NPC or vehicle).</summary>
        public EntityId Actor;
        /// <summary>What happened.</summary>
        public CombatActivityKind Kind;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }
}
