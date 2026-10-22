namespace WildWasteland.Modules.Survival
{
    /// <summary>
    /// Compatibility part of the Survival entry (Wave A split). Empty since B17: the single Compatibility Apocaspawner bridge lists the
    /// Stew Pot while Survival runs. Wave C deletes it.
    /// </summary>
    public sealed partial class SurvivalModule
    {
        /// <summary>Empty (B17): the Stew Pot entry is added by the Compatibility Apocaspawner bridge while Survival runs.</summary>
        partial void InitCompatibility() { }

        /// <summary>Empty (B17): the Compatibility Apocaspawner bridge removes the Stew Pot entry once Survival is off.</summary>
        partial void ShutdownCompatibility() { }
    }
}
