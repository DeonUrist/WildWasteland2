namespace WildWasteland.Modules.Guns
{
    /// <summary>Compatibility part of the Guns entry (Wave A split, B17): Apocaspawner damage, Weapons Pack stab sound bridges.</summary>
    public sealed partial class GunsModule
    {
        /// <summary>Empty (B17): the Apocaspawner damage postfix is installed by the Compatibility Apocaspawner bridge.</summary>
        partial void InitCompatibility() { }

        /// <summary>Empty (B17): the Weapons Pack sound part is installed by the Compatibility Weapons Pack bridge.</summary>
        static partial void EnsureCompatibility() { }

        /// <summary>Empty (B17): the Weapons Pack sound part shuts down with the Compatibility bridge.</summary>
        static partial void ShutdownCompatibility() { }
    }
}
