using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Worker-safe geometry over detached hints and numeric exclusion bounds.</summary>
    internal static class RouteGeometry
    {
        /// <summary>Measures horizontal distance without touching scene state.</summary>
        private static float FlatDistance(Vector3 a,Vector3 b) {float x=a.x-b.x,z=a.z-b.z;return Mathf.Sqrt(x*x+z*z);}
        internal static Vector3[] Build(Vector3 a,Vector3 b,Vector3[][] hints,Bounds[] blocks,float influence,int seed,out bool[] roads)
        {
            float length=FlatDistance(a,b);
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
                if(FlatDistance(route[route.Count-1],chosen)<12f)continue;
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
                        float score=FlatDistance(start,c)+FlatDistance(c,e)+FlatDistance(e,end);
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
