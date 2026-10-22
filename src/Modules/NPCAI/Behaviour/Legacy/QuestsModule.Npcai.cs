namespace WildWasteland.Modules.Quests
{
    /// <summary>Quests entry, NPCAI part (Wave A, from Quests/QuestsModule.cs; B07): drives the hired follower's body (QuestFollower).</summary>
    public sealed partial class QuestsModule
    {
        /// <summary>Follower body tick, every frame after the engine frame.</summary>
        partial void FrameNpcai() { QuestFollower.Tick(); }
        /// <summary>Follower late pose, after the engine's late frame.</summary>
        partial void LateNpcai() { QuestFollower.Late(); }
        /// <summary>Releases the follower body on shutdown.</summary>
        partial void ShutdownNpcai() { QuestFollower.Release(); }
    }
}
