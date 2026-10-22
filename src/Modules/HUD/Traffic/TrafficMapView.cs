using UnityEngine;

namespace WildWasteland.Modules.HUD
{
    /// <summary>Traffic drawing on the native map (route ink, group labels, battle skulls, War Rig notice; Gui job); HUD map layer part of WorldTraffic (Wave A split of Patrols Traffic/TrafficMap.cs).</summary>
    internal sealed class TrafficMapView
    {
        private float _snapshotAt;
        private readonly Core.TrafficMapSnapshot _snapshot = new Core.TrafficMapSnapshot();
        /// <summary>Resolves the active patrol map snapshot provider.</summary>
        private Core.ITrafficMapSource Source { get { return Core.ModApi.Get<Core.ITrafficMapSource>(); } }
        /// <summary>Reads the captured route-line visibility option.</summary>
        private bool RoutesOn { get { return _snapshot.Routes; } }
        /// <summary>Reads the captured traffic-label visibility option.</summary>
        private bool MarkersOn { get { return _snapshot.Markers; } }
        /// <summary>Reads whether captured traffic labels reveal names.</summary>
        private bool MapNames { get { return _snapshot.Names; } }
        /// <summary>Reads the captured route ambiguity option.</summary>
        private bool InkUnknown { get { return _snapshot.HideRoutes; } }
        /// <summary>Reads the captured traffic-marker ambiguity option.</summary>
        private bool MapUnknown { get { return _snapshot.HideMarkers; } }
        private Camera _gameMap;
        private RectTransform _gameMapImage;
        private PlayMakerFSM _mapCheat;
        private float _nextMapScan,_gameInkAt;
        private readonly TrafficMapInk _gameInk=new TrafficMapInk();
        /// <summary>Stores map label values for the traffic map layer.</summary>
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

        // The Gui job's predicate, Gui's branches without drawing: true for the War Rig notice, while the native map (M) is open with
        // routes or rigs to ink, while a 5 s map-reference rescan is due (or the map just opened without them), and for the one call that
        // resets _gameInkAt / _mapWasOpen after the map closed or traffic went off. Destroyed rigs are still pruned there, a bit later.
        /// <summary>The Gui job's predicate, Gui's branches without drawing: true for the War Rig notice, while the native map (M) is open with routes or rigs to ink, while a 5 s map-reference rescan is due (or the map just opened without them), and for the one call that resets _gameInkAt / _mapWasOpen after the map closed or traffic went off. Destroyed rigs are still pruned there, a bit later.</summary>
        internal bool GuiWanted()
        {
            bool inGame=Core.GameRefs.InGame;float now=Time.unscaledTime;
            var source = Source; if (source != null && inGame && now >= _snapshotAt) { source.Capture(_snapshot); _snapshotAt = now + .5f; } else if (source == null) _snapshot.Enabled = false;
            if(inGame&&now<(Source != null ? Source.NoticeUntil : 0f))return true;
            var mapApi=Core.Services.Map;
            bool traffic=_snapshot.Enabled&&(RoutesOn||MarkersOn);
            bool reset=_gameInkAt!=0f||_mapWasOpen;
            if((!traffic&&(Source != null ? Source.RigCount : 0)==0)||!inGame)return reset;
            bool mapOpen=_mapCheat!=null&&_mapCheat.isActiveAndEnabled&&_mapCheat.ActiveStateName=="on";
            if((_gameMap==null||_gameMapImage==null||_mapCheat==null)&&(now>=_nextMapScan||(mapOpen&&!_mapWasOpen)))return true;
            return mapOpen||reset;
        }
        // WW: was OnGUI (Gui job)
        /// <summary>Resolves gui for the traffic map layer.</summary>
        internal void Gui()
        {
            if(Event.current.type!=EventType.Repaint)return;
            if(Core.GameRefs.InGame&&Time.unscaledTime<(Source != null ? Source.NoticeUntil : 0f))
            {
                if(_rigNoticeStyle==null)_rigNoticeStyle=new GUIStyle(GUI.skin.box){fontSize=26,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.yellow}};
                GUI.Box(new Rect((Screen.width-420f)*.5f,40f,420f,55f),"WAR RIG SPAWNED",_rigNoticeStyle);
            }
            var source = Source;
            int rigs = source != null ? source.RigCount : 0;
            if (source == null) _snapshot.Enabled = false;
            var mapApi=Core.Services.Map;
            bool traffic=_snapshot.Enabled&&(RoutesOn||MarkersOn);
            if((!traffic&&rigs==0)||!Core.GameRefs.InGame)
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
                foreach(var journey in _snapshot.Journeys)if(!journey.Foot)for(int i=1;i<journey.Route.Length;i++)   // foot patrols: never inked
                    InkSegment(mapApi,journey.Route[i-1],journey.Route[i],canvas,journey.RouteColor);
                _gameInk.End();
                }
                _mapLabels.Clear();_mapBattles.Clear();
                // World traffic markers (their own detail setting, fog of war respected; "Unknown" with traffic Ambiguous): moving dots + names
                // of every traffic group, cars and foot travelers alike. Map Routes only draws the route lines above.
                if(lblOn)
                {
                foreach(var journey in _snapshot.Journeys)if(!Fogged(mapApi,journey.Position))
                CacheMapLabel(journey.Position,rect,journey.MarkerColor,journey.Name,journey.Foot);
                foreach(var point in _snapshot.Points) if (!Fogged(mapApi, point.Position)) CacheMapLabel(point.Position, rect, point.Color, point.Name);
                foreach(var battle in _snapshot.Battles)
                {if(Fogged(mapApi,battle))continue;var p=MapPoint(battle,rect);_mapBattles.Add(new Rect(p.x-9f,p.y-9f,18f,18f));}
                }
            }
            if(traffic)
            {
                if(inkOn){GUI.color=Color.white;GUI.DrawTexture(new Rect(0,0,rect.width,rect.height),_gameInk.Texture);}
                foreach(var label in _mapLabels)DrawMapLabel(label);
                GUI.color=Color.white;foreach(var battle in _mapBattles)GUI.DrawTexture(battle,Skull());
            }
            for(int rig = 0; rig < rigs; rig++)
            {
                if(mapApi!=null&&mapApi.Markers==Core.MapDetail.Off)continue;
                var p=_gameMap.WorldToViewportPoint(Core.Origin.Scene(source.RigPosition(rig)));
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
        /// <summary>Detects route, camera, fog and display changes before rebuilding the cached route texture.</summary>
        private bool InkChanged(Core.IMapMarkers mapApi,bool first)
        {
            float now=Time.unscaledTime;
            long sig=0;
            foreach(var journey in _snapshot.Journeys)if(!journey.Foot)sig++;
            foreach(var journey in _snapshot.Journeys)
            {
                if(journey.Foot)continue;   // not inked: their walking never re-inks the routes
                var c=journey.RouteColor;
                sig=sig*31+(journey.Route!=null?System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(journey.Route)*7+journey.Route.Length:0);
                sig=sig*31+((c.r<<16)|(c.g<<8)|c.b);
            }
            var cam=_gameMap.transform;
            bool camera=cam.position!=_inkCamPos||cam.rotation!=_inkCamRot||_gameMap.orthographicSize!=_inkCamSize||_gameMap.fieldOfView!=_inkCamFov||_gameMap.aspect!=_inkCamAspect;
            bool fog=false;
            if(mapApi!=null)
            {
                // CleanHUD reveals the fog only around the player, fading in over about a second: re-ink while the player moves (10 m cells) and 2 s after
                var player=Core.GameRefs.Player != null ? Core.GameRefs.Player.transform : null;
                if(player!=null)
                {
                    var w=Core.Origin.World(player.position);int x=Mathf.FloorToInt(w.x/10f),z=Mathf.FloorToInt(w.z/10f);
                    if(x!=_inkPlayerX||z!=_inkPlayerZ){_inkPlayerX=x;_inkPlayerZ=z;_fogMovingUntil=now+2f;}
                }
                fog=now<_fogMovingUntil;
            }
            if(!first&&!camera&&!fog&&sig==_inkSig&&now<_inkSafetyAt)return false;
            _inkSig=sig;_inkCamPos=cam.position;_inkCamRot=cam.rotation;_inkCamSize=_gameMap.orthographicSize;_inkCamFov=_gameMap.fieldOfView;_inkCamAspect=_gameMap.aspect;
            _inkSafetyAt=now+10f;return true;
        }
        /// <summary>With CleanHUD's fog of war on, routes are inked only across explored ground (50 m pieces, judged at their midpoints).</summary>
        private static bool Fogged(Core.IMapMarkers map,Vector3 world){return map!=null&&map.Hidden(Core.Origin.Scene(world));}
        /// <summary>Splits a stable-world route into explored pieces before rasterizing the visible spans.</summary>
        private void InkSegment(Core.IMapMarkers map,Vector3 a,Vector3 b,Rect canvas,Color32 color)
        {
            if(map==null){_gameInk.Segment(MapPoint(a,canvas),MapPoint(b,canvas),color);return;}
            int pieces=Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))/50f),1,400);
            int start=-1;
            for(int k=0;k<=pieces;k++)
            {
                bool shown=k<pieces&&!Fogged(map,Vector3.Lerp(a,b,(k+.5f)/pieces));
                if(shown&&start<0)start=k;
                else if(!shown&&start>=0){_gameInk.Segment(MapPoint(Vector3.Lerp(a,b,(float)start/pieces),canvas),MapPoint(Vector3.Lerp(a,b,(float)k/pieces),canvas),color);start=-1;}
            }
        }
        /// <summary>Resolves the native map image from the MapCheat FSM, including inactive UI.</summary>
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
        /// <summary>Projects and caches one map label for the next GUI repaint.</summary>
        private void CacheMapLabel(Vector3 world,Rect rect,Color color,string text,bool foot=false)
        {var p=MapPoint(world,rect);_mapLabels.Add(new MapLabel{rect=new Rect(p.x+7f,p.y-13f,260f,26f),color=color,text=text,foot=foot});}
        /// <summary>Draws the cached foot or vehicle dot with an outlined caption.</summary>
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
        /// <summary>Projects a stable-world position into the native map rectangle.</summary>
        private Vector2 MapPoint(Vector3 world,Rect rect)
        {var p=_gameMap.WorldToViewportPoint(Core.Origin.Scene(world));return new Vector2(p.x*rect.width,(1f-p.y)*rect.height);}

        /// <summary>Lazily creates the shared battle-marker texture.</summary>
        private Texture2D Skull()
        {
            if(_skull!=null)return _skull;
            string[] rows={"00011111111000","00111111111100","01111111111110","11111111111111","11100111100111","11100111100111","11111100111111","01111100111110","00111111111100","00011011011000","00011011011000"};
            _skull=new Texture2D(14,11,TextureFormat.RGBA32,false);var pixels=new Color32[154];for(int y=0;y<11;y++)for(int x=0;x<14;x++)pixels[(10-y)*14+x]=rows[y][x]=='1'?new Color32(230,230,230,255):new Color32(0,0,0,0);
            _skull.SetPixels32(pixels);_skull.filterMode=FilterMode.Point;_skull.Apply();return _skull;
        }
        /// <summary>Clears transient traffic map layer state so the next scene or session starts cleanly.</summary>
        internal void Reset() { _snapshotAt = 0f; _gameInkAt = 0f; _gameMap = null; _gameMapImage = null; _mapCheat = null; _nextMapScan = 0f; _mapWasOpen = false; _mapLabels.Clear(); _mapBattles.Clear(); }
        /// <summary>Destroys owned traffic map layer resources and releases scene bindings.</summary>
        internal void Dispose(){_gameInk.Dispose();if(_skull!=null)UnityEngine.Object.Destroy(_skull);if(_mapDot!=null)UnityEngine.Object.Destroy(_mapDot);}
    }
}
