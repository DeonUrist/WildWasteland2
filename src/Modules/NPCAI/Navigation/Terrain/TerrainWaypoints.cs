using System;
using Den.Tools.Matrices;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// Foot long-route planner over an ElevationGrid (worker thread); NPCAI foot planning (Wave A split of Patrols Traffic/TrafficElevation.cs).
    /// </summary>
    internal static class TerrainWaypoints
    {
        private static readonly float[] Sides={-1400f,-1000f,-650f,-350f,-150f,0f,150f,350f,650f,1000f,1400f};
        /// <summary>Long foot route from a to b over the elevation grids: a dynamic-programming pick among lateral offsets and road hints per step (slopes, climbs and blocked boxes cost); falls back to RoadPlanner.Build without grids or route. roads flags the points taken from road hints.</summary>
        internal static Vector3[] Build(Vector3 a,Vector3 b,Vector3[][] hints,Bounds[] blocks,float influence,int seed,ElevationGrid[] grids,out bool[] roads)   // refs-ok: REFACTOR-REQUEST B08-3
        {
            if(grids.Length==0)return RoadPlanner.Build(a,b,hints,blocks,influence,seed,out roads);   // refs-ok: REFACTOR-REQUEST B08-3
            float length=TrafficLocations.FlatDistance(a,b);int steps=Mathf.Clamp(Mathf.CeilToInt(length/350f),6,16);
            var forward=b-a;forward.y=0f;forward.Normalize();var right=Vector3.Cross(Vector3.up,forward);
            const int choices=12;
            var points=new Vector3[steps+1,choices];var valid=new bool[steps+1,choices];var onRoad=new bool[steps+1,choices];
            points[0,0]=a;valid[0,0]=true;points[steps,0]=b;valid[steps,0]=true;
            float phase=(float)new System.Random(seed).NextDouble()*Mathf.PI*2;
            for(int i=1;i<steps;i++)
            {
                float t=(float)i/steps;var center=Vector3.Lerp(a,b,t)+right*(Mathf.Sin(t*Mathf.PI*3+phase)*Mathf.Sin(t*Mathf.PI)*Mathf.Min(100f,length*0.1f));
                for(int j=0;j<Sides.Length;j++)
                {
                    var p=center+right*(Sides[j]*Mathf.Min(1f,length/1600f));
                    if(ElevationGrid.Sample(grids,p,out float height,out _))p.y=height;   // refs-ok: REFACTOR-REQUEST B08-3
                    points[i,j]=p;valid[i,j]=!Inside(blocks,p);
                }
                float best=influence*influence;Vector3 nearest=center;bool found=false;
                foreach(var tile in hints)foreach(var p in tile)
                {
                    if(Mathf.Abs(Vector3.Dot(p-a,forward)-length*t)>length/steps*0.7f)continue;
                    float distance=TrafficLocations.FlatDistance(p,center);if(distance*distance>=best||Inside(blocks,p))continue;
                    best=distance*distance;nearest=p;found=true;
                }
                if(found)
                {if(ElevationGrid.Sample(grids,nearest,out float height,out _))nearest.y=height;points[i,11]=nearest;valid[i,11]=true;onRoad[i,11]=true;}   // refs-ok: REFACTOR-REQUEST B08-3
            }
            var costs=new float[steps+1,choices];var parent=new int[steps+1,choices];
            for(int i=0;i<=steps;i++)for(int j=0;j<choices;j++){costs[i,j]=float.PositiveInfinity;parent[i,j]=-1;}costs[0,0]=0f;
            for(int i=1;i<=steps;i++)for(int j=0;j<choices;j++)
            {
                if(!valid[i,j])continue;
                for(int previous=0;previous<choices;previous++)
                {
                    if(float.IsPositiveInfinity(costs[i-1,previous]))continue;
                    float score=Leg(points[i-1,previous],points[i,j],a,b,grids,blocks);
                    if(onRoad[i,j])score*=0.9f;
                    float cost=costs[i-1,previous]+score;
                    if(cost<costs[i,j]){costs[i,j]=cost;parent[i,j]=previous;}
                }
            }
            if(parent[steps,0]<0)return RoadPlanner.Build(a,b,hints,blocks,influence,seed,out roads);   // refs-ok: REFACTOR-REQUEST B08-3
            var route=new Vector3[steps+1];roads=new bool[steps+1];int pick=0;
            for(int i=steps;i>=0;i--){route[i]=points[i,pick];roads[i]=onRoad[i,pick];pick=parent[i,pick];}
            return route;
        }
        /// <summary>True when the flat point lies in one of the blocked boxes.</summary>
        private static bool Inside(Bounds[] blocks,Vector3 p)
        {p.y=0f;foreach(var box in blocks)if(box.Contains(p))return true;return false;}
        /// <summary>Cost of one leg: its length, crossing blocked boxes (infinite), climbing above the a-b baseline, height changes and slope samples.</summary>
        private static float Leg(Vector3 a,Vector3 b,Vector3 origin,Vector3 destination,ElevationGrid[] grids,Bounds[] blocks)   // refs-ok: REFACTOR-REQUEST B08-3
        {
            var aa=a;var bb=b;aa.y=bb.y=0f;var delta=bb-aa;float length=delta.magnitude;
            foreach(var box in blocks)
                if(!box.Contains(aa)&&!box.Contains(bb)&&length>0.01f&&box.IntersectRay(new Ray(aa,delta/length),out var hit)&&hit<length)return float.PositiveInfinity;
            float cost=length,last=a.y;int known=0;
            var along=destination-origin;along.y=0f;float span=along.sqrMagnitude;
            for(int sample=1;sample<=8;sample++)
            {
                var p=Vector3.Lerp(a,b,sample/8f);
                if(!ElevationGrid.Sample(grids,p,out float height,out float slope)){cost+=30f;continue;}   // refs-ok: REFACTOR-REQUEST B08-3
                float t=span>0f?Mathf.Clamp01(Vector3.Dot(p-origin,along)/span):0f;
                float baseline=Mathf.Lerp(origin.y,destination.y,t);
                cost+=Mathf.Max(0f,height-baseline-15f)*1.5f+Mathf.Abs(height-last)*3f+slope*slope*600f;
                if(slope>0.35f||Mathf.Abs(height-last)>Mathf.Max(1f,length/8f)*0.35f)cost+=3000f;
                last=height;known++;
            }
            return cost+(known==0?100f:0f);
        }
    }
}
