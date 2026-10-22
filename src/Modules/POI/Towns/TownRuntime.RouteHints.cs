using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Publishes town-interior exclusion and exterior destination hints to the road-routing owner.</summary>
    internal sealed partial class TownRuntime
    {
        private IVehicleRouting _roadRoutes;
        private int _routeHint;
        /// <summary>Rebinds the owned route hint when the actual Patrols routing provider changes.</summary>
        private void RefreshRouteHints()
        {var next=ModApi.Get<IVehicleRouting>();if(ReferenceEquals(next,_roadRoutes))return;if(_roadRoutes!=null)_roadRoutes.RemoveHintProvider(_routeHint);_roadRoutes=next;_routeHint=next==null?0:next.RegisterHintProvider(Owner,new TownRouteHints(this),0);}
        /// <summary>Exposes numeric town policy without sharing plans with the route owner.</summary>
        private sealed class TownRouteHints:IRouteHintProvider
        {
            private readonly TownRuntime _towns;
            /// <summary>Binds the currently running town runtime.</summary>
            internal TownRouteHints(TownRuntime towns){_towns=towns;}
            /// <summary>Invalidates certified routes when town geometry or availability changes.</summary>
            public int Revision=>_towns.TravelRevision;
            /// <summary>Redirects a requested interior endpoint to the committed exterior parking lot.</summary>
            public bool Adjust(Vector3 from,Vector3 to,List<Vector3> route)
            {Vector3 parking;if(route.Count==0||!_towns.ParkingOutside(to,out parking)||(route[route.Count-1]-parking).sqrMagnitude<.01f)return false;route[route.Count-1]=parking;return true;}
            /// <summary>Rejects vehicle travel through the known town interior.</summary>
            public bool IsBlocked(Vector3 world){Vector3 parking;return _towns.ParkingOutside(world,out parking);}
        }
    }
}
