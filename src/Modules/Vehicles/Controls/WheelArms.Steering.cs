using NWH.VehiclePhysics2;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Owns the native removable-wheel steering rule independently of presentation updates.</summary>
    internal static class MissingSteeringWheel
    {
        private static GameObject _car;
        private static VehicleController _vehicle;
        /// <summary>Installs the native steering guard under its actual vehicle owner.</summary>
        internal static void Install(IModule owner){Patches.Patch(owner,Patches.Method(typeof(Steering),"CalculateSteerAngles"),prefix:Patches.Method(typeof(MissingSteeringWheel),"BeforeSteer"));}
        /// <summary>Prevents native steering only on the player's car with an empty removable-wheel mount.</summary>
        private static void BeforeSteer(Steering __instance)
        {
            var car=PlayerState.Car;if(!ReferenceEquals(car,_car)||_vehicle==null){_car=car;_vehicle=car!=null?car.GetComponent<VehicleController>():null;}
            if(_vehicle==null||!ReferenceEquals(__instance,_vehicle.steering))return;
            var mount=__instance.steeringWheel;if(mount==null||mount.name!="hinge_steeringwheel")return;
            for(int i=0;i<mount.childCount;i++){var part=mount.GetChild(i);if(part.gameObject.activeInHierarchy&&part.CompareTag("vehPart"))return;}
            _vehicle.input.Steering=0f;
        }
        /// <summary>Drops native cache references when the world or owner changes.</summary>
        internal static void Clear(){_car=null;_vehicle=null;}
    }
}
