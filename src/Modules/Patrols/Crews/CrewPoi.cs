using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // Read the existing NPC structures once per half-second, shared by all cars. No new scene scan or bake.
    internal static class PatrolPoi
    {
        private static readonly List<Core.NavStructure> Structures = new List<Core.NavStructure>();
        private static readonly Collider[] Occupants = new Collider[32];
        private static float _next;
        internal static Transform Root;
        internal static Bounds Box;

        internal static void Reset() { Root = null; _next = 0f; Structures.Clear(); }
        internal static Transform PlayerRoot
        {
            get
            {
                if (Time.time < _next) return Root;
                _next = Time.time + .5f; Root = null;
                var player = PlayerRef.Player; var api = Core.Services.Npc;
                if (player == null || api == null || PlayerRef.PlayerCar != null) return null;
                api.Structures(Structures); float best = float.MaxValue;
                foreach (var s in Structures)
                {
                    if (s == null || s.Root == null || !s.Root.gameObject.activeInHierarchy || !s.Box.HasValue
                        || !(TrafficLocations.IsCamp(s.Root.name) || TrafficLocations.PoiName(s.Root.name))) continue;
                    var box = s.Box.Value; var p = player.position;
                    if (p.x < box.min.x || p.x > box.max.x || p.z < box.min.z || p.z > box.max.z
                        || p.y < box.min.y - 3f || p.y > box.max.y + 3f) continue;
                    float area = box.size.x * box.size.z;
                    if (area >= best) continue;
                    best = area; Root = s.Root; Box = box;
                }
                Structures.Clear(); return Root;
            }
        }

        internal static bool Ignored(GameObject car)
        {
            var agent = WorldTraffic.AgentOf(car);
            return agent != null && WorldTraffic.Find(agent.Journey)?.convoy == true && PlayerRoot != null;
        }

        internal static bool Parking(GameObject car, Bounds box, out Vector3 point)
        {
            point = car.transform.position; float best = float.MaxValue;
            Vector3 townParking;
            if (Modules.Towns.TownsApi.Provider != null && Modules.Towns.TownsApi.Provider.ParkingOutside(TrafficSpace.World(box.center), out townParking))
            {
                var p = TrafficSpace.Scene(townParking); RaycastHit floor;
                if (!Physics.Raycast(p + Vector3.up * 80f, Vector3.down, out floor, 200f, 1 << 14, QueryTriggerInteraction.Ignore) || floor.normal.y < .94f) return false;
                p.y = floor.point.y;
                int occupied = Physics.OverlapBoxNonAlloc(p + Vector3.up * 2f, new Vector3(4f, 1.5f, 4f), Occupants, Quaternion.identity, Physics.DefaultRaycastLayers & ~(1 << 14), QueryTriggerInteraction.Ignore);
                if (occupied == Occupants.Length) return false;
                for (int i = 0; i < occupied; i++) if (!Occupants[i].transform.IsChildOf(car.transform)) return false;
                point = p; return true;
            }
            var exclusion = box; exclusion.Expand(new Vector3(8f, 0f, 8f));
            var start = car.transform.position; start.y = exclusion.center.y;
            // Four faces, with two alternatives each; the vehicle's existing obstacle avoidance drives the approach.
            for (int side = 0; side < 4; side++) for (int n = -1; n <= 1; n++)
            {
                var p = car.transform.position;
                if (side < 2) { p.x = side == 0 ? box.min.x - 12f : box.max.x + 12f; p.z = Mathf.Clamp(car.transform.position.z + n * 12f, box.min.z, box.max.z); }
                else { p.z = side == 2 ? box.min.z - 12f : box.max.z + 12f; p.x = Mathf.Clamp(car.transform.position.x + n * 12f, box.min.x, box.max.x); }
                float distance = (p - car.transform.position).sqrMagnitude;
                if (distance >= best) continue;
                var end = p; end.y = start.y; var leg = end - start; float entry;
                if (!exclusion.Contains(start) && exclusion.IntersectRay(new Ray(start, leg.normalized), out entry) && entry < leg.magnitude) continue;
                RaycastHit hit;
                if (!Physics.Raycast(p + Vector3.up * 80f, Vector3.down, out hit, 200f, 1 << 14, QueryTriggerInteraction.Ignore) || hit.normal.y < .94f) continue;
                p.y = hit.point.y;
                int count = Physics.OverlapBoxNonAlloc(p + Vector3.up * 2f, new Vector3(4f, 1.5f, 4f), Occupants, Quaternion.identity, Physics.DefaultRaycastLayers & ~(1 << 14), QueryTriggerInteraction.Ignore);
                bool blocked = count == Occupants.Length;
                for (int i = 0; i < count; i++) if (!Occupants[i].transform.IsChildOf(car.transform)) { blocked = true; break; }
                if (blocked) continue;
                point = p; best = distance;
            }
            if (best < float.MaxValue) return true;
            // If all surveyed spots are occupied, a car already standing beside the POI can stop where it is.
            point = car.transform.position;
            return !exclusion.Contains(start) && (box.ClosestPoint(start) - start).sqrMagnitude <= 900f && car.transform.up.y > .94f;
        }
    }

    internal partial class Crew
    {
        private Transform _poiRoot;
        private Vector3 _poiPark; // stable world coordinates
        private bool _poiApproach, _poiAssault;
        private float _nextPoiPark, _poiRange = 120f;
        private RoadJob _poiRoad;
        private int _poiLeg;
        internal bool PoiExit { get { return _poiAssault; } }
        internal bool PoiEngaged
        {
            get
            {
                var player = PlayerRef.Player;
                return _poiRoot != null && PatrolPoi.PlayerRoot == _poiRoot && player != null && !PlayerRef.GodMode
                    && TrafficFactions.PlayerHostile(Marker.Faction) && CombatEnemy != null
                    && (_poiApproach || TrafficLocations.FlatDistance(player.position, transform.position) <= _poiRange);
            }
        }

        private bool PoiTick(bool slow)
        {
            if (Regroup != null) return false;
            var agent = WorldTraffic.AgentOf(_car);
            var journey = agent != null ? WorldTraffic.Find(agent.Journey) : null;
            bool eligible = journey != null && !journey.convoy && !journey.merchant && !journey.townParked && !journey.townResident
                && !Marker.SetupOnly && !Marker.PlayerEntered && !_dead && !_done && !FullyStuck && !PlayerInside()
                && PatrolsModule.ReturnToCar.Value && Core.Services.Npc != null && !PlayerRef.GodMode && TrafficFactions.PlayerHostile(Marker.Faction);
            var root = eligible ? PatrolPoi.PlayerRoot : null;
            var player = PlayerRef.Player;
            if (root == null || player == null || (_poiRoot != null && root != _poiRoot)) { EndPoi(); return false; }
            if (_poiRoot == null)
            {
                if (agent.SeenUntil <= Time.time && CombatEnemy != player.gameObject) return false;
                if (Time.time < _nextPoiPark) return false;
                _nextPoiPark = Time.time + 2f;
                Vector3 park;
                if (!PatrolPoi.Parking(_car, PatrolPoi.Box, out park)) return false;
                _poiRoot = root; _poiPark = TrafficSpace.World(park); _poiApproach = _poiAssault = true;
                _poiRoad = RoadPlanner.RequestRoute(new TrafficBase { apron = TrafficSpace.World(transform.position) }, new TrafficBase { apron = _poiPark }, false);
                _poiLeg = 1;
                _combatEnemy = player.gameObject; _enemyHealth = CrewReturn.HealthVar(_combatEnemy); _resumeChase = false;
                PatrolsModule.Verbose("POI assault: " + _car.name + " parks outside " + root.name);
            }
            if (!PoiEngaged) { EndPoi(); return false; }
            Marker.PlayerContactAt = Time.time;
            if (!slow || TrafficLocations.FlatDistance(transform.position, TrafficSpace.Scene(_poiPark)) > 9f) return false;
            // Large POIs can put the player over 120 m from their perimeter; do not immediately recall a fresh assault.
            _poiRange = Mathf.Max(120f, TrafficLocations.FlatDistance(player.position, transform.position) + 30f);
            _poiApproach = false;
            PatrolsModule.Verbose("POI assault: parked; crew attacks on foot using NPC navigation");
            return ExitCrew(false);
        }

        internal bool PoiGoal(out Vector3 goal, out bool stop, out float cruise)
        {
            goal = Vector3.zero; stop = false; cruise = 0f;
            if (!_poiApproach || _poiRoot == null) return false;
            goal = TrafficSpace.Scene(_poiPark);
            float distance = TrafficLocations.FlatDistance(transform.position, goal);
            stop = distance <= 8f;
            cruise = Mathf.Min(8f, Mathf.Sqrt(Mathf.Max(0f, distance - 5f) * 3f));
            if (stop) return true;
            if (_poiRoad != null && !_poiRoad.Complete) { goal = transform.position; stop = true; return true; }
            var route = _poiRoad != null ? _poiRoad.Route : null;
            if (route != null && route.Length >= 2)
            {
                var actual = TrafficSpace.World(transform.position);
                while (_poiLeg < route.Length - 1)
                {
                    var leg = route[_poiLeg] - route[_poiLeg - 1]; leg.y = 0f;
                    var offset = actual - route[_poiLeg - 1]; offset.y = 0f;
                    float t = leg.sqrMagnitude > .01f ? Vector3.Dot(offset, leg) / leg.sqrMagnitude : 1f;
                    bool passed = t >= 1f && TrafficLocations.FlatDistance(actual, route[_poiLeg - 1] + leg * t) < 8f;
                    if (!passed && TrafficLocations.FlatDistance(actual, route[_poiLeg]) > 8f) break;
                    _poiLeg++;
                }
                goal = TrafficSpace.Scene(RoadPath.Target(route, _poiLeg, actual, _rb != null ? _rb.velocity.magnitude : 0f));
                cruise = RoadPath.Speed(route, _poiLeg, actual, cruise);
            }
            else if (_poiRoad != null)
            {
                PatrolsModule.Verbose("POI assault: no certified terrain route; using local vehicle avoidance to the exterior parking point");
                _poiRoad = null;
            }
            return true;
        }

        internal void EndPoi()
        {
            if (!_poiAssault) return;
            _poiRoot = null; _poiRoad = null; _poiApproach = _poiAssault = false; _combatEnemy = null; _enemyHealth = null; _resumeChase = false; _attackedAt = -1e6f;
            var agent = WorldTraffic.AgentOf(_car); if (agent != null) agent.ForgetPlayer();
            PatrolsModule.Verbose("POI assault: ended; crew returns to patrol");
        }
    }
}
