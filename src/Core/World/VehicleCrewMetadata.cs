using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Patrols-owned identity metadata used by vehicle presentation without exposing runtime components.</summary>
    public interface IVehicleCrewMetadata
    {
        string Faction(GameObject vehicle);
        bool HasCrewController(GameObject actor);
    }
}
