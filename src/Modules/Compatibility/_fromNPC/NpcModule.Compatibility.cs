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

        /// <summary>Registers the Weapons Pack bridge job (2 s, scaled time; between Brain and NPC.Rigs as before).</summary>
        partial void InitCompatibilityJobs()
        {
            Scheduler.Interval(this, "Weapons Pack compatibility", 2f, WeaponsPackCompat.Tick, Phase.Update, true);
        }

        /// <summary>Shutdown step "Weapons Pack".</summary>
        static partial void ShutdownCompatibility()
        {
            Step("Weapons Pack", WeaponsPackCompat.Shutdown);
        }

        /// <summary>ResetAll: the Weapons Pack chainsaw / burning state (first, as before).</summary>
        static partial void ResetCompatibility()
        {
            WeaponsPackCompat.Reset();
        }

        /// <summary>A save starts loading: the Weapons Pack state is reset.</summary>
        static partial void LoadBeginCompatibility()
        {
            WeaponsPackCompat.Reset();
        }

        /// <summary>CreateObject.OnEnter postfix: a chainsaw kill's corpse (WeaponsPackCompat.AfterCorpse; after Pounce as before).</summary>
        partial void InstallHooksCompatibility()
        {
            Hooks.CreateObjectEnter.Postfix(this, WeaponsPackCompat.AfterCorpse);
        }
    }
}
