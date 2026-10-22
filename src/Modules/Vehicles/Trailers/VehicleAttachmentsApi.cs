using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;
using WildWasteland.Modules.Vehicles.WarRig;
namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Owns native trailer discovery, attachment and rig cleanup operations.</summary>
    internal sealed class VehicleAttachmentsApi : IVehicleAttachments
    {
        /// <summary>Recognizes a native trailer body.</summary>
        public bool IsTrailer(GameObject car) {return TrailerSupport.IsTrailer(car);}
        /// <summary>Checks actual native joint connectivity.</summary>
        public bool Connected(GameObject car,GameObject trailer) {return TrailerSupport.Connected(car,trailer);}
        /// <summary>Finds the currently attached native trailer.</summary>
        public GameObject Attached(GameObject car) {return TrailerSupport.Attached(car);}
        /// <summary>Connects through native joint semantics, retaining poses during restore when requested.</summary>
        public void Connect(GameObject car,GameObject trailer,bool preservePose=false) {TrailerSupport.Connect(car,trailer,preservePose);}
        /// <summary>Classifies native hinge parts and free attachables.</summary>
        public bool IsPart(Transform item) {return VehicleAttachments.IsPart(item);}
        /// <summary>Classifies a native free attachment without exposing its implementation.</summary>
        public bool IsFreeAttachment(GameObject item) {return VehicleAttachments.IsFreeAttachment(item);}
        /// <summary>Recognizes an actual War Rig ancestor without exposing its component.</summary>
        public bool IsWarRig(GameObject item) {return item!=null&&item.GetComponentInParent<WarRigUnit>()!=null;}
        /// <summary>Releases actual tanker joints before a towing vehicle is deleted.</summary>
        public void DetachTankers(GameObject truck) {if(truck==null)return;var body=truck.GetComponent<Rigidbody>();if(body==null)return;foreach(var rig in WarRigUnit.All)if(rig!=null)foreach(var joint in rig.GetComponents<ConfigurableJoint>())if(joint.connectedBody==body){Object.Destroy(joint);var leg=rig.transform.Find("leg");if(leg!=null)leg.gameObject.SetActive(true);}}
    }
}
