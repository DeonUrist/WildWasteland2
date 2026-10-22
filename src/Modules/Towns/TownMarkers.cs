using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;
using WildWasteland.Modules.CleanHUD;

namespace WildWasteland.Modules.Towns
{
    internal static class TownMarkers
    {
        private const string Owner = "WildWasteland.Towns";
        private sealed class Marker
        {
            internal TownSite Site;
            internal IMapMarkers Map;
            internal ICompassMarkers Compass;
            internal int MapId, CompassId;
            internal object Entry;
            internal RectTransform Root;
            internal RawImage Image;
            internal GlyphText Label;
            internal bool Named;
            internal string Icon;
            internal Texture2D Texture;
            internal Color Color;
            internal Vector3 Position() { return Site.Anchor.TransformPoint(Site.Plan.WorldCenter); }
        }
        private static readonly HashSet<Transform> Buildings = new HashSet<Transform>();
        private static readonly List<Marker> Markers = new List<Marker>();
        private static FieldInfo _entries, _id, _root, _image;
        private static ConfigEntry<bool> _locations, _compass;
        private static IDictionary _nativeLocations;
        private static FieldInfo _nativeIcon;
        internal static void Install(TownsModule owner)
        {
            var assembly = typeof(Services).Assembly;
            var locations = assembly.GetType("WildWasteland.Modules.CleanHUD.LocationMarkers", true);
            var map = assembly.GetType("WildWasteland.Modules.CleanHUD.QuestMap", true);
            var entry = map.GetNestedType("Entry", BindingFlags.NonPublic);
            var nativeLocation = locations.GetNestedType("Location", BindingFlags.NonPublic);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            _nativeLocations = (IDictionary)locations.GetField("Locations", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            _nativeIcon = nativeLocation.GetField("Icon", flags);
            _entries = map.GetField("_entries", flags); _id = entry.GetField("Id", flags);
            _root = entry.GetField("Root", flags); _image = entry.GetField("Image", flags);
            var hud = typeof(CleanHudModule);
            _locations = (ConfigEntry<bool>)hud.GetField("ShowLocations", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            _compass = (ConfigEntry<bool>)hud.GetField("ShowCompass", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (!Patches.Patch(owner, Patches.Method(locations, "Observe"), prefix: typeof(TownMarkers).GetMethod("Observe", BindingFlags.Static | BindingFlags.NonPublic),
                postfix: typeof(TownMarkers).GetMethod("StartingCamp", BindingFlags.Static | BindingFlags.NonPublic))
                || !Patches.Patch(owner, Patches.Method(map, "Tick"), postfix: typeof(TownMarkers).GetMethod("Draw", BindingFlags.Static | BindingFlags.NonPublic)))
                throw new InvalidOperationException("Town marker bridge could not be installed.");
        }
        internal static void Building(Transform root) { Buildings.Add(root); }
        internal static bool Owns(Transform root) { return root != null && Buildings.Contains(root); }
        private static bool Observe(Transform icon)
        {
            for (var t = icon; t != null; t = t.parent) if (Owns(t)) return false;
            return true;
        }
        private static void StartingCamp(Transform icon)
        {
            var root = icon == null ? null : icon.parent;
            if (root == null || root.name != "Starter_Area") return;
            var location = _nativeLocations[root];
            if (location != null) _nativeIcon.SetValue(location, "tent");
        }
        internal static void Add(TownSite site) { Markers.Add(new Marker { Site = site }); }
        internal static void Refresh()
        {
            foreach (var marker in Markers)
            {
                if (marker.Site.Anchor == null) continue;
                var plan = marker.Site.Plan;
                var player = GameRefs.Player;
                if (GameRefs.InGame && !SaveBus.Busy && !plan.Discovered && player != null)
                    foreach (var building in marker.Site.Buildings)
                    {
                        if (building == null) continue;
                        var d = building.transform.position - player.transform.position; d.y = 0;
                        if (d.sqrMagnitude > 3600) continue;
                        plan.Discovered = true;
                        Log.Always("[Towns] visited " + plan.Name + " (" + plan.Key + ")"); break;
                    }
                if (marker.Map != Services.Map)
                { RemoveMap(marker); marker.Map = Services.Map; }
                bool named = plan.Discovered || marker.Map != null && marker.Map.Markers == MapDetail.On;
                string glyph = named && plan.Condition == TownCondition.Ruined ? "ruins" : "town";
                Color color = named && plan.Owned ? FactionColor(plan.Faction) : Color.white;
                if (marker.Named != named || marker.Color != color || marker.Icon != glyph)
                { RemoveMap(marker); RemoveCompass(marker); marker.Named = named; marker.Color = color; marker.Icon = glyph; marker.Texture = null; }
                if (marker.Map != null && GameRefs.InGame && !SaveBus.Busy)
                    foreach (var building in marker.Site.Buildings)
                        if (building != null) marker.Map.Observe(building.transform, named ? plan.Name : "Town", color);
                if (marker.Map != null && _locations.Value && marker.Map.Markers != MapDetail.Off)
                {
                    if (!marker.Map.Exists(marker.MapId))
                    {
                        marker.MapId = marker.Map.Add(Owner, marker.Position, glyph, color, named ? plan.Name : "Town");
                        // The service does not draw labels. Cache its entry once when registering, then
                        // the Tick postfix uses only typed Unity references.
                        foreach (var entry in (IList)_entries.GetValue(marker.Map))
                            if ((int)_id.GetValue(entry) == marker.MapId) { marker.Entry = entry; break; }
                    }
                    if (marker.Root == null && marker.Entry != null) Attach(marker);
                    if (marker.Texture == null) marker.Texture = marker.Map.GetIcon(glyph);
                    // Observe's result is deliberately ignored: only physically visiting reveals a name.
                }
                else RemoveMap(marker);
                if (marker.Compass != Services.Compass) { RemoveCompass(marker); marker.Compass = Services.Compass; }
                bool near = player != null && (player.transform.position - marker.Position()).sqrMagnitude < 1000000;
                if (marker.Compass != null && _locations.Value && _compass.Value && near)
                {
                    if (!marker.Compass.Exists(marker.CompassId)) marker.CompassId = marker.Compass.Add(Owner, marker.Position, glyph, color);
                }
                else RemoveCompass(marker);
            }
        }
        private static void Attach(Marker marker)
        {
            marker.Root = _root.GetValue(marker.Entry) as RectTransform;
            marker.Image = _image.GetValue(marker.Entry) as RawImage;
            if (marker.Root == null) return;
            var label = new GameObject("Towns name", typeof(RectTransform));
            var rect = (RectTransform)label.transform; rect.SetParent(marker.Root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(360, 22); rect.anchoredPosition = new Vector2(0, -2);
            marker.Label = label.AddComponent<GlyphText>(); marker.Label.Size = 13;
            marker.Label.Align = TextAnchor.MiddleCenter; marker.Label.raycastTarget = false;
            label.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
        }
        private static void Draw()
        {
            foreach (var marker in Markers)
            {
                if (marker.Root == null || marker.Image == null || marker.Map == null || !marker.Map.Open || marker.Site.Anchor == null) continue;
                marker.Root.sizeDelta = new Vector2(36, 36);
                marker.Root.anchoredPosition += new Vector2(-4, 4);
                if (marker.Texture != null) marker.Image.texture = marker.Texture;
                marker.Image.color = marker.Color;
                if (marker.Root.gameObject.activeSelf && (!_locations.Value || marker.Map.Hidden(marker.Position()))) marker.Root.gameObject.SetActive(false);
                marker.Label.Set(marker.Named ? marker.Site.Plan.Name : "");
                marker.Label.color = marker.Color;
            }
        }
        private static Color FactionColor(TownFaction faction)
        {
            switch (faction)
            {
                case TownFaction.Coyotes: return new Color(.31f, .86f, .39f);
                case TownFaction.Scrapyard: return new Color(1f, .31f, .31f);
                case TownFaction.Mutants: return new Color(.76f, .62f, .46f);
                default: return Color.white;
            }
        }
        private static void RemoveMap(Marker marker)
        { if (marker.Map != null) marker.Map.Remove(marker.MapId); marker.MapId = 0; marker.Entry = null; marker.Root = null; marker.Image = null; marker.Label = null; }
        private static void RemoveCompass(Marker marker)
        { if (marker.Compass != null) marker.Compass.Remove(marker.CompassId); marker.CompassId = 0; }
        internal static void Remove(TownSite site)
        {
            for (int i = Markers.Count - 1; i >= 0; i--) if (Markers[i].Site == site)
            { RemoveMap(Markers[i]); RemoveCompass(Markers[i]); Markers.RemoveAt(i); }
            foreach (var building in site.Buildings) if (building != null) Buildings.Remove(building.transform);
        }
    }
}
