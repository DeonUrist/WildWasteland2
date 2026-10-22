namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// Former Gunplay part of the NPC entry (Wave A split). B09 moved the VanillaEvents hooks, the WeaponRanges.Sweep job and the cache
    /// resets into GunplayModule; the partial methods stay empty until Wave C deletes the NPC entry.
    /// </summary>
    public sealed partial class NpcModule
    {
        /// <summary>Empty: the "Gunplay.WeaponRanges.Sweep" job is registered by GunplayModule.</summary>
        partial void InitGunplayJobs() { }

        /// <summary>Empty: VanillaEvents.Shutdown runs in GunplayModule.Shutdown.</summary>
        static partial void ShutdownGunplay() { }

        /// <summary>Empty: WeaponRanges/VanillaEvents caches are reset by GunplayModule.</summary>
        static partial void ResetGunplay() { }

        /// <summary>Empty: the VanillaEvents hooks are subscribed by GunplayModule.Init.</summary>
        partial void InstallHooksGunplay() { }
    }
}
