using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace WildWasteland.Core.UI
{
    /// <summary>Gunplay rows of the settings window (Wave A split): Toggle Ironsights on the HUD page.</summary>
    internal static partial class CorePages
    {
        /// <summary>HUD page row: Toggle Ironsights (Guns).</summary>
        static partial void HudGunplay(ToolLayout l)
        {
            Switch(l, "Toggle Ironsights", WildWasteland.Modules.Guns.GunsModule.ToggleIronsights);
        }
    }
}
