using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Gunplay
{
    /// <summary>Actual thrown lance and explosive damage engine; traffic supplies attack intent and tuning.</summary>
    internal sealed class RiderWeapons : IRiderWeapons
    {
        /// <summary>Creates the native projectile with the original launch solver, collision exclusions and fuse.</summary>
        public void Throw(Vector3 from,Vector3 velocity,Vector3 target,Vector3 targetVelocity,float speed,Collider[] platform)
        {
            var prefab=LancePrefabs.FindAny(LanceExplosions.Projectile);if(prefab==null)return;
            Vector3 launch;float flight;if(!LanceTrajectory.Solve(from,velocity,target+Vector3.up*.9f,targetVelocity,speed,out launch,out flight))return;
            var dir=launch.normalized;var go=Object.Instantiate(prefab,from+dir*.6f,Quaternion.LookRotation(dir));go.SetActive(true);
            var rb=go.GetComponent<Rigidbody>()??go.AddComponent<Rigidbody>();rb.isKinematic=false;rb.velocity=launch+velocity;
            foreach(var c in platform ?? new Collider[0])foreach(var projectile in go.GetComponentsInChildren<Collider>(true))if(c!=null && projectile!=null)Physics.IgnoreCollision(projectile,c,true);
            go.AddComponent<LanceFuse>();
        }
        /// <summary>Applies the real blast using caller-supplied tuning and the native player damage path.</summary>
        public void Blast(Vector3 at,string who,float radius,float damage) { LanceExplosions.Kaboom(at,who,radius,damage); }
    }
}
