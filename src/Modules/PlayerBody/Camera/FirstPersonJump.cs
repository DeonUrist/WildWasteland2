using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    internal sealed partial class Body
    {
        internal bool TryJumpHead(out Vector3 position)
        {
            position = Vector3.zero;
            Transform head;
            if (_inCar || Traversal != null || _prone > .01f || _actionW <= .001f || _actionClip == null || !_actionClip.Contains("Jump") || !Bones.TryGetValue("mixamorig:Head", out head)) return false;
            position = head.position; return true;
        }
    }
}
