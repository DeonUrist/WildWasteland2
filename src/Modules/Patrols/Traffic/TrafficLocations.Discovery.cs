using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrol-local policy records refreshed from the POI-owned discovery service.</summary>
    internal static partial class TrafficLocations
    {
        internal static readonly Dictionary<string,TrafficBase> Known=new Dictionary<string,TrafficBase>();
        internal static readonly Dictionary<string,TrafficPoi> Points=new Dictionary<string,TrafficPoi>();
        private static readonly List<CampDiscovery> Camps=new List<CampDiscovery>();
        private static readonly List<PoiDiscovery> Pois=new List<PoiDiscovery>();
        private static IWorldDiscovery _provider; private static int _seen=-1;
        internal static int Revision { get; private set; }
        internal static bool DiscoveryReady { get { return ModApi.Get<IWorldDiscovery>()?.Ready??false; } }
        internal static int DiscoveryLeft { get { return ModApi.Get<IWorldDiscovery>()?.Left??0; } }
        internal static void Changed(){Revision++;}
        internal static void Reset(){Known.Clear();Points.Clear();_provider=null;_seen=-1;Revision++;}
        internal static void Tick()
        { var p=ModApi.Get<IWorldDiscovery>();if(p==null||_provider==p&&_seen==p.Revision)return;_provider=p;_seen=p.Revision;p.Camps(Camps);p.Points(Pois);foreach(var c in Camps){TrafficBase b;if(!Known.TryGetValue(c.id,out b))Known[c.id]=b=new TrafficBase();b.id=c.id;b.name=c.name;b.faction=c.faction;b.center=c.center;b.size=c.size;b.gate=c.gate;b.apron=c.apron;b.mappedEntrance=c.mappedEntrance;b.cleared=c.cleared;}foreach(var q in Pois)Points[q.id]=new TrafficPoi{id=q.id,name=q.name,position=q.position};Revision++; }
        internal static void TileApplied() { }
        internal static bool TownInterior(Vector3 world){Vector3 p;return ModApi.Get<ITownTraffic>()?.ParkingOutside(world,out p)??false;}
        internal static Bounds[] Blocks()=>Known.Values.Select(c=>new Bounds(new Vector3(c.center.x,0,c.center.z),new Vector3(c.size.x+16,10000,c.size.z+16))).ToArray();
        internal static Bounds[] StartingBlocks()=>Points.Values.Where(p=>p.name.StartsWith("Starter_Area",StringComparison.Ordinal)).Select(p=>new Bounds(new Vector3(p.position.x,0,p.position.z),new Vector3(1000,10000,1000))).ToArray();
        internal static bool StartingAllowed(Vector3 p){foreach(var b in StartingBlocks())if(b.Contains(new Vector3(p.x,0,p.z)))return false;return true;}
        internal static int StartingRevision {get{int r=0;foreach(var p in Points.Values)if(p.name.StartsWith("Starter_Area",StringComparison.Ordinal))r^=p.position.GetHashCode();return r;}}
        internal static bool Contains(TrafficBase c,Vector3 p,float margin=35)=>Mathf.Abs(p.x-c.center.x)<=c.size.x/2+margin&&Mathf.Abs(p.z-c.center.z)<=c.size.z/2+margin;
        internal static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        internal static bool IsCamp(string n)=>n!=null&&n.StartsWith("Camp_",StringComparison.OrdinalIgnoreCase)&&n.Length>5&&char.IsDigit(n[5]);
        internal static bool PoiName(string n)=>n!=null&&(n.StartsWith("Building_",StringComparison.Ordinal)||n.StartsWith("Wreck_",StringComparison.Ordinal)||n.StartsWith("Cave_",StringComparison.Ordinal)||n.StartsWith("Starter_Area",StringComparison.Ordinal)||n.StartsWith("Camp_10",StringComparison.Ordinal)||n.StartsWith("Camp_15",StringComparison.Ordinal)||n.StartsWith("Camp_16",StringComparison.Ordinal));
    }
}
