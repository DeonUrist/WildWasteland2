using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    internal sealed class RoadJob
    {
        internal volatile bool Complete;
        internal Vector3[] Route;
        internal bool[] Roads;
        internal string Error;
        internal int Revision, Epoch;
        internal bool Elevation;
        internal TrafficBase Source, Destination;
    }

    // Coarse hints influence a short, fixed waypoint chain. No junction graph, connectivity prediction
    // or full-world search. A single worker computes routes once; driving and maps reuse those arrays.
    internal static class RoadPlanner
    {
        private sealed class Request
        {
            internal RoadJob Job;
            internal Vector3 A,B;
            internal Vector3[][] Hints;
            internal Bounds[] Blocks;
            internal Bounds[] Quiet;
            internal float Influence;
            internal int Seed;
            internal ElevationGrid[] Elevation;
            internal Vector3[][] Lines;
            internal bool Convoy, Foot;
            internal TrafficBase[] Sources, Destinations;
            internal Vector3[] Existing;
        }
        private static readonly ConcurrentQueue<Request> Queue=new ConcurrentQueue<Request>();
        private static int _running,_epoch;
        private static int _snapshotRoad=-1,_snapshotCamps=-1,_snapshotCount=-1;
        private static RoadTile[] _tiles;
        private static Bounds[] _blocks;
        internal static long Searches;
        internal static void Reset(){Interlocked.Increment(ref _epoch);_snapshotRoad=_snapshotCamps=_snapshotCount=-1;}
        internal static RoadJob RequestRoute(TrafficBase source,TrafficBase destination,bool convoy,bool foot=false,TrafficBase[] sources=null,TrafficBase[] destinations=null,Vector3[] existing=null)
        {
            if(PatrolsModule.TrafficRoadRouting.Value)TrafficRoads.RequestAlong(source.apron,destination.apron);
            if(_snapshotRoad!=TrafficRoads.Revision||_snapshotCamps!=TrafficLocations.Revision||_snapshotCount!=TrafficLocations.Known.Count)
            {
                _snapshotRoad=TrafficRoads.Revision;_snapshotCamps=TrafficLocations.Revision;_snapshotCount=TrafficLocations.Known.Count;
                _tiles=TrafficRoads.Tiles.Values.ToArray();_blocks=TrafficLocations.Blocks();
            }
            int seed=unchecked(source.apron.GetHashCode()*397^destination.apron.GetHashCode()^(convoy?73:19));
            var job=new RoadJob{Epoch=_epoch,Revision=_snapshotRoad,Source=source,Destination=destination};
            var size=TrafficRoads.TileSize;float margin=PatrolsModule.TrafficRoadConnector.Value+1600f;
            var a=source.apron;var b=destination.apron;var bounds=new Rect(Mathf.Min(a.x,b.x)-margin,Mathf.Min(a.z,b.z)-margin,Mathf.Abs(a.x-b.x)+margin*2f,Mathf.Abs(a.z-b.z)+margin*2f);
            var nearby=_tiles.Where(t=>sources!=null||destinations!=null||size.x<=0f||bounds.Overlaps(new Rect(t.X*size.x,t.Z*size.z,size.x,size.z))).ToArray();
            var hints=PatrolsModule.TrafficRoadRouting.Value?nearby.Select(t=>t.Hints).ToArray():new Vector3[0][];
            job.Elevation=nearby.Any(t=>t.Elevation!=null&&(foot||t.FinalGround));
            var lines=PatrolsModule.TrafficRoadRouting.Value?nearby.SelectMany(t=>t.Lines).ToList():new List<Vector3[]>();
            if(PatrolsModule.TrafficRoadRouting.Value&&Core.VistasWorldGen.Present)lines.AddRange(Core.VistasWorldGen.RoadLines);
            var blocks = new List<Bounds>(_blocks);
            var quiet = TrafficLocations.StartingBlocks(); blocks.AddRange(quiet);
            if (Modules.Towns.TownsApi.Provider != null) Modules.Towns.TownsApi.Provider.TravelBlocks(blocks);
            Queue.Enqueue(new Request{Job=job,A=a,B=b,Hints=hints,
                Elevation=nearby.Where(t=>t.Elevation!=null&&(foot||t.FinalGround)).Select(t=>t.Elevation).ToArray(),
                Blocks=blocks.Where(box => bounds.Overlaps(new Rect(box.min.x, box.min.z, box.size.x, box.size.z))).ToArray(),Influence=PatrolsModule.TrafficRoadConnector.Value,Seed=seed,Lines=lines.ToArray(),Convoy=convoy,Foot=foot,
                Sources=Alternatives(sources,source),Destinations=Alternatives(destinations,destination),Existing=existing,Quiet=quiet});
            Start();return job;
        }
        private static TrafficBase[] Alternatives(TrafficBase[] choices,TrafficBase fallback)
        {
            if(choices==null||choices.Length==0)return new[]{fallback};
            var result=new TrafficBase[Math.Min(4,choices.Length)];int start=UnityEngine.Random.Range(0,choices.Length);
            for(int i=0;i<result.Length;i++)result[i]=choices[(start+i)%choices.Length];return result;
        }
        private static void Start(){if(Interlocked.CompareExchange(ref _running,1,0)==0)Task.Run((Action)Run);}
        private static void Run()
        {
            try
            {
                while(Queue.TryDequeue(out var request))
                {
                    try
                    {
                        if(request.Job.Epoch!=Volatile.Read(ref _epoch))continue;
                        if(request.Foot)
                        {
                            var stops = request.Existing ?? new[] { request.A, request.B };
                            var points = new List<Vector3>(); var kinds = new List<bool>(); bool complete = true;
                            for (int i = 1; i < stops.Length; i++)
                            {
                                var leg = TerrainWaypoints.Build(stops[i-1],stops[i],request.Hints,request.Blocks,request.Influence,request.Seed,request.Elevation,out var roads);
                                if (leg == null) { complete = false; break; }
                                points.AddRange(i == 1 ? leg : leg.Skip(1)); kinds.AddRange(i == 1 ? roads : roads.Skip(1));
                            }
                            if (complete) { request.Job.Route=points.ToArray(); request.Job.Roads=kinds.ToArray(); }
                        }
                        else if(request.Existing!=null)
                        {
                            request.Job.Route=VehicleRoute.Revalidate(request.Existing,request.Lines,request.Elevation,request.Blocks,request.Convoy,out var roads);
                            request.Job.Roads=roads;
                        }
                        else
                        {
                            var pairs=new List<Tuple<float,TrafficBase,TrafficBase>>();
                            foreach(var source in request.Sources)foreach(var destination in request.Destinations)
                            {
                                float distance=TrafficLocations.FlatDistance(source.apron,destination.apron);
                                if(ReferenceEquals(source,destination)||request.Sources.Length+request.Destinations.Length>2&&distance<300f)continue;
                                float score=distance;
                                for(int i=0;i<=12;i++)
                                {var p=Vector3.Lerp(source.apron,destination.apron,i/12f);score+=ElevationGrid.Sample(request.Elevation,p,out _,out float slope)?slope*slope*distance:distance;}
                                pairs.Add(Tuple.Create(score,source,destination));
                            }
                            pairs.Sort((x,y)=>x.Item1.CompareTo(y.Item1));float best=float.PositiveInfinity;
                            for(int i=0;i<Math.Min(6,pairs.Count);i++)
                            {
                                if(request.Job.Epoch!=Volatile.Read(ref _epoch))break;
                                var pair=pairs[i];
                                var route=VehicleRoute.Build(pair.Item2.apron,pair.Item3.apron,request.Lines,request.Elevation,request.Blocks,request.Convoy,out var roads,out float score);
                                if(route==null||score>=best)continue;
                                best=score;request.Job.Route=route;request.Job.Roads=roads;request.Job.Source=pair.Item2;request.Job.Destination=pair.Item3;
                            }
                        }
                        if(request.Job.Route!=null && !Clear(request.Job.Route,request.Quiet))request.Job.Route=null;
                        if(request.Job.Route!=null)RoadPath.Prepare(request.Job.Route);Interlocked.Increment(ref Searches);
                    }
                    catch(Exception e){request.Job.Error=e.ToString();}
                    finally{request.Job.Complete=true;}
                }
            }
            finally{Interlocked.Exchange(ref _running,0);if(!Queue.IsEmpty)Start();}
        }
        internal static Vector3[] Build(Vector3 a,Vector3 b,Vector3[][] hints,Bounds[] blocks,float influence,int seed,out bool[] roads)
        {
            float length=TrafficLocations.FlatDistance(a,b);
            int steps=Mathf.Clamp(Mathf.CeilToInt(length/400f),6,16);
            var direction=b-a;direction.y=0f;direction.Normalize();var side=Vector3.Cross(Vector3.up,direction);
            var random=new System.Random(seed);float phase=(float)random.NextDouble()*Mathf.PI*2f;
            float amplitude=Mathf.Min(180f,length*0.16f);
            var route=new List<Vector3>{a};var kinds=new List<bool>{false};
            for(int i=1;i<steps;i++)
            {
                float t=(float)i/steps;
                var desired=Vector3.Lerp(a,b,t)+side*(Mathf.Sin(t*Mathf.PI*3f+phase)*Mathf.Sin(t*Mathf.PI)*amplitude);
                var chosen=desired;float best=influence*influence;bool road=false;
                foreach(var tile in hints)foreach(var point in tile)
                {
                    float along=Vector3.Dot(point-a,direction);
                    if(Mathf.Abs(along-length*t)>length/steps*0.7f)continue;
                    var d=point-desired;d.y=0f;float score=d.sqrMagnitude;
                    if(score>=best)continue;
                    bool inside=false;var flat=point;flat.y=0f;foreach(var box in blocks)if(box.Contains(flat)){inside=true;break;}
                    if(inside)continue;best=score;chosen=point;road=true;
                }
                if(TrafficLocations.FlatDistance(route[route.Count-1],chosen)<12f)continue;
                route.Add(chosen);kinds.Add(road);
            }
            route.Add(b);kinds.Add(false);
            // A coarse route can skirt a known camp without constructing a world navigation graph.
            for(int attempts=0;attempts<16 && route.Count<32;attempts++)
            {
                bool changed=false;
                for(int leg=1;leg<route.Count&&!changed;leg++)foreach(var box in blocks)
                {
                    var start=route[leg-1];var end=route[leg];start.y=end.y=0f;var d=end-start;float distance=d.magnitude;
                    if(box.Contains(start)||box.Contains(end)||distance<0.01f||!box.IntersectRay(new Ray(start,d/distance),out var hit)||hit>=distance)continue;
                    var min=box.min;var max=box.max;
                    var corners=new[]{new Vector3(min.x-3f,route[leg-1].y,min.z-3f),new Vector3(min.x-3f,route[leg-1].y,max.z+3f),
                        new Vector3(max.x+3f,route[leg-1].y,min.z-3f),new Vector3(max.x+3f,route[leg-1].y,max.z+3f)};
                    float cost=float.MaxValue;Vector3[] detour=null;
                    foreach(var c in corners)foreach(var e in corners)
                    {
                        if(Crosses(box,start,c)||Crosses(box,c,e)||Crosses(box,e,end))continue;
                        float score=TrafficLocations.FlatDistance(start,c)+TrafficLocations.FlatDistance(c,e)+TrafficLocations.FlatDistance(e,end);
                        if(score<cost){cost=score;detour=c==e?new[]{c}:new[]{c,e};}
                    }
                    if(detour==null)continue;
                    route.InsertRange(leg,detour);kinds.InsertRange(leg,Enumerable.Repeat(false,detour.Length));kinds[leg+detour.Length]=false;
                    changed=true;break;
                }
                if(!changed)break;
            }
            roads=kinds.ToArray();return route.ToArray();
        }
        private static bool Crosses(Bounds box,Vector3 a,Vector3 b)
        {a.y=b.y=0f;var d=b-a;float length=d.magnitude;return length>0.01f&&box.IntersectRay(new Ray(a,d/length),out var hit)&&hit<length;}
        internal static bool Clear(Vector3[] route, Bounds[] blocks)
        {
            foreach (var box in blocks) for (int i = 0; i < route.Length; i++)
            { var p = route[i]; p.y = 0; if (box.Contains(p) || i > 0 && Crosses(box, route[i - 1], p)) return false; }
            return true;
        }
    }
}
