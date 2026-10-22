using System;
using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.HUD
{
    /// <summary>HUD element, world-map and fog settings contributed by their existing owner.</summary>
    public sealed partial class HudModule
    {
        /// <summary>Contributes independent HUD/map rows without Core depending on HUD types.</summary>
        private void CleanSettings(SettingsShell shell)
        {
            shell.Custom(this, SettingsShell.Page.World, "", NavigationSettings, 40);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawDirection, 140);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawNeeds, 190);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawDriving, 230);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawHudTail, 250);
            shell.Custom(this, SettingsShell.Page.Debug, "", DrawFog, 400);
            foreach (SettingsShell.Page page in new[] { SettingsShell.Page.Player, SettingsShell.Page.World, SettingsShell.Page.Hud })
                shell.OnShow(this, page, PreparePictures);
            var driving = ModApi.Get<IDrivingHud>()?.Enabled;
            if (driving != null) shell.Resettable(this, SettingsShell.Page.Hud, driving);
        }
        /// <summary>Prepares character portrait resources owned by this HUD display.</summary>
        private static void PreparePictures() { WildWasteland.Modules.PlayerBody.InventoryModel.PrepareSettingsPictures(); }
        /// <summary>Damage-direction toggle in its original feedback position.</summary>
        private static void DrawDirection(ToolLayout l) { SettingsShell.Switch(l, "Damage direction", DamageDirection); }
        /// <summary>Needs, health/armor numbers and insanity toggles.</summary>
        private static void DrawNeeds(ToolLayout l)
        { SettingsShell.Switch(l, "Needs", ShowNeeds); SettingsShell.Switch(l, "HP and armor numbers", ShowHealthNumbers); SettingsShell.Switch(l, "Insanity", ShowInsanity); }
        /// <summary>The optional driving-HUD mode with its external enable value.</summary>
        private static void DrawDriving(ToolLayout l)
        {
            var driving = ModApi.Get<IDrivingHud>()?.Enabled; if (driving == null) return;
            int mode = !driving.Value ? 0 : DrivingHudOnlyThirdPerson.Value ? 1 : 2;
            int selected = l.Choices("Gear/RPM/Speed HUD", mode, "Off", "3rd person", "On");
            if (selected != mode) { DrivingHudOnlyThirdPerson.Value = selected == 1; driving.Value = selected != 0; }
        }
        /// <summary>Compass, simple artwork and HUD scale rows.</summary>
        private static void DrawHudTail(ToolLayout l)
        { SettingsShell.Switch(l, "Compass", ShowCompass); SettingsShell.Switch(l, "Simple map icons", SimpleMapIcons); l.Entry(Scale); }
        /// <summary>Map-fog fine tuning retained in Debug.</summary>
        private static void DrawFog(ToolLayout l)
        { l.Heading("Map fog of war"); l.Text("Tune unexplored areas on the map. Fog of war on/off is in World."); l.Entry(FogRadius); l.Entry(FogEdge); l.Entry(FogOpacity); }
        /// <summary>WORLD tail: fog of war, compass detail and map marker levels.</summary>
        private static void NavigationSettings(ToolLayout l)
        {
            l.Text("Map and compass world settings");
            SettingsShell.Switch(l, "Fog of war", FogOfWar);
            CompassDetail(l, "Patrols on compass", ShowConvoys, AmbiguousPatrols);
            CompassDetail(l, "Locations on compass (1 km)", ShowLocations, AmbiguousLocations);
            MapMarkers.Value = (MapDetail)l.Choices("Map markers", (int)MapMarkers.Value, "Off", "Ambiguous", "On");
            MapTrafficMarkers.Value = (MapDetail)l.Choices("World traffic markers", (int)MapTrafficMarkers.Value, "Off", "Ambiguous", "On");
            MapRoutes.Value = (MapDetail)l.Choices("Map Routes", (int)MapRoutes.Value, "Off", "Ambiguous", "On");
        }

        /// <summary>Off / Ambiguous / On over a show + ambiguous entry pair.</summary>
        static void CompassDetail(ToolLayout l, string label, ConfigEntry<bool> show, ConfigEntry<bool> ambiguous)
        {
            int current = !show.Value ? 0 : ambiguous.Value ? 1 : 2;
            int next = l.Choices(label, current, "Off", "Ambiguous", "On");
            if (next != current) { ambiguous.Value = next == 1; show.Value = next != 0; }
        }

    }
}
