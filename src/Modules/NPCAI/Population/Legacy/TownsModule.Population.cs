namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Former NPCAI/Population part of the old Towns entry (Wave A split). Empty since B07: NPCAIModule installs the interior / native-POI
    /// population rules (hooks and jobs owned by NPCAI) and releases deferred native spawn events on its shutdown. Wave C deletes it.
    /// </summary>
    public sealed partial class TownsModule
    {
        /// <summary>Empty (B07): NPCAIModule.Init installs InteriorPopulation.</summary>
        partial void InitPopulation() { }
        /// <summary>Empty (B07): NPCAIModule.Shutdown releases deferred native spawn events.</summary>
        partial void ShutdownPopulation() { }
    }
}
