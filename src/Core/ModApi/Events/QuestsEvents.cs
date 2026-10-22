using UnityEngine;

// Quest events (publisher: Quests).
// Event payloads (core-modapi.md 2, 9): structs with ids, enums, numbers, strings and WORLD-space vectors only (Origin.World),
// never GameObject/Component references (use EntityId / Entities). [Broadcast] marks events a future multiplayer transport may
// send; the rest are local. Publish with ModEvents.Publish(ref evt); check ModEvents.HasListeners<T>() before building costly
// payloads. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core.Events
{
    /// <summary>Ways a quest stops.</summary>
    public enum QuestOutcome { Completed, Failed, Cancelled, Abandoned }

    /// <summary>A quest was offered.</summary>
    [Broadcast(Authority.Host)]
    public struct QuestOffered : IModEvent
    {
        /// <summary>Quest id.</summary>
        public string QuestId;
        /// <summary>Quest type.</summary>
        public string Type;
        /// <summary>Giver entity.</summary>
        public EntityId Giver;
        /// <summary>Giver faction key.</summary>
        public string Faction;
        /// <summary>Target position, world space.</summary>
        public Vector3 TargetWorld;
        /// <summary>Required count.</summary>
        public int Count;
        /// <summary>Reward.</summary>
        public int Reward;
    }

    /// <summary>A quest was accepted.</summary>
    [Broadcast(Authority.Host)]
    public struct QuestAccepted : IModEvent
    {
        /// <summary>Quest id.</summary>
        public string QuestId;
        /// <summary>Giver entity.</summary>
        public EntityId Giver;
        /// <summary>Faction key.</summary>
        public string Faction;
    }

    /// <summary>A quest was declined.</summary>
    [Broadcast(Authority.Host)]
    public struct QuestDeclined : IModEvent
    {
        /// <summary>Quest id.</summary>
        public string QuestId;
        /// <summary>Giver entity.</summary>
        public EntityId Giver;
    }

    /// <summary>Quest progress changed.</summary>
    [Broadcast(Authority.Host)]
    public struct QuestProgress : IModEvent
    {
        /// <summary>Quest id.</summary>
        public string QuestId;
        /// <summary>Progress.</summary>
        public int Progress;
        /// <summary>Required count.</summary>
        public int Count;
        /// <summary>Objective done.</summary>
        public bool ObjectiveDone;
    }

    /// <summary>A quest was completed.</summary>
    [Broadcast(Authority.Host)]
    public struct QuestCompleted : IModEvent
    {
        /// <summary>Quest id.</summary>
        public string QuestId;
        /// <summary>Reward.</summary>
        public int Reward;
        /// <summary>Reputation change.</summary>
        public int ReputationDelta;
    }

    /// <summary>A quest failed.</summary>
    [Broadcast(Authority.Host)]
    public struct QuestFailed : IModEvent
    {
        /// <summary>Quest id.</summary>
        public string QuestId;
        /// <summary>Reason.</summary>
        public string Reason;
        /// <summary>Reputation change.</summary>
        public int ReputationDelta;
    }

    /// <summary>A quest was cancelled.</summary>
    [Broadcast(Authority.Host)]
    public struct QuestCancelled : IModEvent
    {
        /// <summary>Quest id.</summary>
        public string QuestId;
        /// <summary>Reason.</summary>
        public string Reason;
    }

    /// <summary>The player abandoned a quest.</summary>
    [Broadcast(Authority.Host)]
    public struct QuestAbandoned : IModEvent
    {
        /// <summary>Quest id.</summary>
        public string QuestId;
        /// <summary>Reputation change.</summary>
        public int ReputationDelta;
    }

    /// <summary>Faction reputation changed.</summary>
    [Broadcast(Authority.Host)]
    public struct ReputationChanged : IModEvent
    {
        /// <summary>Faction key.</summary>
        public string Faction;
        /// <summary>Previous value.</summary>
        public int Old;
        /// <summary>New value.</summary>
        public int New;
        /// <summary>Reason code.</summary>
        public string Reason;
    }

    /// <summary>An NPC/faction became hostile or friendly to the player (Quests -> NPCAI).</summary>
    [Broadcast(Authority.Host)]
    public struct PlayerHostilityChanged : IModEvent
    {
        /// <summary>NPC entity (None = whole faction).</summary>
        public EntityId Npc;
        /// <summary>Faction key (may be null).</summary>
        public string Faction;
        /// <summary>Hostile now.</summary>
        public bool Hostile;
    }

    /// <summary>A conversation opened.</summary>
    [Broadcast(Authority.Owner)]
    public struct ConversationOpened : IModEvent
    {
        /// <summary>Speaker entity.</summary>
        public EntityId Npc;
        /// <summary>Related quest (may be null).</summary>
        public string QuestId;
    }

    /// <summary>A conversation choice was selected.</summary>
    [Broadcast(Authority.Owner)]
    public struct ChoiceSelected : IModEvent
    {
        /// <summary>Speaker entity.</summary>
        public EntityId Npc;
        /// <summary>Choice key.</summary>
        public string ChoiceKey;
    }

    /// <summary>A conversation closed.</summary>
    [Broadcast(Authority.Owner)]
    public struct ConversationClosed : IModEvent
    {
        /// <summary>Speaker entity.</summary>
        public EntityId Npc;
    }
}
