using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Shared numeric world-space probes and geometry helpers.</summary>
    public static class WorldGeometry
    {
        /// <summary>Horizontal distance between two scene or stable-world points.</summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b) { float x = a.x - b.x, z = a.z - b.z; return Mathf.Sqrt(x * x + z * z); }
        /// <summary>Finds loaded ground beneath a scene X/Z position using the original native broad ray.</summary>
        public static bool GroundAt(float x, float z, out Vector3 position)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(x, 1500f, z), Vector3.down, out hit, 4000f, ~0, QueryTriggerInteraction.Ignore)) { position = hit.point; return true; }
            position = Vector3.zero; return false;
        }
    }
}
