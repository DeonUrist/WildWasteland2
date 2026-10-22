namespace WildWasteland.Modules.Guns
{
    /// <summary>Compatibility part of the old Guns entry (Wave A split). Empty since B17 (Apocaspawner/Weapons Pack bridges live in the
    /// Compatibility module); Wave C deletes it.</summary>
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
