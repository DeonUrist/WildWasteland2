namespace WildWasteland.Core
{
    /// <summary>Presentation-owned animation preferences consumed by the NPC controller.</summary>
    public interface INpcAnimationPolicy
    {
        bool PlayerClips { get; }
        bool AimPose { get; }
    }
}
