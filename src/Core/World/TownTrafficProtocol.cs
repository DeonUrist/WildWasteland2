using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Immutable-by-convention town parking snapshot consumed by long-distance traffic in world coordinates.</summary>
    public sealed class TownTrafficStop
    {
        public string Id, Name, Faction;
        public Vector3 Center, Parking, Heading;
        public Vector3? RoadApproach;
        public float Radius;
        public bool CanTravel, MerchantSeeded, Lane;
        /// <summary>Returns the existing exterior road approach, falling back to the parking lane heading.</summary>
        public Vector3 Approach
        {
            get
            {
                if (RoadApproach.HasValue) return RoadApproach.Value;
                var delta = Parking - Center;
                float x = Mathf.Abs(Heading.x) < .001f ? float.MaxValue : (Radius + 20f - Mathf.Sign(Heading.x) * delta.x) / Mathf.Abs(Heading.x);
                float z = Mathf.Abs(Heading.z) < .001f ? float.MaxValue : (Radius + 20f - Mathf.Sign(Heading.z) * delta.z) / Mathf.Abs(Heading.z);
                return Parking + Heading * Mathf.Max(60f, Mathf.Min(x, z));
            }
        }
    }

    /// <summary>World-space patrol origin supplied by inhabited-town exterior parking.</summary>
    public sealed class TownPatrolBase
    {
        public string Id, Name, Faction;
        public Vector3 Center, Gate, Apron, Size;
    }

    /// <summary>Town traffic geometry and population facts absent from the frozen editor-facing town contract.</summary>
    public interface ITownTraffic
    {
        /// <summary>Revision of known town travel geometry, including streaming/reset changes.</summary>
        int TravelRevision { get; }
        /// <summary>Existing gas-station parking lane separation in metres.</summary>
        float LaneSpread { get; }
        /// <summary>Appends eligible town parking destinations in world coordinates.</summary>
        void TravelStops(List<TownTrafficStop> into);
        /// <summary>Records that this town already received its resident merchant.</summary>
        void MerchantSeeded(string key);
        /// <summary>True when the key identifies an inhabited town parking origin.</summary>
        bool PatrolBase(string id);
        /// <summary>Number of inhabited town origins near a world point.</summary>
        int PatrolCount(Vector3 world, float range);
        /// <summary>Appends inhabited town patrol origins as detached world-space data.</summary>
        void PatrolBases(List<TownPatrolBase> into);
        /// <summary>Resolves exterior parking for a point inside a town footprint.</summary>
        bool ParkingOutside(Vector3 world, out Vector3 parking);
        /// <summary>Appends exclusion geometry for numeric worker route planning.</summary>
        void TravelBlocks(List<Bounds> into);
        /// <summary>Returns the town-safe waypoint and hold instruction for scene-space vehicle navigation.</summary>
        bool VehicleWaypoint(Vector3 sceneFrom, Vector3 sceneGoal, out Vector3 sceneWaypoint, out bool hold);
    }
}
