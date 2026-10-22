using UnityEngine;
using HutongGames.PlayMaker;
using WildWasteland.Core;
namespace WildWasteland.Modules.NPC
{
    /// <summary>NPC-owned magazine counting and native sensor-range eligibility.</summary>
    internal sealed class NpcShotPolicy : INpcShotPolicy
    {
        /// <summary>Reports the live NPC senses gate used by native shot observers.</summary>
        public bool Senses { get { return NPC.Senses.On; } }
        /// <summary>Reports whether NPC aiming or movement requires native ranged sensor extension.</summary>
        public bool ExtendRange { get { return NpcaiRuntime.Running && (NpcaiSettings.AimEnabled.Value || NpcaiSettings.BrainEnabled.Value); } }
        /// <summary>Counts the actual native ray as one magazine shot while the NPC owner runs.</summary>
        public void Shot(GameObject actor,Fsm fsm) { if(NpcaiRuntime.Running)Brain.Shot(actor,fsm); }
    }
}
