using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    /// <summary>HUD world-ready bridge for the exploration record embedded in the world save.</summary>
    public sealed partial class CleanHudModule
    {
        /// <summary>Loads the fog of war of the current slot (MapFog.WorldReady).</summary>
        partial void WorldReadySaves() { MapFog.WorldReady(SaveBus.Slot); }
    }
}
