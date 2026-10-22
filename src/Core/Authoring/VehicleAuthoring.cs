using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Vehicles-owned native authoring queries used by template capture tools.</summary>
    public interface IVehicleAuthoring
    {
        /// <summary>Recognizes the actual authored motorcycle chassis name.</summary>
        bool IsMotorcycleBody(string name);
        /// <summary>Returns the currently attached native trailer.</summary>
        GameObject AttachedTrailer(GameObject vehicle);
        /// <summary>Recognizes a native trailer frame.</summary>
        bool IsTrailer(GameObject vehicle);
        bool IsPart(Transform part);
        bool IsFreeAttachment(GameObject part);
        /// <summary>Reads current native suspension adjustment without changing it.</summary>
        bool ApplySuspension(GameObject vehicle,float width,float height,string reason);
        string SuspensionDescription(GameObject vehicle);
        GameObject Template(string name);
        void RefreshTemplates();
        void ReadSuspension(GameObject vehicle,out float width,out float height);
    }
}
