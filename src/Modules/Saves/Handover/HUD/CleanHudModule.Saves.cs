using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    /// <summary>HANDOVER to HUD (B10; REFACTOR-REQUEST B04-1). Saves part of the CleanHUD entry (Wave A split, B04): fog-of-war sidecar read at world ready.</summary>
    public sealed partial class CleanHudModule
    {
        /// <summary>Loads the fog of war of the current slot (MapFog.WorldReady).</summary>
        partial void WorldReadySaves() { MapFog.WorldReady(SaveBus.Slot); }
    }
}
