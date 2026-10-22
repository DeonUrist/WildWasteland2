using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Optional cave-only ambient field, independent of the world daylight provider.</summary>
    public interface ICaveAmbient
    {
        /// <summary>Samples a scene position: 0..1 light, or -1 when the optional cave source cannot answer.</summary>
        float At(Vector3 scenePosition);
    }
}
