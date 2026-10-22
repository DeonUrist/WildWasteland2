using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// NpcModule part owned by Compatibility (Wave A split): lifecycle of the Dog Companion and Weapons Pack bridges. Partial methods are
    /// called from the NPC entry where the moved lines were.
    /// </summary>
    public sealed partial class NpcModule
    {
        /// <summary>Starts the Dog Companion bridge (only when that mod is installed; [NPC] DogCompanionCompat switches it live).</summary>
        partial void InitCompatibilityDogCompanion()
        {
            DogCompanionCompat.Initialize(this);
        }

        /// <summary>Empty (B17): the Weapons Pack cut sweep is the job "Compatibility.WeaponsPack.Cuts" of the Compatibility module.</summary>
        partial void InitCompatibilityJobs() { }

        /// <summary>Empty (B17): the Weapons Pack bridge shuts down with its Compatibility switch/module.</summary>
        static partial void ShutdownCompatibility() { }

        /// <summary>Empty (B17): Compatibility resets the Weapons Pack state on scene load.</summary>
        static partial void ResetCompatibility() { }

        /// <summary>Empty (B17): Compatibility resets the Weapons Pack state on the Core LoadBegin event.</summary>
        static partial void LoadBeginCompatibility() { }

        /// <summary>Empty (B17): the chainsaw corpse postfix is subscribed by the Compatibility.WeaponsPack scope.</summary>
        partial void InstallHooksCompatibility() { }
    }
}
