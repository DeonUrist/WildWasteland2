using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Towns editor/test settings: POI dresser key, gas-station test pad, gas-station scale, Camp_10 test spawn.
    /// Wave A (A6): partial split of Towns/TownSettings.cs (Tools part, B16). Same sections/keys/defaults as before.
    /// </summary>
    internal sealed partial class TownSettings
    {
        internal ConfigEntry<bool> TestGasStation, TestCamp10;
        internal ConfigEntry<float> GasStationScale;
        internal ConfigEntry<UnityEngine.InputSystem.Key> PoiDresserKey;
        /// <summary>Binds the POI dresser key and the gas-station / Camp_10 test-pad entries (first four Towns binds, original order).</summary>
        partial void BindTools(ConfigFile file)
        {
            PoiDresserKey = file.Bind("Towns / Editors", "POI dresser", UnityEngine.InputSystem.Key.None, "Open the static prop dresser for the nearest custom POI within 200 metres. Requires Enable Debug and Developer mode. Layouts are saved with Towns assets and reused by every instance of that POI type.");
            TestCamp10 = file.Bind("Towns / Debug", "Test Camp_10 spawn", false, "Place the merchant camp (Camp_10, stripped of its spawners and logic) on a level pad near the starting camp, so its NPC navigation can be baked on flat ground (towns need that bake to use it as their merchant lot). Test only, unsaved. Reload the world after enabling or disabling.");
            TestGasStation = file.Bind("Towns / Debug", "Test gas station", false, "Place the supplied gas station as the closest available POI outside the starting camp, on a clear level pad, with three melee Scrapyard raiders after its ground-floor navigation is baked. Test only; excluded from normal town lots and saves. Reload the world after enabling or disabling.");
            GasStationScale = file.Bind("Towns / Debug", "Gas station scale", 1f, new ConfigDescription("Uniform extra scale for the render model and separate gasstation_collider.glb. Use 1 with the supplied models (their authored 1.5 scale gives a 32.19 x 34.28 metre footprint). Reload after changing: the terrain pad and navigation cache must match the scale.", new AcceptableValueRange<float>(.1f, 5f)));
        }
    }
}
