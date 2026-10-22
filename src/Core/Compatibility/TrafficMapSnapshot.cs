using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>One patrol-owned journey projected by HUD; positions and route points use the stable world frame.</summary>
    public sealed class TrafficMapJourney
    {
        public Vector3 Position;
        public Vector3[] Route;
        public Color32 RouteColor, MarkerColor;
        public string Name;
        public bool Foot;
    }
    /// <summary>One detached merchant or cleared location label supplied by Patrols.</summary>
    public struct TrafficMapPoint
    {
        public Vector3 Position;
        public Color Color;
        public string Name;
    }
    /// <summary>Reusable presentation snapshot containing no patrol simulation objects.</summary>
    public sealed class TrafficMapSnapshot
    {
        public readonly List<TrafficMapJourney> Journeys = new List<TrafficMapJourney>();
        public readonly List<TrafficMapPoint> Points = new List<TrafficMapPoint>();
        public readonly List<Vector3> Battles = new List<Vector3>();
        public bool Enabled, Routes, Markers, Names, HideRoutes, HideMarkers;
    }
    /// <summary>Patrol-owned traffic data for HUD-owned map drawing.</summary>
    public interface ITrafficMapSource
    {
        float NoticeUntil { get; }
        int RigCount { get; }
        Vector3 RigPosition(int index);
        void Capture(TrafficMapSnapshot target);
    }
}
