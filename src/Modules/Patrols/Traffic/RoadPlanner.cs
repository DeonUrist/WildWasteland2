using WildWasteland.Core;
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
        private static readonly ConcurrentQueue<RoadJob> Completed=new ConcurrentQueue<RoadJob>();
        /// <summary>Publishes completed worker geometry after main-thread owner hints, never calling Unity/module providers on a worker.</summary>
        internal static void PublishCompleted() { while(Completed.TryDequeue(out var job)){if(job.Epoch!=Volatile.Read(ref _epoch)){job.Route=null;job.Roads=null;}else VehicleRoutingApi.ApplyHints(job);job.Complete=true;} }
        private static readonly ConcurrentQueue<Request> Queue=new ConcurrentQueue<Request>();
        private static int _running,_epoch;
        private static int _snapshotRoad=-1,_snapshotCamps=-1,_snapshotCount=-1;
        private static RoadTile[] _tiles;
        private static Bounds[] _blocks;
        internal static long Searches;
        /// <summary>Clears retained runtime state at the owning lifecycle boundary.</summary>
        internal static void Reset(){Interlocked.Increment(ref _epoch);_snapshotRoad=_snapshotCamps=_snapshotCount=-1;}
        /// <summary>Checks the current request route condition for road planner.</summary>
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
            if (Core.ModApi.Get<Core.ITownTraffic>() != null) Core.ModApi.Get<Core.ITownTraffic>().TravelBlocks(blocks);
            Queue.Enqueue(new Request{Job=job,A=a,B=b,Hints=hints,
                Elevation=nearby.Where(t=>t.Elevation!=null&&(foot||t.FinalGround)).Select(t=>t.Elevation).ToArray(),
                Blocks=blocks.Where(box => bounds.Overlaps(new Rect(box.min.x, box.min.z, box.size.x, box.size.z))).ToArray(),Influence=PatrolsModule.TrafficRoadConnector.Value,Seed=seed,Lines=lines.ToArray(),Convoy=convoy,Foot=foot,
                Sources=Alternatives(sources,source),Destinations=Alternatives(destinations,destination),Existing=existing,Quiet=quiet});
            Start();return job;
        }
        /// <summary>Chooses the bounded alternative endpoint set for worker planning.</summary>
        private static TrafficBase[] Alternatives(TrafficBase[] choices,TrafficBase fallback)
        {
            if(choices==null||choices.Length==0)return new[]{fallback};
            var result=new TrafficBase[Math.Min(4,choices.Length)];int start=UnityEngine.Random.Range(0,choices.Length);
            for(int i=0;i<result.Length;i++)result[i]=choices[(start+i)%choices.Length];return result;
        }
        /// <summary>Resolves native references after the component enters the loaded scene.</summary>
        private static void Start(){if(Interlocked.CompareExchange(ref _running,1,0)==0)Task.Run((Action)Run);}
        /// <summary>Drains detached numeric worker requests and publishes completed results.</summary>
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
                    finally{Completed.Enqueue(request.Job);}
                }
            }
            finally{Interlocked.Exchange(ref _running,0);if(!Queue.IsEmpty)Start();}
        }
        /// <summary>Validates immutable planned routes against numeric exclusion bounds.</summary>
        internal static bool Clear(Vector3[] route,Bounds[] blocks) {return Core.RouteGeometry.Clear(route,blocks);}
    }
}
