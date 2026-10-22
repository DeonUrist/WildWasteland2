using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    internal static class MapLink
    {
        const string Owner = "Quests";
        sealed class Marker
        {
            internal Vector3 Position; internal Transform Target; internal string Label;
            internal Color Color; internal int Compass, Map; internal ICompassMarkers CompassApi; internal IMapMarkers MapApi;
            internal Vector3 Scene() { return Target != null ? Target.position : Core.Origin.Scene(Position); }
        }
        static readonly Dictionary<string, Marker> Markers = new Dictionary<string, Marker>();
        static readonly List<string> Removed = new List<string>();
        internal static bool Available { get { return Services.Map != null || Services.Compass != null; } }
        internal static bool IsExplored(Vector3 scene) { return Services.Map != null && Services.Map.Explored(scene); }
        internal static Color QuestColor(string id)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < id.Length; i++) hash = unchecked((hash ^ id[i]) * 16777619u);
            return Color.HSVToRGB((hash % 360u) / 360f, .6f, 1f);
        }
        internal static void Sync(Dictionary<string, Vector3> positions)
        {
            Removed.Clear(); foreach (var key in Markers.Keys) if (!positions.ContainsKey(key)) Removed.Add(key);
            foreach (var key in Removed) { Remove(Markers[key]); Markers.Remove(key); }
            foreach (var pair in positions)
            {
                Marker marker;
                if (!Markers.TryGetValue(pair.Key, out marker)) Markers[pair.Key] = marker = new Marker { Color = QuestColor(pair.Key.Substring(2)) };
                marker.Position = Core.Origin.World(pair.Value); marker.Target = null;
                bool returning = pair.Key.StartsWith("T:", StringComparison.Ordinal); string label = pair.Key.Substring(2);
                if (Plugin.I != null) foreach (var quest in Plugin.I.book.Active)
                {
                    if (quest.Id != label) continue;
                    label = quest.Title;
                    if (returning) marker.Target = Game.FindLive(quest.GiverId);
                    break;
                }
                label = (returning ? "Report in: " : "Quest: ") + label;
                if (marker.Label != label && marker.MapApi != null) { marker.MapApi.Remove(marker.Map); marker.Map = 0; }
                marker.Label = label;
            }
            Reconcile();
        }
        internal static void Reconcile()
        {
            foreach (var m in Markers.Values)
            {
                if (m.CompassApi != Services.Compass) { if (m.CompassApi != null) m.CompassApi.Remove(m.Compass); m.CompassApi = Services.Compass; m.Compass = 0; }
                if (m.MapApi != Services.Map) { if (m.MapApi != null) m.MapApi.Remove(m.Map); m.MapApi = Services.Map; m.Map = 0; }
                if (m.CompassApi != null && !m.CompassApi.Exists(m.Compass)) m.Compass = m.CompassApi.Add(Owner, m.Scene, "flag", m.Color);
                if (m.MapApi != null && !m.MapApi.Exists(m.Map)) m.Map = m.MapApi.Add(Owner, m.Scene, "flag", m.Color, m.Label);
            }
        }
        static void Remove(Marker marker) { if (marker.CompassApi != null) marker.CompassApi.Remove(marker.Compass); if (marker.MapApi != null) marker.MapApi.Remove(marker.Map); }
        internal static void Reset() { foreach (var m in Markers.Values) Remove(m); Markers.Clear(); }
    }
}
