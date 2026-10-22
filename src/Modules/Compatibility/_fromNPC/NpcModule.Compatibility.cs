using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// NpcModule part owned by Compatibility (Wave A split). Since B17 every method is empty: the Dog Companion and Weapons Pack bridges
    /// live in the Compatibility module. Wave C deletes these partial methods with the old NPC entry.
    /// </summary>
    public sealed partial class NpcModule
    {
        /// <summary>Empty (B17): the Dog Companion bridge is started by the Compatibility module (it acts while NPC runs).</summary>
        partial void InitCompatibilityDogCompanion() { }

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
