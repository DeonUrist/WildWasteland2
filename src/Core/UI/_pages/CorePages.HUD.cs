using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using Player = WildWasteland.Modules.PlayerBody.PlayerBodyModule;
using HudSettings = WildWasteland.Modules.HUD.HudModule;
using CleanHudSettings = WildWasteland.Modules.CleanHUD.CleanHudModule;

namespace WildWasteland.Core.UI
{
    /// <summary>HUD rows of the settings window (Wave A split): HUD page, map/compass settings, fog tuning, reset/preview hooks.</summary>
    internal static partial class CorePages
    {
        /// <summary>RESET also restores the optional ChaseCamera driving HUD switch (when ChaseCamera is installed).</summary>
        static partial void AddResetEntriesHud(List<ConfigEntryBase> entries)
        {
            var drivingHud = WildWasteland.Modules.CleanHUD.ChaseCameraHud.HudEnabled;
            if (drivingHud != null) entries.Add(drivingHud);
        }

        /// <summary>Game pages OnShow: body pictures (InventoryModel) and crosshair previews, prepared once per opening.</summary>
        static partial void PrepareHudPreviews()
        {
            WildWasteland.Modules.PlayerBody.InventoryModel.PrepareSettingsPictures();
            WildWasteland.Modules.HUD.CrosshairController.PreparePreview();
        }

        /// <summary>HUD page: crosshair, feedback switches, Clean HUD elements, vehicle hints, compass/map, scale.</summary>
        static void DrawHud(ToolLayout l)
        {
            l.Heading("HUD");
            CrosshairSelection(l);
            Switch(l, "Grabbing dot", HudSettings.SelectionDot);
            bool damage = HudSettings.HudEnabled.Value && HudSettings.DamageNumbers.Value == 2;
            bool show = l.Choices("Floating damage", damage ? 1 : 0, "Off", "On") == 1;
            if (show != damage) { HudSettings.DamageNumbers.Value = show ? 2 : 0; if (show) HudSettings.HudEnabled.Value = true; }
            FeedbackSwitch(l, "Hit markers", HudSettings.HitMarker);
            Switch(l, "Damage direction", CleanHudSettings.DamageDirection);
            FeedbackSwitch(l, "Headshot sound", HudSettings.HeadshotSound);
            HudGunplay(l);
            HudNpc(l);
            if (!ModuleHost.IsRunning(HudSettings.Instance)) l.Text("Enable Combat HUD in Modules to use these HUD settings.");
            Switch(l, "Needs", CleanHudSettings.ShowNeeds);
            Switch(l, "HP and armor numbers", CleanHudSettings.ShowHealthNumbers);
            Switch(l, "Insanity", CleanHudSettings.ShowInsanity);
            Switch(l, "Vehicle Hints", Player.VehicleHotkeyHint);
            var drivingHud = WildWasteland.Modules.CleanHUD.ChaseCameraHud.HudEnabled;
            if (drivingHud != null)
            {
                int mode = !drivingHud.Value ? 0 : CleanHudSettings.DrivingHudOnlyThirdPerson.Value ? 1 : 2;
                int selected = l.Choices("Gear/RPM/Speed HUD", mode, "Off", "3rd person", "On");
                if (selected != mode) { CleanHudSettings.DrivingHudOnlyThirdPerson.Value = selected == 1; drivingHud.Value = selected != 0; }
            }
            Switch(l, "Vehicle Icons", Player.VehicleStatusHint);
            Switch(l, "Compass", CleanHudSettings.ShowCompass);
            Switch(l, "Simple map icons", CleanHudSettings.SimpleMapIcons);
            l.Entry(CleanHudSettings.Scale);
        }

        /// <summary>WORLD tail: fog of war, compass detail and map marker levels.</summary>
        static partial void NavigationSettings(ToolLayout l)
        {
            l.Text("Map and compass world settings");
            Switch(l, "Fog of war", CleanHudSettings.FogOfWar);
            CompassDetail(l, "Patrols on compass", CleanHudSettings.ShowConvoys, CleanHudSettings.AmbiguousPatrols);
            CompassDetail(l, "Locations on compass (1 km)", CleanHudSettings.ShowLocations, CleanHudSettings.AmbiguousLocations);
            CleanHudSettings.MapMarkers.Value = (MapDetail)l.Choices("Map markers", (int)CleanHudSettings.MapMarkers.Value, "Off", "Ambiguous", "On");
            CleanHudSettings.MapTrafficMarkers.Value = (MapDetail)l.Choices("World traffic markers", (int)CleanHudSettings.MapTrafficMarkers.Value, "Off", "Ambiguous", "On");
            CleanHudSettings.MapRoutes.Value = (MapDetail)l.Choices("Map Routes", (int)CleanHudSettings.MapRoutes.Value, "Off", "Ambiguous", "On");
        }

        /// <summary>Off / Ambiguous / On over a show + ambiguous entry pair.</summary>
        static void CompassDetail(ToolLayout l, string label, ConfigEntry<bool> show, ConfigEntry<bool> ambiguous)
        {
            int current = !show.Value ? 0 : ambiguous.Value ? 1 : 2;
            int next = l.Choices(label, current, "Off", "Ambiguous", "On");
            if (next != current) { ambiguous.Value = next == 1; show.Value = next != 0; }
        }

        /// <summary>Off/On switch that is only "on" while the combat HUD is enabled; turning it on enables the HUD.</summary>
        static void FeedbackSwitch(ToolLayout l, string label, ConfigEntry<bool> entry)
        {
            bool value = HudSettings.HudEnabled.Value && entry.Value;
            bool next = l.Choices(label, value ? 1 : 0, "Off", "On") == 1;
            if (next != value) { entry.Value = next; if (next) HudSettings.HudEnabled.Value = true; }
        }

        /// <summary>Crosshair choice cards with live previews.</summary>
        static readonly string[] _crosshairs = { "None", "Vanilla", "Lines", "Dot" };
        /// <summary>Crosshair choice cards (None / Vanilla / Lines / Dot) with previews from the HUD.</summary>
        static void CrosshairSelection(ToolLayout l)
        {
            l.Text("Crosshair");
            var r = l.Next(74); float width = (r.width - 24) / 4;
            for (int i = 0; i < _crosshairs.Length; i++)
            {
                var card = new Rect(r.x + i * (width + 8), r.y, width, r.height);
                bool selected = HudSettings.Crosshair.Value == i;
                Ledger.Panel(card, selected ? Ledger.Selected : Ledger.Field, selected ? Ledger.Accent : Ledger.Line);
                float previewSize = i == 3 ? 27 : 36;
                WildWasteland.Modules.HUD.CrosshairController.DrawPreview(new Rect(card.center.x - previewSize / 2, card.y + 23 - previewSize / 2, previewSize, previewSize), i);
                Ledger.Label(new Rect(card.x, card.yMax - 29, card.width, 26), _crosshairs[i], Ledger.Body, selected ? Ledger.Accent : Ledger.Text, TextAnchor.MiddleCenter);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none) && !selected) HudSettings.Crosshair.Value = i;
            }
        }

        /// <summary>Map fog entries (shown in the CleanHUD Debug category, not as switches).</summary>
        static bool MapFogSetting(ConfigEntryBase e)
        { return ReferenceEquals(e, CleanHudSettings.FogOfWar) || ReferenceEquals(e, CleanHudSettings.FogRadius) || ReferenceEquals(e, CleanHudSettings.FogEdge) || ReferenceEquals(e, CleanHudSettings.FogOpacity); }

        /// <summary>Debug page, CleanHUD category: map fog tuning.</summary>
        static partial void DebugSectionHud(ToolLayout l, IModule owner)
        {
            if (owner != null && owner.Id == "CleanHUD")
            {
                l.Heading("Map fog of war");
                l.Text("Tune unexplored areas on the map. Fog of war on/off is in World.");
                l.Entry(CleanHudSettings.FogRadius);
                l.Entry(CleanHudSettings.FogEdge); l.Entry(CleanHudSettings.FogOpacity);
            }
        }
    }
}
