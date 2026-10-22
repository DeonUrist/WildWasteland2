using UnityEngine;

namespace WildWasteland.Modules.Presentation
{
    internal sealed partial class Body
    {
        /// <summary>Head position of the player body during a jump clip (ModAPI TryPlayerJumpHead); false in a car, during a traversal, prone or outside a Jump clip.</summary>
        internal bool TryJumpHead(out Vector3 position)
        {
            position = Vector3.zero;
            Transform head;
            if (_inCar || Traversal != null || _prone > .01f || _actionW <= .001f || _actionClip == null || !_actionClip.Contains("Jump") || !Bones.TryGetValue("mixamorig:Head", out head)) return false;
            position = head.position; return true;
        }
    }
}
