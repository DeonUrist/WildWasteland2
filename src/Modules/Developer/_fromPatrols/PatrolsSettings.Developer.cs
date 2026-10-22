using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrols settings of the numpad editors (turret spots, bus passenger pose, merchant items) and their keys; Wave A split of PatrolsSettings.cs.</summary>
    public sealed partial class PatrolsModule
    {
        internal static ConfigEntry<Key> BusPassengerPoseEditorKey, MerchantEditorKey;
        internal static ConfigEntry<bool> TurretSpotEditor;
        internal static ConfigEntry<bool> BusPassengerPoseEditorEnabled, MerchantEditorEnabled;

        /// <summary>Binds the [Keys] entries of the bus passenger pose editor and the merchant item editor.</summary>
        partial void BindDeveloperKeys(ModuleConfig cfg)
        {
            BusPassengerPoseEditorKey = cfg.Hotkey("Bus passenger pose editor", Key.None, "Patrols: toggle the bus passenger pose editor (also F7 > Seat & turret poses). None = unbound");
            MerchantEditorKey = cfg.Hotkey("Merchant item editor", Key.None, "Patrols: toggle the merchant item placement editor (also F7 > Merchants). None = unbound");
        }

        /// <summary>Binds the tools.cfg Debug switches of the turret spot, bus passenger pose and merchant item editors.</summary>
        partial void BindDeveloperDebug(ModuleConfig cfg)
        {
            // debug switches and editors
            TurretSpotEditor = cfg.Tool(TDebug, "TurretSpotEditor", false,
                "Glowing markers on the human turret spots of the nearest raider car and numpad keys to move them: 8/2 forward/back, 4/6 left/right, " +
                "7/9 down/up (Shift = x5), 0 = next turret, 5 = save the spot into the car's template file, . = markers on/off");
            BusPassengerPoseEditorEnabled = cfg.Tool(TDebug, "Bus passenger pose editor", false, "Live arm, leg and seat adjustment for the nearest bus passenger. Num0 selects joint, Num/ selects side, Num8/2 bends, Num4/6 swings sideways, Num7/9 twists, Num5 saves. Disable other numpad editors while using this tool.");
            MerchantEditorEnabled = cfg.Tool(TDebug, "Merchant item editor", false, "The in-car merchant object editor (numpad). Disable TurretSpotEditor while using it.");
        }
    }
}
