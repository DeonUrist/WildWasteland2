using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Registers POI geometry readiness and delegates NPC movement decisions to NPCAI.</summary>
    internal sealed partial class TownRuntime
    {
        partial void InitNavigation(){ModApi.Changed+=NavigationChanged;RefreshNavigation();}
        partial void ShutdownNavigation(){ModApi.Changed-=NavigationChanged;ModApi.Get<ITownNavigation>()?.RemovePoiGate(Owner);}
        private void NavigationChanged(System.Type type){if(type==typeof(ITownNavigation))RefreshNavigation();}
        private void RefreshNavigation(){ModApi.Get<ITownNavigation>()?.RegisterPoiGate(Owner,PoiDressing.MapGate,new PoiExitWaypoints());}
        private sealed class PoiExitWaypoints:IWaypointProvider{public bool TryNext(GameObject actor,Vector3 from,Vector3 goal,out Vector3 point)=>PoiDressing.Exit(from,goal,out point);}
    }
}
