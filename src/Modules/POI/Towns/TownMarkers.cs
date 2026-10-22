using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    /// <summary>
    /// Town map/compass markers and discovery (visited within 60 m), town building registry (Owns, used by Patrols and population).
    /// Wave A (A6): POI part of Towns/TownMarkers.cs; CleanHUD label/icon patches in HUD/_fromTowns/TownMarkers.Labels.cs.
    /// </summary>
    internal static partial class TownMarkers
    {
        private const string Owner = "WildWasteland.Towns";
        private sealed partial class Marker
        {
            internal TownSite Site;
            internal IMapMarkers Map;
            internal ICompassMarkers Compass;
            internal int MapId, CompassId;
            internal bool Named;
            internal string Icon;
            internal Color Color;
            /// <summary>Returns the live marker scene position.</summary>
            internal Vector3 Position() { return Site.Anchor.TransformPoint(Site.Plan.WorldCenter); }
        }
        private static readonly HashSet<Transform> Buildings = new HashSet<Transform>();
        private static readonly List<Marker> Markers = new List<Marker>();
        /// <summary>Registers a town-owned root and excludes its automatic native location marker.</summary>
        internal static void Building(Transform root) { Buildings.Add(root); var locations = ModApi.Get<ILocationMarkers>(); if (locations != null) locations.Exclude(root, true); }
        /// <summary>Tests exact registration of town-owned building geometry.</summary>
        internal static bool Owns(Transform root) { return root != null && Buildings.Contains(root); }
        /// <summary>Adds the captured item to the owning plan or live registry.</summary>
        internal static void Add(TownSite site) { Markers.Add(new Marker { Site = site }); }
        /// <summary>Refreshes live state within the existing bounded update cadence.</summary>
        internal static void Refresh()
        {
            var visibility=ModApi.Get<ILocationVisibility>();
            foreach (var marker in Markers)
            {
                if (marker.Site.Anchor == null) continue;
                var locations=ModApi.Get<ILocationMarkers>();if(locations!=null)foreach(var building in marker.Site.Buildings)if(building!=null)locations.Exclude(building.transform,true);
                var plan = marker.Site.Plan;
                var player = GameRefs.Player;
                if (GameRefs.InGame && !SaveBus.Busy && !plan.Discovered && player != null)
                    foreach (var building in marker.Site.Buildings)
                    {
                        if (building == null) continue;
                        var d = building.transform.position - player.transform.position; d.y = 0;
                        if (d.sqrMagnitude > 3600) continue;
                        plan.Discovered = true;
                        Log.Main(null, "[POI] visited " + plan.Name + " (" + plan.Key + ")"); break;
                    }
                if (marker.Map != ModApi.Get<IMapMarkers>())
                { RemoveMap(marker); marker.Map = ModApi.Get<IMapMarkers>(); }
                bool named = plan.Discovered || marker.Map != null && marker.Map.Markers == MapDetail.On;
                string glyph = named && plan.Condition == TownCondition.Ruined ? "ruins" : "town";
                Color color = named && plan.Owned ? FactionColor(plan.Faction) : Color.white;
                if (marker.Named != named || marker.Color != color || marker.Icon != glyph)
                { RemoveMap(marker); RemoveCompass(marker); marker.Named = named; marker.Color = color; marker.Icon = glyph; }
                if (marker.Map != null && GameRefs.InGame && !SaveBus.Busy)
                    foreach (var building in marker.Site.Buildings)
                        if (building != null) marker.Map.Observe(building.transform, named ? plan.Name : "Town", color);
                if (marker.Map != null && (visibility==null||visibility.Locations) && marker.Map.Markers != MapDetail.Off)
                {
                    if (!marker.Map.Exists(marker.MapId))
                    {
                        marker.MapId = marker.Map.Add(Owner, marker.Position, glyph, color, named ? plan.Name : "Town");
                        var style = ModApi.Get<IMapMarkerStyle>();
                        if (style != null) { style.SetLabel(marker.MapId, named ? plan.Name : "Town"); style.SetColor(marker.MapId, color); style.SetIcon(marker.MapId, glyph); }
                    }
                    // Observe's result is deliberately ignored: only physically visiting reveals a name.
                }
                else RemoveMap(marker);
                if (marker.Compass != ModApi.Get<ICompassMarkers>()) { RemoveCompass(marker); marker.Compass = ModApi.Get<ICompassMarkers>(); }
                bool near = player != null && (player.transform.position - marker.Position()).sqrMagnitude < 1000000;
                if (marker.Compass != null && (visibility==null||visibility.Locations&&visibility.Compass) && near)
                {
                    if (!marker.Compass.Exists(marker.CompassId)) marker.CompassId = marker.Compass.Add(Owner, marker.Position, glyph, color);
                }
                else RemoveCompass(marker);
            }
        }
        /// <summary>Maps the committed faction to its presentation color.</summary>
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
        /// <summary>Removes the owned map marker and clears its handle.</summary>
        private static void RemoveMap(Marker marker)
        { if (marker.Map != null) marker.Map.Remove(marker.MapId); marker.MapId = 0; }
        /// <summary>Removes the owned compass marker and clears its handle.</summary>
        private static void RemoveCompass(Marker marker)
        { if (marker.Compass != null) marker.Compass.Remove(marker.CompassId); marker.CompassId = 0; }
        /// <summary>Removes the matching registration and releases its live presentation state.</summary>
        internal static void Remove(TownSite site)
        {
            for (int i = Markers.Count - 1; i >= 0; i--) if (Markers[i].Site == site)
            { RemoveMap(Markers[i]); RemoveCompass(Markers[i]); Markers.RemoveAt(i); }
            foreach (var building in site.Buildings) if (building != null) { Buildings.Remove(building.transform); var locations = ModApi.Get<ILocationMarkers>(); if (locations != null) locations.Exclude(building.transform, false); }
        }
    }
}
