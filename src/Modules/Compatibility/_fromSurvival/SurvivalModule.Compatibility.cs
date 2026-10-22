namespace WildWasteland.Modules.Survival
{
    /// <summary>
    /// Compatibility part of the Survival entry (Wave A split, owner B17): the Apocaspawner catalog bridge that lists the
    /// Stew Pot under Camp &amp; Farming. Wave B merges it with the Patrols SpawnerIntegration into one Apocaspawner bridge.
    /// </summary>
    public sealed partial class SurvivalModule
    {
        /// <summary>Empty (B17): the Stew Pot entry is added by the Compatibility Apocaspawner bridge while Survival runs.</summary>
        partial void InitCompatibility() { }

        /// <summary>Empty (B17): the Compatibility Apocaspawner bridge removes the Stew Pot entry once Survival is off.</summary>
        partial void ShutdownCompatibility() { }
    }
}
