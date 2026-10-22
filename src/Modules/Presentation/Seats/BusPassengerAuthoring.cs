using UnityEngine;
using Pose=WildWasteland.Modules.Patrols.Pose;
using WildWasteland.Core;
using WildWasteland.Modules.Patrols;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Owns bus-pose authoring values and a lifecycle-cleared visual edit target.</summary>
    internal sealed class BusPassengerAuthoring : IBusPassengerAuthoring
    {
        private GameObject _target;
        /// <summary>Selects the nearest actual live bus pose from the Presentation pose registry.</summary>
        public GameObject Nearest(Vector3 point,float radius) {GameObject best=null;float distance=radius*radius;foreach(var pose in Pose.All){if(pose==null||!pose.IsBusPassenger)continue;float next=(pose.transform.position-point).sqrMagnitude;if(next<distance){distance=next;best=pose.gameObject;}}return best;}
        /// <summary>Changes only the visual edit target; null releases live adjustment highlighting.</summary>
        public void Select(GameObject actor) {_target=actor;}
        /// <summary>Reports the actual current visual edit target.</summary>
        public bool Editing(GameObject actor) {return actor!=null&&actor==_target;}
        /// <summary>Copies one current joint or seat vector without exposing the mutable preset.</summary>
        public Vector3 Value(int joint,int side) {var p=BusPassengerPoseStore.Current;return joint==5?p.seatOffset:side==2?p.right[joint]:p.left[joint];}
        /// <summary>Applies the original mirrored joint and remapped seat movement with native authoring limits.</summary>
        public void Adjust(int joint,int side,Vector3 delta) {var p=BusPassengerPoseStore.Current;if(joint==5)p.seatOffset=Clamp(p.seatOffset+new Vector3(delta.y,delta.z,delta.x),1);else{if(side!=2)p.left[joint]=Clamp(p.left[joint]+delta,180);if(side!=1)p.right[joint]=Clamp(p.right[joint]+(side==0?new Vector3(delta.x,-delta.y,-delta.z):delta),180);}}
        /// <summary>Writes the actual current Presentation-owned preset atomically.</summary>
        public string Save() {return BusPassengerPoseStore.Save();}
        /// <summary>Restores unsaved shipped preset defaults.</summary>
        public void Reset() {BusPassengerPoseStore.Reset();}
        /// <summary>Clamps the existing authoring vector limits.</summary>
        private static Vector3 Clamp(Vector3 v,float limit) {return new Vector3(Mathf.Clamp(v.x,-limit,limit),Mathf.Clamp(v.y,-limit,limit),Mathf.Clamp(v.z,-limit,limit));}
    }
}
