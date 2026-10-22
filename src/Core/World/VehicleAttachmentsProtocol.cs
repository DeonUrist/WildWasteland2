using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Native trailer joints and fitted-part classification owned by Vehicles.</summary>
    public interface IVehicleAttachments
    {
        bool IsTrailer(GameObject vehicle);
        bool Connected(GameObject vehicle,GameObject trailer);
        GameObject Attached(GameObject vehicle);
        void Connect(GameObject vehicle,GameObject trailer,bool preservePose=false);
        bool IsPart(Transform item);
        bool IsWarRig(GameObject item);
        void DetachTankers(GameObject vehicle);
    }
}
