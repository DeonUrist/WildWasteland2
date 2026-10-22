using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles.WarRig;
namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Actual War Rig factory, placement and tank geometry used by optional mods and traffic.</summary>
    internal sealed class WarRigWorldApi : IWarRigWorld
    {
        public bool Running=>VehiclesModule.Running;
        public bool SpawnEnabled=>Running&&S.SpawnEnabled!=null&&S.SpawnEnabled.Value;
        public string TemplateName=>Factory.Name;
        public GameObject Template()=>Running?Factory.Template():null;
        public void SpawnNearPlayer() { if(Running)WarRigFeature.SpawnNearPlayer(S.EquipWheels); }
        public bool IsTemplate(GameObject prefab)=>WorldSpawning.IsTemplate(prefab);
        public bool CanSpawnAt(ref Vector3 at,Quaternion rotation)=>WorldSpawning.CanSpawnAt(ref at,rotation);
        public bool InsideTank(GameObject actor) {
            if(!Running||actor==null)return false;
            var tank=new Bounds(new Vector3(0,.95f,-1.25f),new Vector3(4.4f,2,17.7f));
            foreach(var rig in WarRigUnit.All)if(rig!=null&&rig.gameObject.activeInHierarchy){var p=Quaternion.Inverse(Layout.Rot)*(rig.transform.InverseTransformPoint(actor.transform.position)-Layout.Off)/S.Scale;if(tank.Contains(p))return true;}
            return false;
        }
    }
}
