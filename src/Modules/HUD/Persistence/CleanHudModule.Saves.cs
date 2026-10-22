using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>Saves part of the CleanHUD entry (Wave A split, B04): fog-of-war sidecar read at world ready.</summary>
    public sealed partial class HudModule
    {
        /// <summary>Loads the fog of war of the current slot (MapFog.WorldReady).</summary>
        partial void WorldReadySaves() { MapFog.WorldReady(SaveBus.Slot); }
    }
}
