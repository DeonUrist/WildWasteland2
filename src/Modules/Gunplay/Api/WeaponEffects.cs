using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Gunplay {
    /// <summary>Publishes the actual native melee audio cache.</summary>
    internal sealed class WeaponEffects : IWeaponEffects { public AudioClip StabClip()=>Tracers.StabClip(); }
}
