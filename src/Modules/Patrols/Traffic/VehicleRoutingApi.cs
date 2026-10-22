using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Exposes asynchronous road planning through pollable results and owner-scoped main-thread route hints.</summary>
    internal sealed class VehicleRoutingApi : IVehicleRouting
    {
        private sealed class Hint { internal int Id,Order,Revision; internal IModule Owner;internal IRouteHintProvider Provider; }
        private sealed class Query { internal Vector3 From,To;internal int Revision;internal RoadJob Job; }
        private static readonly List<Hint> Hints=new List<Hint>();
        private readonly List<Query> _queries=new List<Query>();
        private static int _id,_revision;
        /// <summary>Includes road geometry and actual hint-provider revision changes.</summary>
        public int Revision { get { RefreshHints();return unchecked(TrafficRoads.Revision*397^_revision); } }
        /// <summary>Queues immutable worker planning once, returning its completed route on a subsequent query.</summary>
        public bool TryRoute(Vector3 from,Vector3 to,List<Vector3> into)
        {
            into.Clear();int revision=Revision;
            for(int i=0;i<_queries.Count;i++){var q=_queries[i];if(q.From!=from||q.To!=to||q.Revision!=revision)continue;if(!q.Job.Complete)return false; if(q.Job.Route==null)return false;into.AddRange(q.Job.Route);return true;}
            if(_queries.Count>=32)_queries.RemoveAt(0);
            _queries.Add(new Query {From=from,To=to,Revision=revision,Job=RoadPlanner.RequestRoute(new TrafficBase {apron=from},new TrafficBase {apron=to},false)});return false;
        }
        /// <summary>Projects onto actual captured road segments without generating routes or querying terrain.</summary>
        public bool TryNearestRoad(Vector3 world,float range,out Vector3 point,out Vector3 direction)
        {
            point=Vector3.zero;direction=Vector3.forward;float best=range*range;bool found=false;
            foreach(var tile in TrafficRoads.Tiles.Values)foreach(var line in tile.Lines)for(int i=1;i<line.Length;i++){var a=line[i-1];var d=line[i]-a;float length=d.x*d.x+d.z*d.z;if(length<.001f)continue;float t=Mathf.Clamp01(((world.x-a.x)*d.x+(world.z-a.z)*d.z)/length);var p=a+d*t;float dx=p.x-world.x,dz=p.z-world.z;float distance=dx*dx+dz*dz;if(distance>best)continue;best=distance;point=p;direction=d.normalized;found=true;}return found;
        }
        /// <summary>Registers live hint geometry in deterministic order, once per owner/provider instance.</summary>
        public int RegisterHintProvider(IModule owner,IRouteHintProvider provider,int order) { if(provider==null)return -1;foreach(var h in Hints)if(h.Owner==owner&&ReferenceEquals(h.Provider,provider))return h.Id;var entry=new Hint {Id=++_id,Owner=owner,Provider=provider,Order=order,Revision=provider.Revision};Hints.Add(entry);Hints.Sort((a,b)=>a.Order!=b.Order?a.Order.CompareTo(b.Order):a.Id.CompareTo(b.Id));_revision++;return entry.Id; }
        /// <summary>Withdraws an owned hint contribution and invalidates cached route revisions.</summary>
        public void RemoveHintProvider(int id) { if(Hints.RemoveAll(h=>h.Id==id)>0)_revision++; }
        /// <summary>Removes stopped owners and tracks geometry revisions before publishing a route.</summary>
        private static void RefreshHints() { for(int i=Hints.Count-1;i>=0;i--){var h=Hints[i];if(h.Owner!=null&&!ModuleHost.IsRunning(h.Owner)){Hints.RemoveAt(i);_revision++;continue;}int revision=h.Provider.Revision;if(revision!=h.Revision){h.Revision=revision;_revision++;}} }
        /// <summary>Applies optional module hints only on the main thread after worker geometry planning completes.</summary>
        internal static void ApplyHints(RoadJob job)
        {
            if(job.Route==null||job.Route.Length<2)return;RefreshHints();if(Hints.Count==0)return;var route=new List<Vector3>(job.Route);bool changed=false;
            foreach(var h in Hints){try{changed|=h.Provider.Adjust(route[0],route[route.Count-1],route);foreach(var point in route)if(h.Provider.IsBlocked(point)){job.Route=null;job.Roads=null;return;}}catch(Exception error){ModuleHost.ReportHookException(h.Owner,"vehicle route hints",error);job.Route=null;job.Roads=null;return;}}
            if(changed){job.Route=route.ToArray();job.Roads=new bool[route.Count];RoadPath.Prepare(job.Route);}
        }
        /// <summary>Releases all optional owners when Patrols is disabled.</summary>
        internal static void Reset() { Hints.Clear();_revision++; }
    }
}
