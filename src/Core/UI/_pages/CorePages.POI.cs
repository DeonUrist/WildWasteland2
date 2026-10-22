using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace WildWasteland.Core.UI
{
    /// <summary>POI rows of the settings window (Wave A split): the "New POI" switch on the WORLD page (the POI module master entry).</summary>
    internal static partial class CorePages
    {
        static ConfigEntry<bool> _poi;

        /// <summary>Finds the POI module master entry once at Register (null when the module is missing).</summary>
        static partial void FindPoiSwitch()
        {
            foreach (var e in ModuleHost.Entries) if (e.Module.Id == "POI") _poi = e.Config.Enabled;
        }

        /// <summary>WORLD row: New POI on/off.</summary>
        static partial void WorldPoi(ToolLayout l)
        {
            if (_poi != null) Switch(l, "New POI", _poi);
        }
    }
}
