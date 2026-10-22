using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>Loads HUD exploration from the selected ES3 world save when native loading completes.</summary>
    public sealed partial class HudModule
    {
        /// <summary>Loads the fog of war of the current slot (MapFog.WorldReady).</summary>
        partial void WorldReadySaves() { MapFog.WorldReady(SaveBus.Slot); }
    }
}
