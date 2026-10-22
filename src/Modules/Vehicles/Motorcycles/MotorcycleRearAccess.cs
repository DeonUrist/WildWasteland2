using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Saved component on motorcycle chassis: Easy Save can restore old collider-enabled values after instantiating the corrected prefab, so the rear trim is re-applied.</summary>
    internal sealed class MotorcycleRearAccess : MonoBehaviour
    {
        /// <summary>Re-applies the rear collider trim when the chassis is enabled.</summary>
        private void OnEnable() { MotorcycleIntegration.TrimRearCollision(gameObject); }
        /// <summary>Re-applies the rear collider trim after Easy Save wrote the loaded values.</summary>
        private void Start() { MotorcycleIntegration.TrimRearCollision(gameObject); }
    }
}
