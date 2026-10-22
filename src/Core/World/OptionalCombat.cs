using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Native combat effects exposed by their actual Gunplay owner.</summary>
    public interface IWeaponEffects { AudioClip StabClip(); }
    /// <summary>Actual NPC fear operations requested by external weapon contacts.</summary>
    public interface INpcFear { void FireRetreat(GameObject actor,Vector3 source,bool burning);void ChainsawWitnesses(GameObject corpse); }
    /// <summary>Noise multiplier supplied by external held-weapon integrations.</summary>
    public interface IEquipmentNoise { float FootstepScale { get; } }
}
