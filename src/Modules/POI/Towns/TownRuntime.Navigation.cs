using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Registers POI geometry readiness and delegates NPC movement decisions to NPCAI.</summary>
    internal sealed partial class TownRuntime
    {
        /// <summary>Registers the POI readiness gate when the NPC navigation provider exists.</summary>
        partial void InitNavigation(){ModApi.Changed+=NavigationChanged;RefreshNavigation();RefreshRouteHints();}
        /// <summary>Removes the POI gate and provider-change subscription.</summary>
        partial void ShutdownNavigation(){ModApi.Changed-=NavigationChanged;ModApi.Get<ITownNavigation>()?.RemovePoiGate(Owner);if(_roadRoutes!=null)_roadRoutes.RemoveHintProvider(_routeHint);_roadRoutes=null;_routeHint=0;}
        /// <summary>Rebinds construction navigation when its actual provider changes.</summary>
        private void NavigationChanged(System.Type type){if(type==typeof(ITownNavigation))RefreshNavigation();if(type==typeof(IVehicleRouting))RefreshRouteHints();}
        /// <summary>Recreates live routing and overlays after a navigation-provider transition.</summary>
        private void RefreshNavigation()
        {var api=ModApi.Get<ITownNavigation>();api?.RegisterPoiGate(Owner,PoiDressing.MapGate,new PoiExitWaypoints());foreach(var live in _sites){var s=live.Site;s.Overlap?.Dispose();s.Overlap=null;s.Navigation=null;if(api==null||s.Streets==null)continue;var context=TownNavSnapshot.Of(s);s.Overlap=api.CreateOverlay(context,s.Walls!=null);s.Navigation=api.CreateRouting(context);live.Finished=false;live.PreparedAt=Time.unscaledTime;s.PrepSteps=0;s.NavigationStopped=false;}}
        private sealed class PoiExitWaypoints:IWaypointProvider{public bool TryNext(GameObject actor,Vector3 from,Vector3 goal,out Vector3 point)=>PoiDressing.Exit(from,goal,out point);}
    }
}
