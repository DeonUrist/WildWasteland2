using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Publishes patrol data while leaving all map rendering and drawing resources in HUD.</summary>
    public sealed partial class PatrolsModule : ITrafficMapSource
    {
        /// <summary>Returns the current authored War Rig notice deadline.</summary>
        public float NoticeUntil { get { return (ModApi.Get<IEncounterNotices>()?.NoticeUntil??0); } }
        /// <summary>Prunes destroyed rigs before exposing live map positions.</summary>
        public int RigCount { get { return ModApi.Get<IEncounterNotices>()?.RigCount??0; } }
        /// <summary>Reads a live rig in stable world coordinates.</summary>
        public Vector3 RigPosition(int index) { return (ModApi.Get<IEncounterNotices>()?.RigPosition(index)??Vector3.zero); }
        /// <summary>Copies current simulation metadata without handing live patrol actors to HUD.</summary>
        public void Capture(TrafficMapSnapshot target)
        {
            target.Journeys.Clear(); target.Points.Clear(); target.Battles.Clear();
            target.Enabled = false;
            if (_traffic != null) _traffic.CaptureMap(target);
        }
    }
    /// <summary>Converts simulation-owned route and recognition state into presentation records.</summary>
    internal sealed partial class WorldTraffic
    {
        /// <summary>Copies the same visibility, colour and naming decisions formerly used by its map partial.</summary>
        internal void CaptureMap(TrafficMapSnapshot target)
        {
            target.Enabled = PatrolsModule.ConvoyEnabled.Value;
            target.Routes = RoutesOn; target.Markers = MarkersOn; target.Names = MapNames;
            target.HideRoutes = InkUnknown; target.HideMarkers = MapUnknown;
            foreach (var journey in _journeys) target.Journeys.Add(new TrafficMapJourney { Position = journey.position, Route = journey.route, Foot = journey.foot, Name = MapName(journey), RouteColor = RouteColor(journey, InkUnknown), MarkerColor = RouteColor(journey, MapUnknown && !TrafficCompass.Known(journey)) });
            var map = ModApi.Get<IMapMarkers>();
            if (map == null) foreach (var camp in TrafficLocations.Known.Values) if (camp.cleared) target.Points.Add(new TrafficMapPoint { Position = camp.center, Color = Color.gray, Name = camp.name + " [Cleared]" });
            foreach (var battle in _battles) target.Battles.Add(battle.position);
            foreach (var merchant in MerchantHost.All)
            {
                if (merchant == null || Owns(merchant.GetComponent<PatrolMarker>())) continue;
                bool known = TrafficCompass.Known(TrafficCompass.MerchantKey(merchant));
                target.Points.Add(new TrafficMapPoint { Position = Origin.World(merchant.transform.position), Color = MapUnknown && !known ? Color.gray : Color.yellow, Name = MapUnknown && !known ? "Unknown" : MapNames || known ? merchant.Data?.group ?? "Coyote merchant" : "Merchant" });
            }
        }
    }
}
