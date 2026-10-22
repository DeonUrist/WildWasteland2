using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Supplies actual traffic decisions and tuning to NPCAI without sharing marker or journey objects.</summary>
    internal sealed class OccupantCombatPolicy : IOccupantCombatPolicy
    {
        public bool Running => PatrolsModule.Running;
        public bool Ranged => PatrolsModule.RangedCombat.Value;
        public float FireArc => PatrolsModule.FireArc.Value;
        public float ShootDistance => PatrolsModule.ShootDistance.Value;
        public float BurstSeconds => PatrolsModule.FireBurstSeconds.Value;
        public float IntervalMin => PatrolsModule.FireIntervalMin.Value;
        public float IntervalMax => PatrolsModule.FireIntervalMax.Value;
        public float MaxAimPitch => PatrolsModule.MaxAimPitch.Value;
        public float AimTurnSpeed => PatrolsModule.AimTurnSpeed.Value;
        public float BikeArc => Rider.BikeArc;
        /// <summary>Preserves convoy driver passivity, turret enablement and actual traffic combat policy.</summary>
        public bool Allowed(GameObject car,bool driver,bool turret)
        {var traffic=WorldTraffic.AgentOf(car);if(traffic!=null&&driver&&traffic.Truck&&WorldTraffic.Find(traffic.Journey)?.convoy==true)return false;return Ranged&&(!turret||PatrolsModule.RiderLances.Value)&&WorldTraffic.CombatAllowed(car);}
        /// <summary>Distinguishes traffic targets from native caller-independent targets.</summary>
        public bool HasTraffic(GameObject car)=>car!=null&&car.GetComponent<PatrolMarker>()!=null;
        /// <summary>Returns the target chosen by current faction and explicit attack rules.</summary>
        public GameObject Target(GameObject car)=>TrafficFactions.ForCar(car,car.GetComponent<PatrolMarker>());
        /// <summary>Retains player-contact time for normal dismount awareness handover.</summary>
        public void PlayerContact(GameObject car){var marker=car!=null?car.GetComponent<PatrolMarker>():null;if(marker!=null)marker.PlayerContactAt=Time.time;}
    }
}
