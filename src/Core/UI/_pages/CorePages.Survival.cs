using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace WildWasteland.Core.UI
{
    /// <summary>Survival (Weather) rows of the settings window (Wave A split): storm map areas on WORLD, storm tuning on Debug.</summary>
    internal static partial class CorePages
    {
        /// <summary>WORLD row: weather storm areas on the map (through Services.Weather).</summary>
        static partial void WorldWeather(ToolLayout l)
        {
            var weather = Services.Weather;
            if (weather != null)
            {
                weather.MapAreas = l.Choices("Weather storm areas on map", weather.MapAreas ? 1 : 0, "Off", "On") == 1;
            }
        }

        /// <summary>The three player-facing storm density/size entries shown under Debug (not as switches).</summary>
        static bool WeatherTuning(ConfigEntryBase e)
        { return e.Definition.Section == "Weather / Storms" && (e.Definition.Key == "Hurricanes per km²" || e.Definition.Key == "Front width (km)" || e.Definition.Key == "Depth (km)"); }

        /// <summary>Debug page, Weather category: storm tuning entries.</summary>
        static partial void DebugSectionSurvival(ToolLayout l, IModule owner)
        {
            if (owner != null && owner.Id == "Weather")
            {
                l.Heading("Storm tuning");
                foreach (var key in Plugin.Settings.Keys) if (WeatherTuning(Plugin.Settings[key])) l.Entry(Plugin.Settings[key]);
            }
        }
    }
}
