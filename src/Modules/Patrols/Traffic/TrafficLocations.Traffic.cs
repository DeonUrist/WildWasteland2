using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Traffic queries on the known locations (patrol bases and counts, spawn rules, engine range, vehicle clearance, camp approach, coarse route); Patrols part (Wave A split of Traffic/TrafficLocations.cs).</summary>
    internal static partial class TrafficLocations
    {
        internal static int NearbyCount(Vector3 player,float range)
        {
            int count=0;foreach(var camp in Known.Values)
                if(camp.faction!="Coyotes"&&!camp.cleared&&TrafficCamps.Loaded(camp.id)&&!TownInterior(camp.center)&&FlatDistance(camp.center,player)<=range)count++;
            if (Core.ModApi.Get<Core.ITownTraffic>() != null) count += Core.ModApi.Get<Core.ITownTraffic>().PatrolCount(player, range);
            return count;
        }
        internal static TrafficBase[] NearbyBases(Vector3 player,float range)
        {return PatrolBases().Where(c=>BaseLoaded(c.id)&&FlatDistance(c.center,player)<=range).ToArray();}
        internal static TrafficBase[] PatrolBases()
        {
            var bases = Known.Values.Where(c => c.faction != "Coyotes" && !c.cleared && !TownInterior(c.center)).ToList();
            var towns = Core.ModApi.Get<Core.ITownTraffic>();
            if (towns != null)
            {
                var origins = new List<Core.TownPatrolBase>(); towns.PatrolBases(origins);
                foreach (var origin in origins) bases.Add(new TrafficBase { id = origin.Id, name = origin.Name, faction = origin.Faction,
                    center = origin.Center, gate = origin.Gate, apron = origin.Apron, size = origin.Size });
            }
            return bases.ToArray();
        }
        internal static bool BaseLoaded(string id)
        { return TrafficCamps.Loaded(id) || Core.ModApi.Get<Core.ITownTraffic>() != null && Core.ModApi.Get<Core.ITownTraffic>().PatrolBase(id); }

        internal static bool PlayerAt(TrafficBase camp)
        {var player=PlayerRef.Player;return player!=null&&Contains(camp,TrafficSpace.World(player.position));}
        internal static bool PatrolSpawnAllowed(Vector3 point)
        {foreach(var camp in Known.Values)if(PlayerAt(camp)&&Contains(camp,point,60f))return false;return true;}

        internal static float EngineRange(GameObject car)
        {
            return PatrolVisibility.EngineDistance(car);
        }

        private static readonly RaycastHit[] Hits = new RaycastHit[64];
        internal static bool VehicleClear(GameObject car, Vector3 from, Vector3 to, float radius)
        {
            var d = to - from; float length = d.magnitude; if (length < 0.1f) return true;
            int n = Physics.SphereCastNonAlloc(from, radius, d / length, Hits, length,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (n == Hits.Length) return false; // overflow cannot certify clearance
            GameObject trailer = null; bool trailerKnown = false;   // Perf: looked up once per call (was twice per hit)
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.collider == null || h.distance > length || h.distance <= 0f) continue;
                if (h.collider is TerrainCollider) continue;
                if (car != null && !h.collider.transform.IsChildOf(car.transform) && !trailerKnown) { trailerKnown = true; trailer = TrailerSupport.Attached(car); }
                if (car != null && (h.collider.transform.IsChildOf(car.transform)
                    || (trailer != null && h.collider.transform.IsChildOf(trailer.transform)))) continue;
                if (h.collider.gameObject.layer == 9 || h.collider.GetComponentInParent<PatrolMarker>() != null) continue;
                return false;
            }
            Array.Clear(Hits, 0, Hits.Length);
            return true;
        }

        internal static bool CampApproach(GameObject car, Vector3 target, out Vector3 approach, out bool hold)
        {
            approach = target; hold = false;
            var worldTarget = TrafficSpace.World(target);
            foreach (var camp in Known.Values)
            {
                if (Mathf.Abs(worldTarget.x - camp.center.x) > camp.size.x * 0.5f || Mathf.Abs(worldTarget.z - camp.center.z) > camp.size.z * 0.5f) continue;
                var from = car.transform.position;
                // A human-sized opening is never accepted without checking the chassis corridor.
                float radius = car.GetComponent<TrafficAgent>()?.Truck == true ? 2f : 1.4f;
                if (VehicleClear(car, from + Vector3.up, target + Vector3.up, radius)) return false;
                approach = TrafficSpace.Scene(camp.mappedEntrance ? camp.gate : camp.apron);
                if (FlatDistance(from, approach) < 12f)
                {
                    if (camp.mappedEntrance && VehicleClear(car, from + Vector3.up, target + Vector3.up, radius)) return false;
                    hold = true; // shooters can engage from the apron; the driver does not press into spikes/walls
                }
                return true;
            }
            return false;
        }

        internal static Vector3[] Route(TrafficBase source, TrafficBase destination)
        {
            var route = new List<Vector3> { source.apron, destination.apron };
            for (int attempts = 0; attempts < 32; attempts++)
            {
                bool changed = false;
                for (int leg = 1; leg < route.Count && !changed; leg++)
                    foreach (var camp in Known.Values)
                    {
                        var box = new Bounds(new Vector3(camp.center.x, 0f, camp.center.z), new Vector3(camp.size.x + 16f, 10000f, camp.size.z + 16f));
                        var a = route[leg - 1]; var b = route[leg]; a.y = b.y = 0f;
                        if (box.Contains(a) || box.Contains(b)) continue;
                        if (!box.IntersectRay(new Ray(a, (b - a).normalized), out var hit) || hit >= Vector3.Distance(a, b)) continue;
                        Vector3[] corners = {
                            new Vector3(box.min.x - 2f, route[leg-1].y, box.min.z - 2f),
                            new Vector3(box.min.x - 2f, route[leg-1].y, box.max.z + 2f),
                            new Vector3(box.max.x + 2f, route[leg-1].y, box.min.z - 2f),
                            new Vector3(box.max.x + 2f, route[leg-1].y, box.max.z + 2f) };
                        float best = float.MaxValue; Vector3[] detour = null;
                        foreach (var c in corners) foreach (var d in corners)
                        {
                            if (Crosses(box, a, c) || Crosses(box, c, d) || Crosses(box, d, b)) continue;
                            float cost = FlatDistance(a, c) + FlatDistance(c, d) + FlatDistance(d, b);
                            if (cost < best) { best = cost; detour = c == d ? new[] { c } : new[] { c, d }; }
                        }
                        if (detour == null) return null;
                        route.InsertRange(leg, detour); changed = true; break;
                    }
                if (!changed) return route.ToArray();
            }
            return null; // overlapping geometry has no bounded route in this prototype
        }

        private static bool Crosses(Bounds box, Vector3 a, Vector3 b)
        { a.y = b.y = 0f; var d = b - a; return d.magnitude > 0.01f && box.IntersectRay(new Ray(a, d.normalized), out var hit) && hit < d.magnitude; }
    }
}
