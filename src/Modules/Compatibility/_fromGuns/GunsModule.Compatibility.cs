namespace WildWasteland.Modules.Guns
{
    /// <summary>Compatibility part of the Guns entry (Wave A split, B17): Apocaspawner damage, Weapons Pack stab sound bridges.</summary>
    public sealed partial class GunsModule
    {
        /// <summary>Installs the optional Apocaspawner weapon-damage postfix.</summary>
        partial void InitCompatibility() { ApocaspawnerDamage.Install(this); }

        /// <summary>Empty (B17): the Weapons Pack sound part is installed by the Compatibility Weapons Pack bridge.</summary>
        static partial void EnsureCompatibility() { }

        /// <summary>Empty (B17): the Weapons Pack sound part shuts down with the Compatibility bridge.</summary>
        static partial void ShutdownCompatibility() { }
    }
}
