using System;
using System.Collections.Generic;
using System.Globalization;
using Den.Tools;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    internal static class LocationMarkers
    {
        internal const string Owner = "WildWasteland.Locations";
        private sealed class Location
        {
            internal int Marker, MapMarker; internal Transform Root; internal Renderer Native; internal bool ForceOff;
            internal string Icon, GenericIcon, Label, Key, Caption; internal Color Color; internal IMapMarkers Map; internal bool Known, Cleared;
            /// <summary>Resolves position for the location discovery markers.</summary>
            internal Vector3 Position() { return Root != null ? Root.position : Vector3.zero; }
        }
        private static readonly HashSet<Transform> Excluded = new HashSet<Transform>();
        private static readonly Dictionary<Transform, string> Icons = new Dictionary<Transform, string>();
        private static readonly Dictionary<Transform, string> Labels = new Dictionary<Transform, string>();
        /// <summary>Excludes a structure root and removes already registered automatic markers.</summary>
        internal static void Exclude(Transform root, bool excluded)
        {
            if (root == null) return;
            if (excluded) Excluded.Add(root); else Excluded.Remove(root);
            foreach (var pair in Locations)
            {
                if (!ExcludedRoot(pair.Key)) continue;
                var location = pair.Value;
                if (location.Map != null) location.Map.Remove(location.MapMarker); location.MapMarker = 0;
                if (location.Marker != 0) Api.Compass.Remove(location.Marker); location.Marker = 0;
            }
        }
        /// <summary>Returns whether an automatic location belongs to an excluded root.</summary>
        static bool ExcludedRoot(Transform root) { for (var t = root; t != null; t = t.parent) if (Excluded.Contains(t)) return true; return false; }
        /// <summary>Sets a location's artwork override and refreshes its presentation.</summary>
        internal static void SetIcon(Transform root, string icon) { if (root == null) return; if (icon == null) Icons.Remove(root); else Icons[root] = icon; RefreshIcons(); }
        /// <summary>Sets a location's display caption without changing its saved discovery identity.</summary>
        internal static void SetLabel(Transform root, string label) { if (root == null) return; if (label == null) Labels.Remove(root); else Labels[root] = label; Location location; if (Locations.TryGetValue(root, out location)) location.Caption = label ?? CaptionName(location.Label); }
        /// <summary>True when the persisted identity has been discovered.</summary>
        internal static bool Known(string key) { return key != null && Discovered.Contains(key); }
        private static readonly Dictionary<Transform, Location> Locations = new Dictionary<Transform, Location>();
        private static readonly List<Transform> Dead = new List<Transform>();
        private static readonly HashSet<string> Discovered = new HashSet<string>(StringComparer.Ordinal);
        private static readonly SaveKey DiscoveriesSave = SaveBus.Key("WildWasteland.HUD.Locations.v1");
        private static string _readError;
        private static bool _loaded, _fresh;
        private static string _slot;
        private static int _seed;

        /// <summary>Resolves known for the location discovery markers.</summary>
        internal static bool Known(Transform root) { Location location; return root != null && Locations.TryGetValue(root, out location) && location.Known; }
        /// <summary>Resolves known for the location discovery markers.</summary>
        internal static bool Known(Func<Vector3> position) { var location = position.Target as Location; return location != null && location.Known; }
        /// <summary>Resolves custom icon for the location discovery markers.</summary>
        internal static bool CustomIcon(Func<Vector3> position) { var location = position.Target as Location; return location != null && location.Icon == location.Label; }
        /// <summary>Resolves custom icon for the location discovery markers.</summary>
        internal static bool CustomIcon(Transform root) { Location location; return root != null && Locations.TryGetValue(root, out location) && location.Icon == location.Label; }
        /// <summary>Queries the typed navigation source for a location whose bake is missing.</summary>
        internal static bool MissingBake(Func<Vector3> position)
        {
            var location = position.Target as Location;
            return location != null && Services.Npc != null && Services.Npc.HighlightMissingPoiBake(location.Label);
        }
        /// <summary>Resets discovery state and records whether this is a fresh world.</summary>
        internal static void Begin(bool fresh)
        {
            Clear(); _readError = null; _loaded = false; _fresh = fresh; Discovered.Clear();
        }
        /// <summary>Resets discovery for a fresh seed or captures it when native serialization begins.</summary>
        private static void StateChanged(string previous, string now) { if (now == "setSeed") Begin(true); else if (now == "SaveGame") SaveEnded(SaveBus.Slot); }
        /// <summary>Reads the native world seed, returning an unknown sentinel when unavailable.</summary>
        private static int Seed() { var fsm = GameRefs.SaveLoadGame; var seed = fsm != null ? fsm.FsmVariables.FindFsmInt("seed") : null; return seed != null ? seed.Value : int.MinValue; }
        /// <summary>Loads discovery state from the selected ES3 save once its world seed is available.</summary>
        internal static void WorldReady(string slot)
        {
            int seed = Seed(); if (_loaded && slot == _slot && seed == _seed) return;
            Discovered.Clear(); _readError = null; _slot = slot; _seed = seed; _loaded = seed != int.MinValue;
            try
            {
                if (!_fresh && _loaded && !string.IsNullOrEmpty(slot))
                {
                    string record = DiscoveriesSave.Read(slot);
                    var lines = record != null ? record.Split('\n') : new string[0];
                    if (record != null)
                    {
                        if (lines.Length == 0 || lines[0] != seed.ToString(CultureInfo.InvariantCulture)) throw new FormatException("Discovery record has an invalid world seed");
                        for (int i = 1; i < lines.Length; i++) if (lines[i].Length > 0) Discovered.Add(lines[i]);
                    }
                }
            }
            catch (Exception e) { Discovered.Clear(); _readError = "Location discoveries could not be read: " + e.Message; HudModule.Warn(_readError); }
            _fresh = false;
            HudModule.Log("Location discoveries loaded: " + Discovered.Count + " (seed " + seed + ")");
        }
        /// <summary>Captures the current discovery record in the verified ES3 save transaction.</summary>
        internal static void SaveEnded(string slot)
        {
            if (_readError != null) { SaveBus.ReportFailure(HudModule.Instance, _readError); return; }
            if (!_loaded || string.IsNullOrEmpty(slot)) return;
            if (Seed() != _seed)
            {
                SaveBus.ReportFailure(HudModule.Instance, "Location discoveries save rejected: world seed differs from loaded discoveries");
                return;
            }
            _slot = slot;
            try
            {
                var lines = new string[Discovered.Count + 1]; lines[0] = _seed.ToString(CultureInfo.InvariantCulture); Discovered.CopyTo(lines, 1);
                DiscoveriesSave.Write(HudModule.Instance, slot, string.Join("\n", lines));
            }
            catch (Exception e) { SaveBus.ReportFailure(HudModule.Instance, "Location discoveries save failed: " + e.Message); HudModule.Warn("Location discoveries could not be saved: " + e.Message); }
        }

        /// <summary>Initializes the location discovery markers.</summary>
        internal static void Init()
        {
            Clear();
            SaveBus.StateChanged -= StateChanged; SaveBus.StateChanged += StateChanged;
            TerrainTile.OnTileApplied -= Applied;
            TerrainTile.OnTileApplied += Applied;
        }
        /// <summary>Observes newly applied terrain tiles for native map location markers.</summary>
        private static void Applied(TerrainTile tile, TileData data, StopToken stop)
        {
            if (!HudModule.Running || tile == null) return;
            SceneCache.QueueRoot(tile.transform);
        }
        /// <summary>Observes the location discovery markers.</summary>
        internal static void Observe(Transform icon)
        {
            var root = icon != null ? icon.parent : null;
            if (root == null || Locations.ContainsKey(root)) return;
            string label = root.name.Split('(')[0].Trim(), glyph = "other"; Color color = Color.white;
            if (label.StartsWith("Cave_", System.StringComparison.OrdinalIgnoreCase)) glyph = "cave";
            else if (label.StartsWith("Wreck_", System.StringComparison.OrdinalIgnoreCase)) glyph = "wreck";
            else if (label.StartsWith("Building_", System.StringComparison.OrdinalIgnoreCase)) glyph = "building";
            else if (label.StartsWith("Camp_", System.StringComparison.OrdinalIgnoreCase))
            {
                glyph = "camp"; int number;
                if (int.TryParse(label.Substring(5), out number)) color = number == 10 || number == 15 || number == 16 ? new Color(.31f, .86f, .39f) : new Color(1f, .31f, .31f);
            }
            var renderer = icon.GetComponent<Renderer>();
            string generic = root.name == "Starter_Area" ? "tent" : glyph; glyph = generic;
            if (!HudModule.SimpleMapIcons.Value && MapIcons.Get(label) != null) glyph = label;
            string overrideIcon; if (Icons.TryGetValue(root, out overrideIcon)) glyph = overrideIcon;
            Locations.Add(root, new Location { Root = root, Icon = glyph, GenericIcon = generic, Color = color, Label = label, Caption = Labels.ContainsKey(root) ? Labels[root] : CaptionName(label), Native = renderer, ForceOff = renderer != null && renderer.forceRenderingOff });
        }
        /// <summary>Refreshes icons for the location discovery markers.</summary>
        internal static void RefreshIcons()
        {
            foreach (var location in Locations.Values)
            {
                string overrideIcon; location.Icon = Icons.TryGetValue(location.Root, out overrideIcon) ? overrideIcon : !HudModule.SimpleMapIcons.Value && MapIcons.Get(location.Label) != null ? location.Label : location.GenericIcon;
                if (location.Map != null) location.Map.Remove(location.MapMarker); location.MapMarker = 0;
                if (location.Marker != 0) Api.Compass.Remove(location.Marker); location.Marker = 0;
            }
        }
        /// <summary>Resolves caption name for the location discovery markers.</summary>
        private static string CaptionName(string label)
        {
            string name = label.Replace('_', ' ').TrimEnd();
            int end = name.Length;
            while (end > 0 && char.IsDigit(name[end - 1])) end--;
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Substring(0, end).TrimEnd().ToLowerInvariant());
        }
        /// <summary>Updates the location discovery markers.</summary>
        internal static void Tick()
        {
            if (!_loaded && GameRefs.InGame && !SaveBus.Busy) WorldReady(SaveBus.Slot);
            var player = Core.GameRefs.Player;
            bool hud = HudModule.Running && HudModule.CleanHudEnabled.Value;
            bool visible = hud && HudModule.ShowCompass.Value && HudModule.ShowLocations.Value && player != null;
            var camera = Core.GameRefs.MainCamera;
            var origin = camera != null ? camera.transform.position : player != null ? player.transform.position : Vector3.zero;
            Dead.Clear();
            foreach (var pair in Locations)
            {
                var location = pair.Value;
                if (pair.Key != null && _loaded && Origin.AnchorTransform != null && GameRefs.InGame && !SaveBus.Busy && player != null)
                {
                    var world = Origin.World(pair.Key.position);
                    if (location.Key == null)
                    {
                        location.Key = location.Label + ":" + Mathf.RoundToInt(world.x).ToString(CultureInfo.InvariantCulture) + ":" + Mathf.RoundToInt(world.z).ToString(CultureInfo.InvariantCulture);
                        location.Known = Discovered.Contains(location.Key);
                    }
                    var delta = pair.Key.position - player.transform.position; delta.y = 0;
                    bool spotted = Binoculars.Observe(pair.Key, location.Caption, location.Cleared ? Color.white : location.Color) || !location.Known && VantageDiscovery.Sees(pair.Key);
                    if (!location.Known && (delta.sqrMagnitude <= 10000f || spotted))
                    {
                        location.Known = true; Discovered.Add(location.Key);
                        if (Core.Log.Verbose) Core.Log.Info("[CleanHUD] Location discovered: " + location.Key);
                    }
                    if (!location.Cleared && location.Label.StartsWith("Camp_", StringComparison.OrdinalIgnoreCase) && location.Color.g < .5f && Services.Camps != null && Services.Camps.At(world, 120f) == CampState.Cleared)
                    {
                        location.Cleared = true;
                        if (location.Map != null) location.Map.Remove(location.MapMarker); location.MapMarker = 0;
                        if (location.Marker != 0) Api.Compass.Remove(location.Marker); location.Marker = 0;
                        if (Core.Log.Verbose) Core.Log.Info("[CleanHUD] Location cleared: " + location.Key);
                    }
                }
                var color = location.Cleared ? Color.white : location.Color;
                if (location.Native != null) location.Native.forceRenderingOff = hud && HudModule.MapEnabled.Value ? true : location.ForceOff;
                if (location.Map != Services.Map) { if (location.Map != null) location.Map.Remove(location.MapMarker); location.Map = Services.Map; location.MapMarker = 0; }
                bool excluded = ExcludedRoot(pair.Key);
                bool onMap = !excluded && pair.Key != null && hud && HudModule.MapEnabled.Value && location.Map != null && location.Map.Markers != MapDetail.Off;
                if (!onMap) { if (location.Map != null) location.Map.Remove(location.MapMarker); location.MapMarker = 0; }
                else if (!location.Map.Exists(location.MapMarker)) location.MapMarker = location.Map.Add(Owner, location.Position, location.Icon, color, location.Label);
                if (excluded || pair.Key == null || !visible || (pair.Key.position - origin).sqrMagnitude > 1000000f * VantageDiscovery.Scale * VantageDiscovery.Scale)
                {
                    if (location.Marker != 0) Api.Compass.Remove(location.Marker);
                    location.Marker = 0;
                    if (pair.Key == null) Dead.Add(pair.Key);
                }
                else if (location.Marker == 0)
                    location.Marker = Api.Compass.Add(Owner, pair.Key, new Api.Marker { Icon = location.Icon, Color = color, ShowDistance = true, MaxDistance = 1000f });
            }
            foreach (var root in Dead) Locations.Remove(root);
        }
        /// <summary>Clears transient location discovery markers state so the next scene or session starts cleanly.</summary>
        internal static void Clear()
        {
            foreach (var location in Locations.Values)
            {
                if (location.Map != null) location.Map.Remove(location.MapMarker);
                if (location.Native != null) location.Native.forceRenderingOff = location.ForceOff;
            }
            Api.Compass.RemoveAll(Owner); Locations.Clear(); Dead.Clear(); Excluded.Clear(); Icons.Clear(); Labels.Clear();
        }
        /// <summary>Restores native behavior and releases owned location discovery markers resources.</summary>
        internal static void Shutdown()
        {
            TerrainTile.OnTileApplied -= Applied;
            SaveBus.StateChanged -= StateChanged;
            Clear();
        }
    }
}
