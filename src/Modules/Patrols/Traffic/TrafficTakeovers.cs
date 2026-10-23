using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    public sealed partial class PatrolsModule
    {
        private static void OnCampOwnershipChanged(ref Core.Events.CampOwnershipChanged change)
        { WorldTraffic.CampCaptured(change); }
    }

    internal sealed partial class WorldTraffic
    {
        private readonly HashSet<string> _takeoverWaiting = new HashSet<string>();

        internal static void CampCaptured(Core.Events.CampOwnershipChanged change)
        {
            if (change.NewFaction != "Coyotes" || change.OldFaction == change.NewFaction || string.IsNullOrEmpty(change.CampId)) return;
            if (_instance == null) { Core.Log.Warn(null, "[Patrols] Takeover reinforcement unavailable: world traffic is not running."); return; }
            string id = "takeover:" + change.CampId;
            if (Find(id) != null) return;
            var pool = CrewFactions.Travelers("Coyotes");
            var walkers = new TrafficWalker[UnityEngine.Random.Range(2, MaxWalkers + 1)];
            for (int i = 0; i < walkers.Length; i++) walkers[i] = new TrafficWalker { identity = pool[UnityEngine.Random.Range(0, pool.Length)] };
            _instance._journeys.Add(new TrafficJourney { id = id, name = FootName("Coyotes", null), faction = "Coyotes", source = "", destination = change.CampId,
                foot = true, reinforcement = true, route = new[] { change.WorldPos, change.WorldPos }, roadLegs = new bool[2], leg = 1, position = change.WorldPos,
                speed = PatrolsModule.FootSpeed.Value, walkers = walkers, vehicles = NoVehicles });
            Core.Log.Always(null, "[Patrols] Takeover travelers queued for " + change.CampId + ": " + walkers.Length + " Coyotes.");
        }

        // The request is already a saved journey while discovery or a usable source camp is unavailable.
        private void PrepareTakeovers()
        {
            TrafficLocations.Tick();
            foreach (var journey in _journeys)
            {
                if (!journey.foot || !journey.reinforcement || journey.arrived || !string.IsNullOrEmpty(journey.source)) continue;
                TrafficBase destination; if (!TrafficLocations.Known.TryGetValue(journey.destination, out destination)) continue;
                TrafficBase source = null; float best = float.MaxValue;
                foreach (var camp in TrafficLocations.Known.Values)
                {
                    if (camp.id == destination.id || camp.cleared || camp.faction != "Coyotes") continue;
                    float distance = TrafficLocations.FlatDistance(camp.center, destination.center);
                    if (distance < best) { best = distance; source = camp; }
                }
                if (source == null)
                { if (_takeoverWaiting.Add(journey.id)) Core.Log.Always(null, "[Patrols] Takeover travelers waiting for a known Coyote source camp: " + destination.id); continue; }
                journey.source = source.id; journey.position = source.apron; journey.route = new[] { source.apron, destination.apron };
                journey.roadLegs = new bool[2]; journey.leg = 1; journey.TerrainRoute = false;
                _routeAttempts.Remove(journey.id); _takeoverWaiting.Remove(journey.id);
                Core.Log.Always(null, "[Patrols] Takeover travelers: " + source.id + " -> " + destination.id + "; nearest Coyote camp, " + best.ToString("0") + " m.");
            }
        }

        private static bool TakeoverIcon(TrafficJourney journey)
        { return !journey.foot || !journey.reinforcement || (!string.IsNullOrEmpty(journey.source) && journey.TerrainRoute && !journey.arrived); }

        private static bool TakeoverSpawned(TrafficJourney journey, FootLive live)
        {
            if (!journey.reinforcement) return true;
            for (int i = 0; i < journey.walkers.Length; i++) if (!journey.walkers[i].dead && !live.Spawned[i]) return false;
            return true;
        }
    }
}
