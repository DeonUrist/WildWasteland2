using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Route/marker data for the map layer (journey map names, route colours, map detail switches) and off-map entry/exit points of traffic; Patrols part (Wave A split of Traffic/TrafficMap.cs).</summary>
    internal sealed partial class WorldTraffic
    {
        private Vector3 OffscreenTrafficPoint(Vector3 center,Vector3 direction)
        {
            // Route creation only; include the inactive native camera when M is closed.
            if(_gameMap==null)foreach(var camera in Resources.FindObjectsOfTypeAll<Camera>())
                if(camera.name=="MapCheat_Camera"&&camera.gameObject.scene.IsValid()){_gameMap=camera;break;}
            direction.y=0f;direction.Normalize();
            float reach=Mathf.Max(PatrolsModule.TrafficUnloadRange.Value,PatrolsModule.TrafficLoadRange.Value)+250f;
            if(_gameMap==null)return center+direction*(reach+100f);
            var plane=new Plane(Vector3.up,TrafficSpace.Scene(center));
            var corners=new Vector2[4];
            for(int corner=0;corner<4;corner++)
            {
                var ray=_gameMap.ViewportPointToRay(new Vector3(corner==1||corner==2?1f:0f,corner>=2?1f:0f,0f));
                if(!plane.Raycast(ray,out float distance))return center+direction*(reach+100f);
                var world=TrafficSpace.World(ray.GetPoint(distance));corners[corner]=new Vector2(world.x,world.z);
            }
            // CleanHUD's map zoom shrinks the camera view while the map is open: spawn points stay where the native (unzoomed) view ends
            var mapApi=Core.Services.Map;
            if(mapApi!=null&&mapApi.Open)
            {
                float zoom=mapApi.Zoom;
                if(zoom>0.05f&&zoom<0.999f)
                {
                    var mid=_gameMap.ViewportPointToRay(new Vector3(.5f,.5f,0f));
                    if(plane.Raycast(mid,out float midDistance))
                    {
                        var c=TrafficSpace.World(mid.GetPoint(midDistance));var c2=new Vector2(c.x,c.z);
                        for(int corner=0;corner<4;corner++)corners[corner]=c2+(corners[corner]-c2)/zoom;
                    }
                }
            }
            reach=MapExitDistance(new Vector2(center.x,center.z),new Vector2(direction.x,direction.z),corners);
            return center+direction*(reach+100f);
        }
        internal static float MapExitDistance(Vector2 origin,Vector2 direction,Vector2[] corners)
        {
            float exit=0f;
            for(int i=0;i<corners.Length;i++)
            {
                var a=corners[i]-origin;var edge=corners[(i+1)%corners.Length]-corners[i];
                float cross=direction.x*edge.y-direction.y*edge.x;if(Mathf.Abs(cross)<.0001f)continue;
                float t=(a.x*edge.y-a.y*edge.x)/cross,u=(a.x*direction.y-a.y*direction.x)/cross;
                if(t>=0f&&u>=0f&&u<=1f)exit=Mathf.Max(exit,t);
            }
            return exit;
        }

        internal static string MapName(TrafficJourney journey,bool reveal=false)
        {bool known=reveal||TrafficCompass.Known(journey);return MapUnknown&&!known?"Unknown":(MapNames||known)&&!string.IsNullOrEmpty(journey.name)?journey.name:journey.merchant?"Merchant":journey.convoy?"Convoy":"Patrol";}
        // Map Routes and World traffic markers each use Off / Ambiguous / On for route lines and moving dots + names respectively.
        // Traffic ambiguity is independent of location markers. Without CleanHUD's map service the old debug switches decide both.
        private static bool RoutesOn {get{var m=Core.Services.Map;return m!=null?m.Routes!=Core.MapDetail.Off:PatrolsModule.TrafficDebugMap.Value;}}
        private static bool MarkersOn {get{var m=Core.Services.Map;return m!=null?m.TrafficMarkers!=Core.MapDetail.Off:PatrolsModule.TrafficDebugMap.Value;}}
        private static bool InkUnknown {get{var m=Core.Services.Map;return m!=null?m.Routes==Core.MapDetail.Ambiguous:PatrolsModule.HideRouteType.Value;}}
        private static bool MapUnknown {get{var m=Core.Services.Map;return m!=null?m.TrafficMarkers==Core.MapDetail.Ambiguous:PatrolsModule.HideRouteType.Value;}}
        private static bool MapNames {get{var m=Core.Services.Map;return m!=null?m.TrafficMarkers!=Core.MapDetail.Ambiguous:PatrolsModule.RealPatrolNames.Value;}}
        internal static Color32 RouteColor(TrafficJourney journey,bool unknown)
        {
            if(unknown)return new Color32(150,150,150,255);
            if(journey.merchant)return new Color32(255,220,50,255);
            if(TrafficFactions.Normalize(journey.faction)=="Coyotes")return journey.convoy?new Color32(110,210,255,255):new Color32(80,220,100,255);
            return journey.convoy?new Color32(255,150,40,255):new Color32(255,80,80,255);
        }
    }
}
