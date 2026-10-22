using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Patrols;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Applies actual authored faction paint, cargo artwork and frame renderer repair.</summary>
    internal sealed class VehicleVisuals : IVehicleVisuals
    {
        /// <summary>Paints the exact current authored body/faction.</summary>
        public void Paint(GameObject vehicle,string body,string faction) { WildWasteland.Modules.Presentation.Paint.Apply(vehicle,body,faction); }
        /// <summary>Applies the existing chosen cargo panel texture.</summary>
        public void Cargo(GameObject vehicle,string key) {WildWasteland.Modules.Presentation.Paint.ApplyCargo(vehicle,key);}
        /// <summary>Repairs native frame renderers using the existing reference prefab.</summary>
        public void RepairFrame(GameObject vehicle,GameObject prefab) {WildWasteland.Modules.Presentation.Paint.RepairFrame(vehicle,prefab);}
    }
}
