namespace WildWasteland.Modules.Guns
{
    /// <summary>Compatibility part of the Guns entry (Wave A split, B17): Apocaspawner damage, Weapons Pack stab sound, BloodFX bridges.</summary>
    public sealed partial class GunsModule
    {
        /// <summary>Installs the optional Apocaspawner weapon-damage postfix.</summary>
        partial void InitCompatibility() { ApocaspawnerDamage.Install(this); }

        /// <summary>Lazily installs the Weapons Pack and BloodFX bridges (called from the Bosses job every 0.5 s).</summary>
        static partial void EnsureCompatibility()
        {
            WeaponsPackSound.Ensure();
            BloodFxCompat.Ensure();
        }

        /// <summary>Tears the Weapons Pack and BloodFX bridges down on module shutdown.</summary>
        static partial void ShutdownCompatibility()
        {
            WeaponsPackSound.Shutdown();
            BloodFxCompat.Shutdown();
        }
    }
}
