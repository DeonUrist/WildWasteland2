using System;
using UnityEngine;
namespace WildWasteland.Modules.POI
{
    /// <summary>Certifies remembered vehicle entrances against actual terrain and static obstacles.</summary>
    internal static partial class TrafficLocations
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[64];
        /// <summary>Certifies the entrance corridor with a bounded non-allocating physics query.</summary>
        private static bool VehicleClear(Vector3 from,Vector3 to,float radius)
        { var d=to-from;float length=d.magnitude;if(length<.1f)return true;int n=Physics.SphereCastNonAlloc(from,radius,d/length,Hits,length,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);bool clear=n<Hits.Length;for(int i=0;i<n;i++){var h=Hits[i];if(h.collider!=null&&h.distance>0&&h.distance<=length&&!(h.collider is TerrainCollider)&&h.collider.gameObject.layer!=9&&h.collider.attachedRigidbody==null)clear=false;}Array.Clear(Hits,0,Hits.Length);return clear; }
        /// <summary>Finds exposed terrain below the sample and validates its slope.</summary>
        private static bool GroundPoint(Vector3 p,out Vector3 ground)
        { ground=p;int n=Physics.RaycastNonAlloc(new Vector3(p.x,p.y+3000,p.z),Vector3.down,Hits,6000,~0,QueryTriggerInteraction.Ignore);if(n==0||n==Hits.Length){Array.Clear(Hits,0,n);return false;}var best=Hits[0];for(int i=1;i<n;i++)if(Hits[i].distance<best.distance)best=Hits[i];Array.Clear(Hits,0,n);if(!(best.collider is TerrainCollider)||best.normal.y<1f/Mathf.Sqrt(1+.3f*.3f))return false;ground=best.point;return true; }
        /// <summary>Checks the center and four surrounding terrain samples for a vehicle apron.</summary>
        private static bool GroundAt(Vector3 p,out Vector3 ground)
        {if(!GroundPoint(p,out ground))return false;for(int i=0;i<4;i++){var sample=ground+(i<2?Vector3.right:Vector3.forward)*((i%2==0?1:-1)*5);Vector3 edge;if(!GroundPoint(sample,out edge)||Mathf.Abs(edge.y-ground.y)>1.5f)return false;}return true;}
    }
}
