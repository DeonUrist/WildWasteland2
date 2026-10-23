using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.Survival
{
    /// <summary>Survival-owned storm settings shared with the world and Debug pages.</summary>
    public sealed partial class SurvivalModule
    {
        /// <summary>Registers storm controls without Core referring to the weather implementation.</summary>
        public override void Settings(SettingsShell shell)
        {
            shell.Custom(this, SettingsShell.Page.World, "", DrawStormMap, 30);
            shell.Custom(this, SettingsShell.Page.Debug, "Tuning", DrawStormTuning, 410);
        }
        /// <summary>Draws the available weather provider's map-area control.</summary>
        private static void DrawStormMap(ToolLayout layout)
        {
            var weather = ModApi.Get<IWeatherRegions>();
            if (weather != null)
            {
                weather.MapAreas = layout.Choices("Weather storm areas on map", weather.MapAreas ? 1 : 0, "Off", "On") == 1;
                layout.Hint("Displays the areas of sandstorms on map");
            }
        }
        /// <summary>Shows existing storm density and front-size settings with their original descriptions and ranges.</summary>
        private static void DrawStormTuning(ToolLayout layout)
        {
            layout.Heading("Storm tuning");
            foreach (var key in Plugin.Settings.Keys)
                if (key.Section == "Survival / Storms" && (key.Key == "Hurricanes per km²" || key.Key == "Front width (km)" || key.Key == "Depth (km)")) layout.Entry(Plugin.Settings[key]);
        }
    }
}
