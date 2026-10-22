namespace WildWasteland.Modules.Survival
{
    /// <summary>
    /// Compatibility part of the Survival entry (Wave A split, owner B17): the Apocaspawner catalog bridge that lists the
    /// Stew Pot under Camp &amp; Farming. Wave B merges it with the Patrols SpawnerIntegration into one Apocaspawner bridge.
    /// </summary>
    public sealed partial class SurvivalModule
    {
        /// <summary>Resolves Apocaspawner by reflection and hooks its catalog when the mod is loaded.</summary>
        partial void InitCompatibility()
        {
            SpawnerBridge.Install(this);
        }

        /// <summary>Removes the injected Stew Pot catalog entry and clears the cached reflection handles.</summary>
        partial void ShutdownCompatibility()
        {
            SpawnerBridge.Shutdown();
        }
    }
}
