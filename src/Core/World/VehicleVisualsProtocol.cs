using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Presentation-owned repair and cargo artwork for current authored vehicle frames.</summary>
    public interface IVehicleVisuals
    {
        void Paint(GameObject vehicle,string body,string faction);
        void Cargo(GameObject vehicle,string key);
        void RepairFrame(GameObject vehicle,GameObject prefab);
    }
}
