using UnityEngine;
using HutongGames.PlayMaker;
namespace WildWasteland.Core
{
    /// <summary>NPC magazine/perception policy consumed by the native Gunplay shot observer.</summary>
    public interface INpcShotPolicy { bool Senses { get; } bool ExtendRange { get; } void Shot(GameObject actor,Fsm fsm); }
}
