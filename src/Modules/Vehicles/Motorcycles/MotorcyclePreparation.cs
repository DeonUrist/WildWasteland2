using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;
namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Native rear-collider preparation for an optional external chassis.</summary>
    internal sealed class MotorcyclePreparation : IMotorcyclePreparation
    {
        /// <summary>Uses the same native trim and access component as locally built motorcycles.</summary>
        public void Prepare(GameObject car) { if(car==null)return;MotorcycleIntegration.TrimRearCollision(car);if(car.GetComponent<MotorcycleRearAccess>()==null)car.AddComponent<MotorcycleRearAccess>(); }
    }
}
