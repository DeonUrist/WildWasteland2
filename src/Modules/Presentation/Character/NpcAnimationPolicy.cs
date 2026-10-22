using WildWasteland.Core;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Projects actual animation settings without exposing foreign config implementation to NPC behavior.</summary>
    internal sealed class NpcAnimationPolicy : INpcAnimationPolicy
    {
        public bool PlayerClips=>PresentationModule.Running&&CharacterSettings.ApocaplayerClips.Value;
        public bool AimPose=>PresentationModule.Running&&CharacterSettings.AimPose.Value;
    }
}
