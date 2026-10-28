using System;
using System.Linq;
using UnityEngine;
namespace WildWasteland.Modules.Patrols
{
    internal sealed partial class WorldTraffic
    {
        /// <summary>Populates roaming for the current traffic roaming.</summary>
        private void PopulateRoaming(Vector3 player)
        {
            if(RoleCount(0,"Coyotes")>=PatrolsModule.MaxCoyotePatrols.Value||PopulationCount>=PatrolsModule.TrafficMaxGroups.Value
                ||_nearDepartures.Any(d=>d.Role==0&&TrafficFactions.Normalize(d.Faction)=="Coyotes"))return;
            var group=Convoy.TrafficGroup(false,"Coyotes");if(group==null)return;
            QueueNearby(group,0);
        }
    }
}
