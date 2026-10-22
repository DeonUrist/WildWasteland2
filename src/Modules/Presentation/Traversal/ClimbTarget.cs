using UnityEngine;

namespace WildWasteland.Modules.Presentation
{
    internal struct ClimbTarget
    {
        public Collider Surface;
        public Vector3 Start, Lift, End;
        public Quaternion Facing;
        public float Rise;
        public bool LowCatch;
    }
}
