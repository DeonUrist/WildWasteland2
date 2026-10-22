using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// NpcModule part owned by Presentation (B05). Wave B moved everything these partial methods did into PresentationModule: the dog
    /// animation bundle and its hooks, Women of Wasteland, the NPC rig builder and the visual LateUpdate ticks, the Pounce carcass hook,
    /// the taunt AudioPlay patch and the presentation config entries ([Presentation / ...], CharacterSettings). The implementations stay
    /// empty until Wave C deletes the declarations in NpcModule.cs / NpcModule.Config.cs.
    /// </summary>
    public sealed partial class NpcModule
    {
        /// <summary>Empty (B05): the dog animation bundle and hooks start in PresentationModule.</summary>
        partial void InitPresentationDogs() { }

        /// <summary>Empty (B05): Women of Wasteland starts in PresentationModule.</summary>
        partial void InitPresentationWomen() { }

        /// <summary>Empty (B05): "Presentation.NpcRigs" and the visual LateUpdate ticks are PresentationModule jobs.</summary>
        partial void InitPresentationJobs() { }

        /// <summary>Empty (B05): Women of Wasteland stops in PresentationModule.Shutdown.</summary>
        static partial void ShutdownPresentationWomen() { }

        /// <summary>Empty (B05): Gungirl's scene caches are reset by PresentationModule.OnSceneLoaded.</summary>
        static partial void SceneLoadedPresentation() { }

        /// <summary>Empty (B05): Gungirl's world scan starts in PresentationModule.OnWorldReady.</summary>
        static partial void WorldReadyPresentation() { }

        /// <summary>Empty (B05): the Pounce carcass CreateObject postfix is subscribed by PresentationModule.</summary>
        partial void InstallHooksPresentation() { }

        /// <summary>Empty (B05): the taunt AudioPlay prefix is patched by PresentationModule.</summary>
        partial void InstallPatchesPresentation() { }

        /// <summary>Empty (B05): DogAnimations / DogsLieDown are [Presentation / Dogs] entries (CharacterSettings).</summary>
        static partial void BindPresentationToggles() { }

        /// <summary>Empty (B05): DogLieFrontLift is a [Presentation / Dog poses] entry (CharacterSettings).</summary>
        static partial void BindPresentationDogPose() { }

        /// <summary>Empty (B05): ReloadVolume / ReloadSoundRange are [Presentation / NPC sounds] entries (CharacterSettings).</summary>
        static partial void BindPresentationReload() { }

        /// <summary>Empty (B05): AimPose is a [Presentation / NPC animation] entry (CharacterSettings).</summary>
        static partial void BindPresentationAimPose() { }

        /// <summary>Empty (B05): ApocaplayerClips is a [Presentation / NPC animation] entry (CharacterSettings).</summary>
        static partial void BindPresentationClips() { }
    }
}
