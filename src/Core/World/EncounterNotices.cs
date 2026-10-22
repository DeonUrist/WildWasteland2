using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>POI encounter markers for presentation, in stable world coordinates.</summary>
    public interface IEncounterNotices { float NoticeUntil {get;} int RigCount {get;} Vector3 RigPosition(int index); }
    /// <summary>Vehicles-owned toggle governing natural War Rig encounters.</summary>
    public interface IWarRigWorld {
        bool Running { get; } bool SpawnEnabled { get; } string TemplateName { get; }
        GameObject Template();void SpawnNearPlayer();bool InsideTank(GameObject actor);
        bool IsTemplate(GameObject prefab);bool CanSpawnAt(ref Vector3 at,Quaternion rotation);
    }
}
