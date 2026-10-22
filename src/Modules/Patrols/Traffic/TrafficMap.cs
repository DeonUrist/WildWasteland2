using UnityEngine;
namespace WildWasteland.Modules.Patrols
{
    internal sealed partial class WorldTraffic
    {
        private Camera _gameMap;
        private RectTransform _gameMapImage;
        private PlayMakerFSM _mapCheat;
        private float _nextMapScan,_gameInkAt;
        private readonly TrafficMapInk _gameInk=new TrafficMapInk();
        private struct MapLabel { internal Rect rect; internal Color color; internal string text; internal bool foot; }
        private readonly System.Collections.Generic.List<MapLabel> _mapLabels=new System.Collections.Generic.List<MapLabel>();
        private readonly System.Collections.Generic.List<Rect> _mapBattles=new System.Collections.Generic.List<Rect>();
        private Vector2 _gameInkSize;
        private bool _inkNames,_inkHide,_mapWasOpen,_lblHide,_inkOn,_lblOn;
        private readonly Vector3[] _mapCorners=new Vector3[4];
        private Texture2D _skull;
        private Texture2D _mapDot;
        private GUIStyle _mapLabelStyle;
        private GUIStyle _rigNoticeStyle;
        private GUIStyle _rigXStyle;
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
        // The Gui job's predicate, Gui's branches without drawing: true for the War Rig notice, while the native map (M) is open with
        // routes or rigs to ink, while a 5 s map-reference rescan is due (or the map just opened without them), and for the one call that
        // resets _gameInkAt / _mapWasOpen after the map closed or traffic went off. Destroyed rigs are still pruned there, a bit later.
        internal bool GuiWanted()
        {
            bool inGame=Core.GameRefs.InGame;float now=Time.unscaledTime;
            if(inGame&&now<WarRig.WorldSpawning.NaturalNoticeUntil)return true;
            var mapApi=Core.Services.Map;
            bool traffic=PatrolsModule.ConvoyEnabled.Value&&(RoutesOn||MarkersOn);
            bool reset=_gameInkAt!=0f||_mapWasOpen;
            if((!traffic&&WarRig.WorldSpawning.NaturalRigs.Count==0)||!inGame)return reset;
            bool mapOpen=_mapCheat!=null&&_mapCheat.isActiveAndEnabled&&_mapCheat.ActiveStateName=="on";
            if((_gameMap==null||_gameMapImage==null||_mapCheat==null)&&(now>=_nextMapScan||(mapOpen&&!_mapWasOpen)))return true;
            return mapOpen||reset;
        }
        // WW: was OnGUI (Gui job)
        internal void Gui()
        {
            if(Event.current.type!=EventType.Repaint)return;
            if(Core.GameRefs.InGame&&Time.unscaledTime<WarRig.WorldSpawning.NaturalNoticeUntil)
            {
                if(_rigNoticeStyle==null)_rigNoticeStyle=new GUIStyle(GUI.skin.box){fontSize=26,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.yellow}};
                GUI.Box(new Rect((Screen.width-420f)*.5f,40f,420f,55f),"WAR RIG SPAWNED",_rigNoticeStyle);
            }
            var rigs=WarRig.WorldSpawning.NaturalRigs;
            for(int i=rigs.Count-1;i>=0;i--)if(rigs[i]==null)rigs.RemoveAt(i);
            var mapApi=Core.Services.Map;
            bool traffic=PatrolsModule.ConvoyEnabled.Value&&(RoutesOn||MarkersOn);
            if((!traffic&&rigs.Count==0)||!Core.GameRefs.InGame)
            {_gameInkAt=0f;_mapWasOpen=false;return;}
            bool mapOpen=_mapCheat!=null&&_mapCheat.isActiveAndEnabled&&_mapCheat.ActiveStateName=="on";
            if((_gameMap==null||_gameMapImage==null||_mapCheat==null)&&(Time.unscaledTime>=_nextMapScan||(mapOpen&&!_mapWasOpen)))
            {
                _nextMapScan=Time.unscaledTime+5f;
                if(_gameMap==null){var camera=GameObject.Find("MapCheat_Camera");_gameMap=camera!=null?camera.GetComponent<Camera>():null;}
                if(_mapCheat==null)_mapCheat=Core.FsmUtil.Find(Core.GameRefs.GameManager,"MapCheat");
                if(_gameMapImage==null&&_mapCheat!=null)_gameMapImage=FindMapImage();
            }
            _mapWasOpen=_mapCheat!=null&&_mapCheat.isActiveAndEnabled&&_mapCheat.ActiveStateName=="on";
            if(!_mapWasOpen||_gameMap==null||_gameMapImage==null||!_gameMapImage.gameObject.activeInHierarchy)
            {_gameInkAt=0f;return;}
            _gameMapImage.GetWorldCorners(_mapCorners);
            var rect=new Rect(_mapCorners[0].x,Screen.height-_mapCorners[1].y,_mapCorners[2].x-_mapCorners[0].x,_mapCorners[1].y-_mapCorners[0].y);
            if(rect.width<1f||rect.height<1f)return;
            var old=GUI.color;GUI.BeginGroup(rect);
            bool inkOn=RoutesOn,lblOn=MarkersOn;
            if(traffic&&(Time.unscaledTime>=_gameInkAt||_gameInkSize!=rect.size||_inkNames!=MapNames||_inkHide!=InkUnknown||_lblHide!=MapUnknown||_inkOn!=inkOn||_lblOn!=lblOn))
            {
                bool first=_gameInkAt<=0f||_gameInkSize!=rect.size||_inkNames!=MapNames||_inkHide!=InkUnknown||_lblHide!=MapUnknown||_inkOn!=inkOn||_lblOn!=lblOn;
                _gameInkAt=Time.unscaledTime+1f;_gameInkSize=rect.size;
                _inkNames=MapNames;_inkHide=InkUnknown;_lblHide=MapUnknown;_inkOn=inkOn;_lblOn=lblOn;
                // Perf: the route texture is redrawn only when its inputs change (journeys/routes/colours, map camera, size, options),
                // while the fog is being revealed around a moving player, and every 10 s as a safety net (was every second); labels stay 1 Hz
                if(inkOn&&InkChanged(mapApi,first))
                {
                var canvas=new Rect(0,0,Mathf.Min(512f,rect.width),Mathf.Min(512f,rect.height));
                _gameInk.Begin(Mathf.RoundToInt(canvas.width),Mathf.RoundToInt(canvas.height));
                foreach(var journey in _journeys)if(!journey.foot)for(int i=1;i<journey.route.Length;i++)   // foot patrols: never inked
                    InkSegment(mapApi,journey.route[i-1],journey.route[i],canvas,RouteColor(journey,InkUnknown));
                _gameInk.End();
                }
                _mapLabels.Clear();_mapBattles.Clear();
                // World traffic markers (their own detail setting, fog of war respected; "Unknown" with traffic Ambiguous): moving dots + names
                // of every traffic group, cars and foot travelers alike. Map Routes only draws the route lines above.
                if(lblOn)
                {
                foreach(var journey in _journeys)if(!Fogged(mapApi,journey.position))
                CacheMapLabel(journey.position,rect,RouteColor(journey,MapUnknown&&!TrafficCompass.Known(journey)),MapName(journey),journey.foot);
                if(first&&Core.Log.Verbose)LogFootDots(mapApi,rect);
                foreach(var camp in TrafficLocations.Known.Values)if(camp.cleared&&mapApi==null)
                CacheMapLabel(camp.center,rect,Color.gray,camp.name+" [Cleared]");
                foreach(var battle in _battles)
                {if(Fogged(mapApi,battle.position))continue;var p=MapPoint(battle.position,rect);_mapBattles.Add(new Rect(p.x-9f,p.y-9f,18f,18f));}
                foreach(var merchant in MerchantHost.All)if(merchant!=null&&!Owns(merchant.GetComponent<PatrolMarker>())&&(mapApi==null||!mapApi.Hidden(merchant.transform.position)))
                {
                bool known=TrafficCompass.Known(TrafficCompass.MerchantKey(merchant));
                CacheMapLabel(TrafficSpace.World(merchant.transform.position),rect,_lblHide&&!known?Color.gray:Color.yellow,_lblHide&&!known?"Unknown":_inkNames||known?merchant.Data?.group??"Coyote merchant":"Merchant");
                }
                }
            }
            if(traffic)
            {
                if(inkOn){GUI.color=Color.white;GUI.DrawTexture(new Rect(0,0,rect.width,rect.height),_gameInk.Texture);}
                foreach(var label in _mapLabels)DrawMapLabel(label);
                GUI.color=Color.white;foreach(var battle in _mapBattles)GUI.DrawTexture(battle,Skull());
            }
            foreach(var rig in rigs)
            {
                if(mapApi!=null&&mapApi.Markers==Core.MapDetail.Off)continue;
                var p=_gameMap.WorldToViewportPoint(rig.transform.position);
                if(p.z<=0f||p.x<0f||p.x>1f||p.y<0f||p.y>1f)continue;
                if(_rigXStyle==null)_rigXStyle=new GUIStyle(GUI.skin.label){fontSize=24,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.yellow}};
                bool unknown=mapApi!=null&&mapApi.Markers==Core.MapDetail.Ambiguous;
                _rigXStyle.normal.textColor=unknown?Color.white:Color.yellow;
                var markerRect=new Rect(p.x*rect.width-12f,(1f-p.y)*rect.height-12f,24f,24f);
                var icon=mapApi!=null?mapApi.GetIcon(unknown?"unknown":"diamond"):null;
                if(icon!=null){GUI.color=unknown?Color.white:Color.yellow;GUI.DrawTexture(markerRect,icon,ScaleMode.ScaleToFit);}
                else{GUI.color=Color.white;GUI.Label(markerRect,unknown?"?":"X",_rigXStyle);}
            }
            GUI.EndGroup();GUI.color=old;
        }
        private long _inkSig;private Vector3 _inkCamPos;private Quaternion _inkCamRot;private float _inkCamSize,_inkCamFov,_inkCamAspect,_inkSafetyAt,_fogMovingUntil;
        private int _inkPlayerX=int.MinValue,_inkPlayerZ=int.MinValue;
        private bool InkChanged(Core.IMapMarkers mapApi,bool first)
        {
            float now=Time.unscaledTime;
            long sig=0;
            foreach(var journey in _journeys)if(!journey.foot)sig++;
            foreach(var journey in _journeys)
            {
                if(journey.foot)continue;   // not inked: their walking never re-inks the routes
                var c=RouteColor(journey,InkUnknown);
                sig=sig*31+(journey.route!=null?System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(journey.route)*7+journey.route.Length:0);
                sig=sig*31+((c.r<<16)|(c.g<<8)|c.b);
            }
            var cam=_gameMap.transform;
            bool camera=cam.position!=_inkCamPos||cam.rotation!=_inkCamRot||_gameMap.orthographicSize!=_inkCamSize||_gameMap.fieldOfView!=_inkCamFov||_gameMap.aspect!=_inkCamAspect;
            bool fog=false;
            if(mapApi!=null)
            {
                // CleanHUD reveals the fog only around the player, fading in over about a second: re-ink while the player moves (10 m cells) and 2 s after
                var player=PlayerRef.Player;
                if(player!=null)
                {
                    var w=TrafficSpace.World(player.position);int x=Mathf.FloorToInt(w.x/10f),z=Mathf.FloorToInt(w.z/10f);
                    if(x!=_inkPlayerX||z!=_inkPlayerZ){_inkPlayerX=x;_inkPlayerZ=z;_fogMovingUntil=now+2f;}
                }
                fog=now<_fogMovingUntil;
            }
            if(!first&&!camera&&!fog&&sig==_inkSig&&now<_inkSafetyAt)return false;
            _inkSig=sig;_inkCamPos=cam.position;_inkCamRot=cam.rotation;_inkCamSize=_gameMap.orthographicSize;_inkCamFov=_gameMap.fieldOfView;_inkCamAspect=_gameMap.aspect;
            _inkSafetyAt=now+10f;return true;
        }
        // With CleanHUD's fog of war on, routes are inked only across explored ground (50 m pieces, judged at their midpoints).
        // diagnostics (verbose, once per map opening): where each foot patrol dot is and why it may not show
        private void LogFootDots(Core.IMapMarkers mapApi,Rect rect)
        {
            var player=PlayerRef.Player;var pw=player!=null?TrafficSpace.World(player.position):Vector3.zero;int n=0;
            foreach(var journey in _journeys)
            {
                if(!journey.foot)continue;n++;var p=MapPoint(journey.position,rect);
                bool inside=p.x>=0f&&p.y>=0f&&p.x<=rect.width&&p.y<=rect.height;
                PatrolsModule.Verbose("Map: foot patrol "+journey.name+" at "+TrafficLocations.FlatDistance(journey.position,pw).ToString("0")+" m from you, map point "+p.x.ToString("0")+","+p.y.ToString("0")+
                    " of "+rect.width.ToString("0")+"x"+rect.height.ToString("0")+(inside?" (on the map)":" (outside the shown area)")+(Fogged(mapApi,journey.position)?", in fog":"")+(MarkersOn?"":", hidden: World traffic markers off"));
            }
            if(n==0)PatrolsModule.Verbose("Map: no foot patrols in world traffic");
        }
        private static bool Fogged(Core.IMapMarkers map,Vector3 world){return map!=null&&map.Hidden(TrafficSpace.Scene(world));}
        private void InkSegment(Core.IMapMarkers map,Vector3 a,Vector3 b,Rect canvas,Color32 color)
        {
            if(map==null){_gameInk.Segment(MapPoint(a,canvas),MapPoint(b,canvas),color);return;}
            int pieces=Mathf.Clamp(Mathf.CeilToInt(TrafficLocations.FlatDistance(a,b)/50f),1,400);
            int start=-1;
            for(int k=0;k<=pieces;k++)
            {
                bool shown=k<pieces&&!Fogged(map,Vector3.Lerp(a,b,(k+.5f)/pieces));
                if(shown&&start<0)start=k;
                else if(!shown&&start>=0){_gameInk.Segment(MapPoint(Vector3.Lerp(a,b,(float)start/pieces),canvas),MapPoint(Vector3.Lerp(a,b,(float)k/pieces),canvas),color);start=-1;}
            }
        }
        private RectTransform FindMapImage()
        {
            // The native FSM holds MapCheatUI even while its hierarchy is inactive.
            foreach(var state in _mapCheat.FsmStates)foreach(var action in state.Actions)
            {
                var activate=action as HutongGames.PlayMaker.Actions.ActivateGameObject;if(activate==null)continue;
                var root=_mapCheat.Fsm.GetOwnerDefaultTarget(activate.gameObject);if(root==null||root.name!="MapCheatUI")continue;
                foreach(var image in root.GetComponentsInChildren<RectTransform>(true))if(image.name=="RawImage_Map")return image;
            }
            return null;
        }
        private void CacheMapLabel(Vector3 world,Rect rect,Color color,string text,bool foot=false)
        {var p=MapPoint(world,rect);_mapLabels.Add(new MapLabel{rect=new Rect(p.x+7f,p.y-13f,260f,26f),color=color,text=text,foot=foot});}
        private void DrawMapLabel(MapLabel label)
        {
            if(_mapLabelStyle==null)_mapLabelStyle=new GUIStyle(GUI.skin.label){fontSize=15,wordWrap=false,normal={textColor=Color.white}};
            if(_mapDot==null)
            {
                _mapDot=new Texture2D(9,9,TextureFormat.RGBA32,false);var pixels=new Color32[81];
                for(int y=0;y<9;y++)for(int x=0;x<9;x++)if((x-4)*(x-4)+(y-4)*(y-4)<=16)pixels[y*9+x]=new Color32(255,255,255,255);
                _mapDot.SetPixels32(pixels);_mapDot.Apply(false,false);
            }
            var r=label.rect;
            if(label.foot)
            {
                GUI.color=Color.black;GUI.DrawTexture(new Rect(r.x-14f,r.y+6f,14f,14f),_mapDot);
                GUI.color=label.color;GUI.DrawTexture(new Rect(r.x-11.5f,r.y+8.5f,9f,9f),_mapDot);
                GUI.color=Color.black;
                GUI.Label(new Rect(r.x-1f,r.y,r.width,r.height),label.text,_mapLabelStyle);GUI.Label(new Rect(r.x+1f,r.y,r.width,r.height),label.text,_mapLabelStyle);
                GUI.Label(new Rect(r.x,r.y-1f,r.width,r.height),label.text,_mapLabelStyle);GUI.Label(new Rect(r.x,r.y+1f,r.width,r.height),label.text,_mapLabelStyle);
                GUI.color=label.color;GUI.Label(r,label.text,_mapLabelStyle);
                return;
            }
            GUI.color=Color.black;GUI.DrawTexture(new Rect(r.x-11.5f,r.y+8.5f,9f,9f),_mapDot);
            GUI.Label(new Rect(r.x-1f,r.y,r.width,r.height),label.text,_mapLabelStyle);GUI.Label(new Rect(r.x+1f,r.y,r.width,r.height),label.text,_mapLabelStyle);
            GUI.Label(new Rect(r.x,r.y-1f,r.width,r.height),label.text,_mapLabelStyle);GUI.Label(new Rect(r.x,r.y+1f,r.width,r.height),label.text,_mapLabelStyle);
            GUI.color=label.color;GUI.DrawTexture(new Rect(r.x-9.5f,r.y+10.5f,5f,5f),_mapDot);GUI.Label(r,label.text,_mapLabelStyle);
        }
        private Vector2 MapPoint(Vector3 world,Rect rect)
        {var p=_gameMap.WorldToViewportPoint(TrafficSpace.Scene(world));return new Vector2(p.x*rect.width,(1f-p.y)*rect.height);}
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
        private Texture2D Skull()
        {
            if(_skull!=null)return _skull;
            string[] rows={"00011111111000","00111111111100","01111111111110","11111111111111","11100111100111","11100111100111","11111100111111","01111100111110","00111111111100","00011011011000","00011011011000"};
            _skull=new Texture2D(14,11,TextureFormat.RGBA32,false);var pixels=new Color32[154];for(int y=0;y<11;y++)for(int x=0;x<14;x++)pixels[(10-y)*14+x]=rows[y][x]=='1'?new Color32(230,230,230,255):new Color32(0,0,0,0);
            _skull.SetPixels32(pixels);_skull.filterMode=FilterMode.Point;_skull.Apply();return _skull;
        }
        private void OnDestroy(){_gameInk.Dispose();if(_skull!=null)Destroy(_skull);if(_mapDot!=null)Destroy(_mapDot);}
    }
}
