using WildWasteland.Core.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;
using WildWasteland.Modules.HUD;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Town name labels and icon tint on the CleanHUD map (reflection into MapMarkers entries, GlyphText label, MapMarkers.Tick postfix),
    /// hiding native map icons of town buildings and the starting-camp tent icon (LocationMarkers.Observe patches).
    /// Wave A (A6): partial split of Towns/TownMarkers.cs (HUD part, B10: becomes an IMapMarkers label/colour feature).
    /// </summary>
    internal static partial class TownMarkers
    {
        /// <summary>HUD half of a town marker: the cached MapMarkers entry, its icon root/image and the name label.</summary>
        private sealed partial class Marker
        {
            internal object Entry;
            internal RectTransform Root;
            internal RawImage Image;
            internal GlyphText Label;
        }
        private static FieldInfo _entries, _id, _root, _image;
        private static ConfigEntry<bool> _locations, _compass;
        private static IDictionary _nativeLocations;
        private static FieldInfo _nativeIcon;
        internal static void Install(TownsModule owner)
        {
            var assembly = typeof(Services).Assembly;
            var locations = assembly.GetType("WildWasteland.Modules.HUD.LocationMarkers", true);
            var map = assembly.GetType("WildWasteland.Modules.HUD.MapMarkers", true);
            var entry = map.GetNestedType("Entry", BindingFlags.NonPublic);
            var nativeLocation = locations.GetNestedType("Location", BindingFlags.NonPublic);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            _nativeLocations = (IDictionary)locations.GetField("Locations", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            _nativeIcon = nativeLocation.GetField("Icon", flags);
            _entries = map.GetField("_entries", flags); _id = entry.GetField("Id", flags);
            _root = entry.GetField("Root", flags); _image = entry.GetField("Image", flags);
            var hud = typeof(HudModule);
            _locations = (ConfigEntry<bool>)hud.GetField("ShowLocations", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            _compass = (ConfigEntry<bool>)hud.GetField("ShowCompass", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (!Patches.Patch(owner, Patches.Method(locations, "Observe"), prefix: typeof(TownMarkers).GetMethod("Observe", BindingFlags.Static | BindingFlags.NonPublic),
                postfix: typeof(TownMarkers).GetMethod("StartingCamp", BindingFlags.Static | BindingFlags.NonPublic))
                || !Patches.Patch(owner, Patches.Method(map, "Tick"), postfix: typeof(TownMarkers).GetMethod("Draw", BindingFlags.Static | BindingFlags.NonPublic)))
                throw new InvalidOperationException("Town marker bridge could not be installed.");
        }
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
    }
}
