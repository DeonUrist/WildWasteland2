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
            shell.Custom(this, SettingsShell.Page.World, "", NavigationSettings, 201);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawDirection, 120, 1);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawAmmo, 140, 0);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawHealthNumbers, 140, 1);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawNeeds, 150, 0);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawInsanity, 150, 1);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawCompass, 160, 0);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawSimpleIcons, 160, 1);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawVehicleHints, 190, 0);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawVehicleIcons, 190, 1);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawDriving, 200);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawHudTail, 250);
            shell.Custom(this, SettingsShell.Page.Debug, "Tuning", DrawFog, 400);
            foreach (SettingsShell.Page page in new[] { SettingsShell.Page.Player, SettingsShell.Page.World, SettingsShell.Page.Hud })
                shell.OnShow(this, page, PreparePictures);
            var driving = ModApi.Get<IDrivingHud>()?.Enabled;
            if (driving != null) shell.Resettable(this, SettingsShell.Page.Hud, driving);
        }
        /// <summary>Prepares character portrait resources owned by this HUD display.</summary>
        private static void PreparePictures() { InventoryModel.PrepareSettingsPictures(); }
        /// <summary>Damage-direction toggle in its original feedback position.</summary>
        private static void DrawDirection(ToolLayout l) { SettingsShell.Switch(l, "Damage direction", DamageDirection, "Show a red arrow pointing toward an attacker after you are hit"); }
        /// <summary>Needs, health/armor numbers and insanity toggles.</summary>
        private static void DrawNeeds(ToolLayout l)
        { SettingsShell.Switch(l, "Display needs", ShowNeeds, "Show water, food and sleep beneath HP and armor"); }
        /// <summary>HUD ammunition count visibility.</summary>
        private static void DrawAmmo(ToolLayout l) { SettingsShell.Switch(l, "Display ammo", ShowAmmo); }
        /// <summary>HUD numerical health and armor values.</summary>
        private static void DrawHealthNumbers(ToolLayout l) { SettingsShell.Switch(l, "Display HP/armor", ShowHealthNumbers, "Show HP and armor numbers inside their bars"); }
        /// <summary>HUD insanity meter visibility.</summary>
        private static void DrawInsanity(ToolLayout l) { SettingsShell.Switch(l, "Display insanity", ShowInsanity, "Show the insanity meter while insanity is above zero"); }
        /// <summary>HUD compass strip and marker visibility.</summary>
        private static void DrawCompass(ToolLayout l) { SettingsShell.Switch(l, "Compass", ShowCompass, "Show the direction strip and nearby compass markers"); }
        /// <summary>Shared map and compass location artwork style.</summary>
        private static void DrawSimpleIcons(ToolLayout l) { SettingsShell.Switch(l, "Simple map icons", SimpleMapIcons, "Use generic location icons instead of location-specific pictures on the map and compass"); }
        /// <summary>Retains cockpit key hints at their original settings position.</summary>
        private static void DrawVehicleHints(ToolLayout l) { SettingsShell.Switch(l, "Vehicle Hints", VehicleHotkeyHint); }
        /// <summary>Retains native cockpit status icon visibility at its original settings position.</summary>
        private static void DrawVehicleIcons(ToolLayout l) { SettingsShell.Switch(l, "Vehicle Icons", VehicleStatusHint); }
        /// <summary>The optional driving-HUD mode with its external enable value.</summary>
        private static void DrawDriving(ToolLayout l)
        {
            var driving = ModApi.Get<IDrivingHud>()?.Enabled;
            if (driving == null) { l.Text("Gear/RPM/Speed HUD"); l.Hint("Requires ApocaChaseCamera with its Compatibility setting enabled and the driving HUD bridge ready"); return; }
            int mode = !driving.Value ? 0 : DrivingHudOnlyThirdPerson.Value ? 1 : 2;
            int selected = l.Choices("Gear/RPM/Speed HUD", mode, "Off", "3rd person", "On");
            if (selected != mode) { DrivingHudOnlyThirdPerson.Value = selected == 1; driving.Value = selected != 0; }
            l.Hint("Show gear, engine RPM and speed while driving: off, third-person view only, or both camera views");
        }
        /// <summary>Compass, simple artwork and HUD scale rows.</summary>
        private static void DrawHudTail(ToolLayout l)
        {
            l.Entry(Scale);
            SettingsShell.Switch(l, "Display recommended mods", Plugin.Settings[new ConfigDefinition("Debug", "Recommended mods")] as ConfigEntry<bool>, "Show missing optional mod recommendations at the title menu");
        }
        /// <summary>Map-fog fine tuning retained in Debug.</summary>
        private static void DrawFog(ToolLayout l)
        { l.Heading("Map fog of war"); l.Text("Tune unexplored areas on the map. Fog of war on/off is in World."); l.Entry(FogRadius); l.Entry(FogEdge); l.Entry(FogOpacity); }
        /// <summary>WORLD tail: fog of war, compass detail and map marker levels.</summary>
        private static void NavigationSettings(ToolLayout l)
        {
            SettingsShell.Switch(l, "Fog of war", FogOfWar, "Hides the unvisited sections of the map");
            int patrols = CompassDetail(l, "Patrols on compass", ShowConvoys, AmbiguousPatrols);
            l.Hint(patrols == 0 ? "Patrols are not displayed on compass" : patrols == 1 ? "Patrols are visible on compass without showing their allegiance" : "Patrols are displayed and color coded");
            int locations = CompassDetail(l, "Locations on compass (1 km)", ShowLocations, AmbiguousLocations);
            l.Hint(locations == 0 ? "Compass doesn't display nearby locations" : locations == 1 ? "Locations are shown as a generic icon until visited" : "Locations are shown with their icon and allegiance color");
            MapMarkers.Value = (MapDetail)l.Choices("Map markers", (int)MapMarkers.Value, "Off", "Immersive", "On");
            l.Hint(MapMarkers.Value == MapDetail.Off ? "Map doesn't display locations" : MapMarkers.Value == MapDetail.Ambiguous ? "Locations are shown as a generic icon until visited" : "Locations are shown with their icon and allegiance color");
            MapTrafficMarkers.Value = (MapDetail)l.Choices("World traffic markers", (int)MapTrafficMarkers.Value, "Off", "Immersive", "On");
            l.Hint(MapTrafficMarkers.Value == MapDetail.Off ? "Foot and car patrols are not shown on map" : MapTrafficMarkers.Value == MapDetail.Ambiguous ? "Foot and car patrols are shown without revealing their type" : "Foot and car patrols are shown on map");
            MapRoutes.Value = (MapDetail)l.Choices("Map routes", (int)MapRoutes.Value, "Off", "Immersive", "On");
            l.Hint(MapRoutes.Value == MapDetail.Off ? "Vehicle map routes are not shown on map" : MapRoutes.Value == MapDetail.Ambiguous ? "Vehicle map routes are shown without revealing their color" : "Vehicle map routes are color coded");
        }

        /// <summary>Off / Ambiguous / On over a show + ambiguous entry pair.</summary>
        static int CompassDetail(ToolLayout l, string label, ConfigEntry<bool> show, ConfigEntry<bool> ambiguous)
        {
            int current = !show.Value ? 0 : ambiguous.Value ? 1 : 2;
            int next = l.Choices(label, current, "Off", "Immersive", "On");
            if (next != current) { ambiguous.Value = next == 1; show.Value = next != 0; }
            return next;
        }

    }
}
