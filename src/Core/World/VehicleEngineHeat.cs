using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Reversible native engine heat control, owned and restored by Vehicles.</summary>
    public interface IVehicleEngineHeat
    {
        /// <summary>Suppresses heat while held; false restores the captured engine values.</summary>
        void Hold(GameObject car, bool cool);
    }
}
