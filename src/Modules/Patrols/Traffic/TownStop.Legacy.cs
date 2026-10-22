using UnityEngine;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Retained until the POI owner replaces its old traffic producer with ITownTraffic snapshots.</summary>
    [System.Obsolete("refactor: use Core.TownTrafficStop")]
    internal sealed class TownStop
    {
        internal string Id, Name, Faction;
        internal Vector3 Center, Parking, Heading;
        internal Vector3? RoadApproach;
        internal float Radius;
        internal bool CanTravel;
        internal bool MerchantSeeded;
        internal bool Lane;   // (WW) a gas station parking lot: vehicles park one behind the other in its canopy lane, not side by side
        internal Vector3 Approach
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
        internal TrafficBase Gate { get { return new TrafficBase { id = Id, apron = Approach }; } }
    }

}
