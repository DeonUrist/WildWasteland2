using System;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Player perception of a traffic car (sight and engine hearing); NPCAI perception query in Wave B (Wave A split of Patrols Traffic/WorldTraffic.cs).</summary>
    internal sealed partial class TrafficAgent
    {
        private float _nextSense;

        /// <summary>Patrol sight (range by day/lights, 100 degree cone, raycast) and engine hearing of the player every 0.5 s; updates LastKnown/SeenUntil/Detection and the crew contact time.</summary>
        partial void SensePlayer(
            TrafficJourney journey,   // refs-ok: REFACTOR-REQUEST B07-12 - part of a Patrols traffic type, B11 moves callers to INpcControl.CanPerceivePlayer
            bool hasPlayer, Vector3 player, GameObject car)
        {
            if (Time.time >= _nextSense)
            {
                _nextSense = Time.time + 0.5f;
                bool saw = false, heard = false;
                if (hasPlayer && !journey.convoy)
                {
                    var delta = player - transform.position; delta.y = 0f;
                    float sight = TrafficRules.VisibilityRange(Night(), PatrolVisibility.LightsOn(car));   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
                    saw = delta.magnitude <= sight && Vector3.Angle(transform.forward, delta) <= 100f
                        && Sight(player, car);
                    heard = Night() && delta.magnitude <= TrafficLocations.EngineRange(car);
                    if (saw || heard)
                    {
                        LastKnown = TrafficSpace.World(player); SeenUntil = Time.time + PatrolsModule.PatrolMemorySeconds.Value;   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
                        if (_pm == null) _pm = GetComponent<PatrolMarker>();   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
                        if (_pm != null) _pm.PlayerContactAt = Time.time;   // awareness handover at dismount (CrewReturn)
                    }
                }
                Detection = saw ? "sight" : heard ? "engine" : Time.time < SeenUntil ? "last known" : "travelling";
                _detected = saw || heard;
            }
        }

        /// <summary>Line of sight from the car roof to the player (own car, the player and his car ignored); false on a blocker or a full hit buffer.</summary>
        private bool Sight(Vector3 target, GameObject playerCar)
        {
            var origin = transform.position + Vector3.up * 2f;
            var delta = target + Vector3.up - origin;
            int count=Physics.RaycastNonAlloc(origin,delta.normalized,SightHits,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==SightHits.Length)return false;
            for(int i=0;i<count;i++)
            {
                var hit=SightHits[i];
                var t = hit.collider.transform;
                if (t.IsChildOf(transform) || (playerCar != null && t.IsChildOf(playerCar.transform))
                    || (Core.PlayerState.Transform != null && t.IsChildOf(Core.PlayerState.Transform))) continue;
                return false;
            }
            return true;
        }

        private static readonly RaycastHit[] SightHits=new RaycastHit[24];

        /// <summary>Night for patrol sensing: the Survival day-light query, the Survival DayLight rule until Survival provides IDayLight.</summary>
        private static bool Night()
        {
            var light = Core.ModApi.Get<Core.IDayLight>();
            return light != null ? light.Night : DayLight.Dark;   // refs-ok: REFACTOR-REQUEST B07-12 (fallback until B14 provides IDayLight)
        }
    }
}
