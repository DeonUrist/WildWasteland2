using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// The Vistas World Gen bridge (scope Compatibility.VistasWorldGen): installs <see cref="VistasWorldGen"/> (world-gen hooks, road
    /// capture, terrain-edit events into WorldTiles and the Core TerrainEdited event), provides <see cref="IWorldGenFacts"/> and the HUD
    /// engine-heat notices (<see cref="VistasEngineHud"/>).
    /// </summary>
    internal static class VistasBridge
    {
        /// <summary>Installs the hooks (throws when a required hook fails; the bridge then stops) and provides the facts query.</summary>
        internal static void Start(IModule scope)
        {
            VistasWorldGen.Init(scope);
            if (!VistasWorldGen.Present) return;
            ModApi.Provide<IWorldGenFacts>(scope, new VistasFacts());
            ModApi.Provide<IWorldGenRoads>(scope, new VistasRoads(BepInEx.Bootstrap.Chainloader.PluginInfos["apocalypter.worldamp"].Instance.GetType().Assembly));
            ModApi.Provide<IPlaceBanners>(scope, new PlaceBanners());
            VistasEngineHud.Install(scope);
        }

        /// <summary>Detaches config/terrain callbacks and clears captured state (the scope removes the patches and the provider).</summary>
        internal static void Shutdown() { VistasEngineHud.Clear(); VistasWorldGen.Shutdown(); }
    }
}
