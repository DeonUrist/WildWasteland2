using System.Collections;
using System.Linq;
using UnityEngine;
namespace WildWasteland.Modules.Patrols
{
    [ES3NonSerializable]
    internal sealed class MerchantGuardHealth : MonoBehaviour
    {
        internal MerchantTurret Saved;internal float Maximum;
        /// <summary>Resolves native references after the component enters the loaded scene.</summary>
        private IEnumerator Start()
        {
            yield return null;yield return null;
            var health=Patrol.GetHealth(gameObject);if(health<=0f)yield break;
            var prefab=Prefabs.FindAny(Core.Names.Prefab(gameObject.name.Replace("(Rider)","")));
            Maximum=prefab!=null?Patrol.GetHealth(prefab):health;if(Maximum<=0f)Maximum=health;
            var value=Saved!=null?Mathf.Clamp(Saved.maxHealth>Maximum?Saved.health*Maximum/Saved.maxHealth:Saved.health,0f,Maximum):Mathf.Min(health,Maximum);
            Patrol.SetHealth(gameObject,value);
            var late=GetComponent<Patrol.LateHealth>();if(late!=null)late.Value=value;
            GetComponent<RiderGuard>()?.MaxHealth(Maximum);
        }
    }
    internal static class MerchantGuards
    {
        /// <summary>Creates or reuses the runtime component and binds the supplied native objects.</summary>
        internal static void Attach(GameObject car,GameObject person,int slot)
        {
            var host=car.GetComponent<MerchantHost>();if(host?.Data==null)return;
            var health=person.GetComponent<MerchantGuardHealth>()??person.AddComponent<MerchantGuardHealth>();
            health.Saved=host.Data.turrets?.FirstOrDefault(t=>t.slot==slot);
        }
        /// <summary>Copies current native state into detached records for persistence or handoff.</summary>
        internal static void Capture(MerchantHost host)
        {
            var marker=host.GetComponent<PatrolMarker>();if(marker==null)return;
            // A null record means construction has not finished yet. Do not overwrite saved guards
            // while a returning bus is still assembling its occupants.
            if(!host.Ready)return;
            host.Data.turrets=Enumerable.Range(0,marker.Riders.Length).Select(slot=>
            {
                var person=marker.Riders[slot];var anchor=marker.RiderAnchors[slot];var guard=person!=null?person.GetComponent<MerchantGuardHealth>():null;
                return new MerchantTurret{slot=slot,prefab=person!=null?Core.Names.Prefab(person.name.Replace("(Rider)","")):"",health=person!=null?Mathf.Max(0f,Patrol.GetHealth(person)):0f,
                    maxHealth=guard!=null?guard.Maximum:0f,position=anchor!=null?anchor.localPosition:Vector3.zero,standing=person!=null&&person.GetComponent<RiderGuard>()?.StandingPose==true};
            }).ToArray();
        }
        /// <summary>Rebinds recorded state to native objects without rerolling authored outcomes.</summary>
        internal static void Restore(MerchantHost host)
        {
            if(host.Data.turrets==null)return;var marker=host.GetComponent<PatrolMarker>();if(marker==null)return;
            foreach(var person in host.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("(Rider)")&&t.GetComponent<RiderGuard>()==null).ToArray()){Core.ItemSpawner.RemoveTree(person.gameObject);Object.Destroy(person.gameObject);}
            foreach(var saved in host.Data.turrets)
            {
                if(saved==null||saved.slot<0||saved.slot>=marker.Riders.Length||saved.health<=0f||string.IsNullOrEmpty(saved.prefab)||marker.Riders[saved.slot]!=null)continue;
                var platform=saved.slot<3?null:Core.ModApi.Get<Core.IVehicleAttachments>().Attached(host.gameObject);if(saved.slot>=3&&platform==null)continue;
                Rider.Seat(host.gameObject,marker,saved.slot,saved.prefab,new[]{saved.position.x,saved.position.y,saved.position.z},platform,saved.standing);
            }
        }
    }
}
