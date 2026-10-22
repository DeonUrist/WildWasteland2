using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Native wreck mechanics owned by Vehicles; traffic owns when a car becomes a wreck.</summary>
    public interface IVehicleWreck
    {
        void SetCondition(Transform part,float value);
        void StopTweens(Transform part);
        void QuietHinges(GameObject car);
        void StopRpm(Transform part);
        void Darken(GameObject car);
        void BlastAt(Vector3 at);
        void Deaden(GameObject car,bool truck);
        void RestoreDead(GameObject car,string body);
    }
    /// <summary>Traffic notification before native wreck controls are disabled.</summary>
    public interface IVehicleCrewWreck { void Disable(GameObject car); }
}
