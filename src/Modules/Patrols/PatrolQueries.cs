using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Exposes patrol and journey facts without leaking private runtime components to consumers.</summary>
    internal sealed class PatrolQueries : IPatrols
    {
        private readonly QuestFollowers _followers = new QuestFollowers();
        /// <summary>Recognizes seated, dismounted and settled patrol crew through the existing identity rules.</summary>
        public bool IsCrew(GameObject npc) { return _followers.IsCrew(npc); }
        /// <summary>Uses the follower seat provider's existing active-pose rule.</summary>
        public bool IsSeated(GameObject npc) { return _followers.IsSeated(npc); }
        /// <summary>Reports live combat under the same proximity and surviving-crew conditions as save deferral.</summary>
        public bool VehicleCombatActive { get { return PatrolsModule.CombatActive; } }
        /// <summary>Returns a session identity for a known journey, with -1 for unmanaged vehicles.</summary>
        public int JourneyOf(GameObject vehicle)
        {
            var agent = WorldTraffic.AgentOf(vehicle);
            return agent != null ? WorldTraffic.JourneyNumber(agent.Journey) : -1;
        }
        /// <summary>True only for a root carrying the owned patrol marker.</summary>
        public bool IsPatrolVehicle(GameObject vehicle) { return vehicle != null && vehicle.GetComponent<PatrolMarker>() != null; }
        /// <summary>Counts physical patrol roots around a world-space point without allocating a scene scan.</summary>
        public int PatrolCount(Vector3 world, float radius)
        {
            int count = 0; float squared = radius * radius;
            foreach (var marker in PatrolMarker.All)
                if (marker != null && marker.gameObject.activeInHierarchy && (Origin.World(marker.transform.position) - world).sqrMagnitude <= squared) count++;
            return count;
        }
        /// <summary>Includes every current virtual or materialized journey.</summary>
        public int JourneyCount { get { return WorldTraffic.JourneyCount; } }
        /// <summary>Uses the patrol faction policy shared by crew and traffic spawning.</summary>
        public bool IsPlayerHostile(string faction) { return TrafficFactions.PlayerHostile(faction); }
    }

    /// <summary>Owns session-only integer journey handles without changing persisted GUID identities.</summary>
    internal sealed partial class WorldTraffic
    {
        /// <summary>Returns the current total with no dependency on a rendering or map implementation.</summary>
        internal static int JourneyCount { get { return _instance != null ? _instance._journeys.Count : 0; } }
        /// <summary>Allocates one stable session handle only for a journey still present in the authoritative index.</summary>
        internal static int JourneyNumber(string id)
        {
            return _instance != null ? _instance._journeys.Number(id) : -1;
        }
    }
}
