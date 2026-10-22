using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// NpcModule part owned by Gunplay (Wave A split): the vanilla shot / damage observers (VanillaEvents) and the NPC gun cache sweep
    /// (WeaponRanges). Partial methods are called from the NPC entry where the moved lines were.
    /// </summary>
    public sealed partial class NpcModule
    {
        /// <summary>Registers the 30 s WeaponRanges.Sweep job (after Aim.Sweep, as before).</summary>
        partial void InitGunplayJobs()
        {
            Scheduler.Interval(this, "WeaponRanges.Sweep", 30f, WeaponRanges.Sweep);
        }

        /// <summary>Shutdown step "VanillaEvents" (restores the NPC burst ray lengths).</summary>
        static partial void ShutdownGunplay()
        {
            Step("VanillaEvents", VanillaEvents.Shutdown);
        }

        /// <summary>ResetAll: the WeaponRanges and VanillaEvents caches (after Aim, as before).</summary>
        static partial void ResetGunplay()
        {
            WeaponRanges.OnSceneLoaded(); VanillaEvents.OnSceneLoaded();
        }

        /// <summary>
        /// Hooks: SetFsmFloat.OnEnter postfix (vanilla hits -> Senses.Hurt; subscribed before DogBite's as before), Raycast.OnEnter prefix
        /// (the player's vanilla shot), SensorGetDetectionRayHit.OnEnter prefix + finalizer (NPC burst ray length, restored afterwards).
        /// </summary>
        partial void InstallHooksGunplay()
        {
            Hooks.SetFsmFloatEnter.Postfix(this, VanillaEvents.AfterDamage);
            Hooks.RaycastEnter.Prefix(this, VanillaEvents.BeforePlayerShot);
            Hooks.SensorRayHitEnter.Prefix(this, VanillaEvents.BeforeNpcShot);
            Hooks.SensorRayHitEnter.Finalizer(this, VanillaEvents.AfterNpcShot);
        }
    }
}
