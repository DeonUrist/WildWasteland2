using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WildWasteland.Modules.Towns;

namespace WildWasteland.Modules.Patrols
{
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

    internal sealed partial class WorldTraffic
    {
        private sealed class TownDeparture
        { internal RoadJob Job; internal TownStop Source, Destination; internal TrafficJourney Journey; internal bool Existing; }
        private readonly List<TownDeparture> _townDepartures = new List<TownDeparture>();
        private readonly List<TownStop> _townStops = new List<TownStop>();
        private float _nextTownStops, _nextResident;
        private readonly Dictionary<string, GameObject> _townGuards = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _townGuardOrders = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _townGuardJourneys = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _townRetry = new Dictionary<string, float>(StringComparer.Ordinal);
        private static bool TownTravel { get { return TownsApi.Provider != null; } }
        internal static bool TownDismounted(GameObject car, GameObject mob)
        {
            if (_instance == null || car == null || mob == null) return false;
            var marker = car.GetComponent<PatrolMarker>(); var journey = marker != null ? Find(marker.TrafficId) : null;
            if (journey == null || !journey.townParked) return false;
            var names = new HashSet<string>(journey.townGuards ?? new string[0], StringComparer.Ordinal); names.Add(mob.name);
            journey.townGuards = names.ToArray(); _instance._townGuards[mob.name] = mob; _instance._townGuardJourneys[mob.name] = journey.id;
            return true;
        }
        private void RefreshTownStops()
        {
            if (Time.time < _nextTownStops) return;
            _nextTownStops = Time.time + 2f; _townStops.Clear();
            if (TownTravel) TownsApi.Provider.TravelStops(_townStops);
        }
        private void ResetTownTraffic()
        { _townDepartures.Clear(); _townStops.Clear(); _townGuards.Clear(); _townGuardOrders.Clear(); _townGuardJourneys.Clear(); _townRetry.Clear(); _nextTownStops = _nextResident = 0f; }
        internal static bool TownPlanning(string id)
        {
            if (_instance == null) return false;
            foreach (var request in _instance._townDepartures) if (request.Journey.id == id) return true;
            return false;
        }
        internal static void TownsStopped()
        {
            if (_instance == null) return;
            _instance._townDepartures.Clear(); _instance._townStops.Clear();
            foreach (var journey in _instance._journeys) if (journey.townBound && !journey.townParked)
            { journey.townBound = journey.townResident = false; journey.destinationSeconds = journey.unseenSeconds = 0f; }
        }
        internal static bool RetainsTown(GameObject target)
        {
            if (_instance == null || target == null) return false;
            var marker = target.GetComponentInParent<PatrolMarker>();
            var journey = marker != null ? Find(marker.TrafficId) : null;
            if (journey == null)
            {
                string id; if (_instance._townGuardJourneys.TryGetValue(target.name, out id)) journey = Find(id);
                var platform = target.GetComponentInParent<TrafficPlatform>();
                if (journey == null && platform != null && platform.Owner != null) journey = Find(platform.Owner.Journey);
            }
            return journey != null && journey.townBound;
        }
        internal static void RestoreTownGuards()
        {
            if (_instance == null) return;
            var names = new HashSet<string>(_instance._journeys.SelectMany(j => j.townGuards ?? new string[0]), StringComparer.Ordinal);
            foreach (var journey in _instance._journeys) foreach (string name in journey.townGuards ?? new string[0]) _instance._townGuardJourneys[name] = journey.id;
            foreach (var fsm in PlayMakerFSM.FsmList)
                if (fsm != null && fsm.FsmName == "Health" && names.Contains(fsm.name)) _instance._townGuards[fsm.name] = fsm.gameObject;
        }
        private bool SameMapArea(Vector3 world)
        {
            var player = PlayerRef.Player; if (player == null) return true;
            var center = TrafficSpace.World(player.position); var direction = world - center; direction.y = 0f;
            if (direction.sqrMagnitude < 1000000f) return true;
            // Uses the native, unzoomed map footprint even while M is closed.
            return TrafficLocations.FlatDistance(center, world) <= TrafficLocations.FlatDistance(center, OffscreenTrafficPoint(center, direction.normalized));
        }
        private bool ParkingBusy(string stop, string except = null)
        { return _journeys.Any(j => j.id != except && j.townBound && j.destination == stop && !j.townSeedBus)
            || _townDepartures.Any(p => p.Journey.id != except && p.Destination.Id == stop); }
        private bool QueueTownDeparture(SpawnGroup group, int role, List<CarTemplate> roster)
        {
            RefreshTownStops();
            if (_journeys.Count >= 120) return false; // Keep headroom inside the existing traffic-save record limit.
            if (_townDepartures.Any(d => d.Journey.merchant == (role == 2))) return false;
            var destinations = _townStops.Where(s => s.CanTravel && (role != 2 || s.Faction == "Coyotes") && !ParkingBusy(s.Id)).ToList();
            var sources = _townStops.Where(s => s.CanTravel).ToList();
            var player = PlayerRef.Player; if (player == null || destinations.Count == 0) return false;
            var position = TrafficSpace.World(player.position);
            // Actual settlements are remembered before encounter visibility; no invented offscreen endpoints.
            var pairs = (from a in sources from b in destinations where a.Id != b.Id && TrafficLocations.FlatDistance(a.Parking, b.Parking) > 300f
                && !SameMapArea(a.Parking) && !ParkingBusy(a.Id) select new { A = a, B = b, Distance = TrafficLocations.FlatDistance(position, a.Parking) + TrafficLocations.FlatDistance(position, b.Parking) }).OrderBy(p => p.Distance).Take(8).ToArray();
            if (pairs.Length == 0) return false;
            var pair = pairs[UnityEngine.Random.Range(0, pairs.Length)];
            var journey = new TrafficJourney { id = Guid.NewGuid().ToString("N"), name = group.Name, faction = group.Faction,
                source = pair.A.Id, destination = pair.B.Id, merchant = role == 2, convoy = role == 1, townBound = true,
                vehicles = roster.Select(Freeze).ToArray(), speed = PatrolsModule.TrafficConvoySpeed.Value / 3.6f };
            _townDepartures.Add(new TownDeparture { Source = pair.A, Destination = pair.B, Journey = journey,
                Job = RoadPlanner.RequestRoute(pair.A.Gate, pair.B.Gate, true) });
            Core.Log.Info("[Patrols] Town route planning: " + pair.A.Name + " -> " + pair.B.Name);
            return true;
        }
        private static Vector3 ParkPoint(TownStop stop, bool resident)
        {
            if (stop.Lane) return stop.Parking + stop.Heading * (resident ? -Towns.TownGasStation.LaneSpread : Towns.TownGasStation.LaneSpread);   // the bus deep under the canopy, visitors by its open end
            return stop.Parking + Vector3.Cross(Vector3.up, stop.Heading) * (resident ? -6f : 5f);
        }
        private static void AdoptTownRoute(TownDeparture pending)
        {
            var j = pending.Journey; var route = new List<Vector3>(); var roads = new List<bool>();
            route.Add(j.arrived ? j.position : ParkPoint(pending.Source, false)); roads.Add(false);
            foreach (var p in pending.Job.Route) { route.Add(p); roads.Add(false); }
            for (int i = 0; i < pending.Job.Roads.Length && i + 1 < roads.Count; i++) roads[i + 1] = pending.Job.Roads[i];
            route.Add(ParkPoint(pending.Destination, false)); roads.Add(false);
            j.source = pending.Source.Id; j.destination = pending.Destination.Id; j.route = route.ToArray(); j.roadLegs = roads.ToArray();
            j.townResident = j.townSeedBus = false;
            j.position = route[0]; j.leg = 1; j.arrived = false; j.TerrainRoute = pending.Job.Elevation;
            j.destinationSeconds = j.unseenSeconds = j.waitSeconds = j.townVisitSeconds = 0f;
        }
        private void PumpTownRoutes()
        {
            if (!TownTravel) { _townStops.Clear(); _townDepartures.Clear(); return; }
            RefreshTownStops();
            foreach (var pending in _townDepartures.ToArray())
            {
                if (!pending.Job.Complete) continue;
                _townDepartures.Remove(pending);
                if (pending.Job.Route == null || !pending.Job.Elevation || !_townStops.Any(s => s.Id == pending.Destination.Id))
                {
                    if (pending.Existing) _townRetry[pending.Journey.id] = Time.time + 30f;
                    continue;
                }
                var j = pending.Journey;
                if (pending.Existing && !_journeys.Contains(j)) continue;
                if ((!_journeys.Contains(j) || j.townResident) && RoleCount(j.merchant ? 2 : 1) >= (j.merchant ? PatrolsModule.MaxMerchants.Value : PatrolsModule.MaxConvoys.Value)) continue;
                AdoptTownRoute(pending);
                j.townBound = true;
                if (!_journeys.Contains(j)) _journeys.Add(j);
                Core.Log.Always("[Patrols] Town route ready: " + pending.Source.Name + " -> " + pending.Destination.Name + "; " + j.route.Length + " waypoints");
            }
            // Old saves may contain pass-through routes. Redirect their existing vehicles/stock;
            // never rebuild a different roster just to attach a settlement destination.
            foreach (var journey in _journeys)
            {
                if (journey.townBound || journey.foot || !journey.merchant && !journey.convoy || !string.IsNullOrEmpty(journey.battle)
                    || _townDepartures.Any(p => ReferenceEquals(p.Journey, journey))) continue;
                float retryAt; if (_townRetry.TryGetValue(journey.id, out retryAt) && Time.time < retryAt) continue;
                var destination = _townStops.Where(s => s.CanTravel && (!journey.merchant || s.Faction == "Coyotes") && !ParkingBusy(s.Id, journey.id)
                    && TrafficLocations.FlatDistance(s.Parking, journey.position) > 150f).OrderBy(s => TrafficLocations.FlatDistance(s.Parking, journey.position)).FirstOrDefault();
                if (destination == null) continue;
                var source = new TownStop { Id = journey.source ?? "", Name = journey.name, Parking = journey.position, Center = journey.position, Heading = journey.Heading };
                var request = new TownDeparture { Journey = journey, Source = source, Destination = destination, Existing = true,
                    Job = RoadPlanner.RequestRoute(source.Gate, destination.Gate, true) };
                _townDepartures.Add(request);
                break;
            }
            if (TownTravel && PatrolsModule.Merchants.Value && PatrolsModule.MaxMerchants.Value > 0 && PatrolsModule.MerchantSpawnChance.Value > 0f && Time.time >= _nextResident)
            {
                _nextResident = Time.time + 3f;
                foreach (var stop in _townStops)
                {
                    if (_journeys.Count >= 120) break;
                    if (stop.Faction != "Coyotes" || stop.MerchantSeeded) continue;
                    // Buses use the normal template builder, shop stock, health and save registration.
                    var groups = EditorStore.Data.Merchants.Where(g => g.Chance > 0f && g.Faction == "Coyotes").ToArray();
                    if (groups.Length == 0) break;
                    var group = groups[TownPlanner.Hash(stop.Id + "/merchant") % groups.Length];
                    var template = group.Templates.Select(CarTemplate.Find).FirstOrDefault(candidate => candidate != null && candidate.Available && CrewFactions.Compatible(candidate, "Coyotes"));
                    if (template == null) break;
                    var t = template.CloneForSpawn(); MerchantRuntime.Configure(t, group); t.MerchantTravelling = true;
                    var point = ParkPoint(stop, true);
                    _journeys.Add(new TrafficJourney { id = Guid.NewGuid().ToString("N"), name = stop.Name + " merchant", faction = "Coyotes", merchant = true,
                        townBound = true, townResident = true, townSeedBus = true, arrived = true, source = stop.Id, destination = stop.Id, position = point,
                        route = new[] { point + stop.Heading * 4f, point }, roadLegs = new bool[2], TerrainRoute = true, speed = PatrolsModule.TrafficConvoySpeed.Value / 3.6f, vehicles = new[] { Freeze(t) } });
                    TownsApi.Provider.MerchantSeeded(stop.Id); stop.MerchantSeeded = true;
                    break; // One bus record per scheduler tick, never a construction burst.
                }
            }
        }
        private void PopulateTownConvoys()
        {
            if (!TownTravel || RoleCount(1) >= PatrolsModule.MaxConvoys.Value || _journeys.Count(j => !j.townParked && !j.townResident) >= PatrolsModule.TrafficMaxGroups.Value) return;
            var group = Convoy.TrafficGroup(true); if (group == null) return;
            var roster = EditorStore.ComposeFull(group, true, () => UnityEngine.Random.value);
            if (roster.Count > 0 && roster.Any(t => t.IsTruck) && roster.All(t => t.Available)) QueueTownDeparture(group, 1, roster);
        }
        private bool TownVisit(TrafficJourney j, float seconds, Transform player)
        {
            if (!j.townBound || !j.arrived) return false;
            if (TownPlanning(j.id)) return true;
            bool inArea = SameMapArea(j.position);
            j.unseenSeconds = inArea ? 0f : j.unseenSeconds + seconds;
            if (j.townVisitSeconds <= 0f) j.townVisitSeconds = UnityEngine.Random.Range(600f, 900f);
            if (j.townParked)
            {
                _live.TryGetValue(j.id, out var parked);
                if (parked != null && !parked.Building && parked.Cars.Count == 0 && Time.time >= parked.RetryAt)
                { _live.Remove(j.id); parked = null; }
                if (parked != null && !parked.Building) ParkConvoyCrew(j, parked);
                if (j.unseenSeconds >= j.townVisitSeconds) { RemoveParkedTown(j); return true; }
                // Offscreen arrival has a truck/crew record; build it when the city is approached.
                if (parked == null && TrafficLocations.FlatDistance(j.position, TrafficSpace.World(player.position)) <= PatrolsModule.TrafficLoadRange.Value
                    && RealCars < PatrolsModule.TrafficMaxRealCars.Value && !Convoy.AutosaveSoon(30f))
                { var next = new Live(); _live[j.id] = next; StartCoroutine(Materialize(j, next)); }
                return true;
            }
            if (j.convoy)
            {
                if (_live.TryGetValue(j.id, out var arriving) && arriving.Building) return true;
                j.townParked = true; j.destinationSeconds = 0f;
                // No escort cars remain at a settled city, including a virtual arrival.
                if (arriving == null) for (int i = 0; i < j.vehicles.Length; i++) if (!j.vehicles[i].truck) j.vehicles[i].retired = true;
                if (arriving != null) ParkConvoyCrew(j, arriving);
                Core.Log.Always("[Patrols] Convoy parked at " + j.destination + "; no longer counted as travelling; unattended timeout " + (j.townVisitSeconds / 60f).ToString("0.0") + " minutes");
                return true;
            }
            if (!j.merchant) return false;
            j.destinationSeconds += seconds;
            j.townResident = true;
            if (!PatrolsModule.Merchants.Value || PatrolsModule.MerchantSpawnChance.Value <= 0f || RoleCount(2) >= PatrolsModule.MaxMerchants.Value) return false;
            if (j.vehicles.Length > 0 && MerchantTypeInUse(MerchantCatalog.Selected(j.vehicles[0].merchantTypes))) return false;
            float retry;
            if (j.destinationSeconds >= j.townVisitSeconds && (!_townRetry.TryGetValue(j.id, out retry) || Time.time >= retry))
            {
                RefreshTownStops();
                var next = _townStops.Where(s => s.CanTravel && s.Faction == "Coyotes" && s.Id != j.destination && !ParkingBusy(s.Id, j.id)).OrderBy(s => TrafficLocations.FlatDistance(s.Parking, j.position)).Take(4).ToArray();
                var source = _townStops.FirstOrDefault(s => s.Id == j.destination);
                if (source != null && next.Length > 0)
                {
                    var target = next[UnityEngine.Random.Range(0, next.Length)];
                    _townDepartures.Add(new TownDeparture { Journey = j, Source = source, Destination = target, Existing = true, Job = RoadPlanner.RequestRoute(source.Gate, target.Gate, true) });
                }
            }
            return false;
        }
        private void ParkConvoyCrew(TrafficJourney j, Live live)
        {
            foreach (var pair in live.Cars.ToArray())
            {
                var car = pair.Value; if (car == null) continue;
                var marker = car.GetComponent<PatrolMarker>();
                if (!j.vehicles[pair.Key].truck)
                {
                    if (marker != null && !marker.PlayerEntered)
                    {
                        var crew = car.GetComponent<Crew>(); if (crew != null) crew.ParkForTown();
                        // Give the authored door/hop sequence time to release its existing occupants.
                        if (crew != null && (Time.time - crew.TownParkedAt < 3f || crew.TownCrewSeated)) continue;
                        CollectTownGuards(j, crew);
                        CrewReturn.SettleTown(crew);
                        Cleanup.Remove(marker, "convoy escort crew now guards the parked truck");
                    }
                    j.vehicles[pair.Key].retired = true;
                    live.Cars.Remove(pair.Key); continue;
                }
                var truckCrew = car.GetComponent<Crew>(); if (truckCrew != null) truckCrew.ParkForTown();
                CollectTownGuards(j, truckCrew);
                if (truckCrew != null && !truckCrew.TownCrewSeated) CrewReturn.SettleTown(truckCrew);
            }
            foreach (string name in j.townGuards ?? new string[0])
            {
                GameObject body = TownGuard(name); if (body == null || QuestFollowers.IsHired(body)) continue;
                float due; if (_townGuardOrders.TryGetValue(name, out due) && Time.time < due) continue;
                _townGuardOrders[name] = Time.time + 5f;
                if (Core.Services.Npc != null && Core.Services.Npc.IsIdle(body) && (body.transform.position - TrafficSpace.Scene(j.position)).sqrMagnitude > 144f)
                {
                    float angle = (TownPlanner.Hash(name) % 360) * Mathf.Deg2Rad;
                    Core.Services.Npc.Travel(body, TrafficSpace.Scene(j.position) + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 7f, 3f);
                }
            }
        }
        private void CollectTownGuards(TrafficJourney j, Crew crew)
        {
            if (crew == null || crew.Regroup == null) return;
            var names = new HashSet<string>(j.townGuards ?? new string[0], StringComparer.Ordinal);
            foreach (var member in crew.Regroup.Members)
                if (member.Body != null && (member.Health == null || member.Health.Value > 0f)) { names.Add(member.Body.name); _townGuards[member.Body.name] = member.Body; _townGuardJourneys[member.Body.name] = j.id; }
            j.townGuards = names.ToArray();
        }
        private GameObject TownGuard(string name)
        {
            GameObject body; return _townGuards.TryGetValue(name, out body) ? body : null;
        }
        private void RemoveParkedTown(TrafficJourney j)
        {
            foreach (string name in j.townGuards ?? new string[0])
            {
                var body = TownGuard(name); if (body == null || QuestFollowers.IsHired(body)) continue;
                Register.RemoveTree(body); Destroy(body);
            }
            foreach (string name in j.townGuards ?? new string[0]) { _townGuards.Remove(name); _townGuardJourneys.Remove(name); _townGuardOrders.Remove(name); }
            if (_live.TryGetValue(j.id, out var live)) foreach (var car in live.Cars.Values)
                if (car != null)
                {
                    if (car.GetComponent<Crew>() != null) car.GetComponent<Crew>().TownParking = false;
                    if (car.GetComponent<PatrolMarker>()?.PlayerEntered == true) Detach(car);
                }
            Remove(j);
        }
    }
}
