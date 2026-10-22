using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>POI encounter markers for presentation, in stable world coordinates.</summary>
    public interface IEncounterNotices { float NoticeUntil {get;} int RigCount {get;} Vector3 RigPosition(int index); }
    /// <summary>Vehicles-owned toggle governing natural War Rig encounters.</summary>
    public interface IWarRigWorld { bool SpawnEnabled {get;} }
}
