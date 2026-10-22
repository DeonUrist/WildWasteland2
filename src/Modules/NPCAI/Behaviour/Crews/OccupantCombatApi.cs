using System;
using UnityEngine;
using HutongGames.PlayMaker;
using WildWasteland.Core;
namespace WildWasteland.Modules.NPC
{
    /// <summary>Actual NPC guard implementation; traffic callers retain no private component dependency.</summary>
    internal sealed class OccupantCombatApi : IOccupantCombat
    {
        public Component Attach(GameObject actor,GameObject car,Transform seat,bool driver=false,bool turret=false,float arc=0f)
        {if(PassengerGuard.Policy==null)return null;if(driver)return PassengerGuard.AttachDriver(actor,car,seat);if(turret)return PassengerGuard.AttachTurret(actor,car,seat,arc);PassengerGuard.Attach(actor,car,seat);return actor.GetComponent<PassengerGuard>();}
        public void Prepare(GameObject actor,bool allowed)=>PassengerGuard.Prepare(actor,allowed);
        public void NeutralizeContact(GameObject actor)=>PassengerGuard.NeutralizeContact(actor);
        public bool IsRanged(GameObject actor)=>PassengerGuard.IsRangedHuman(actor);
        public bool IsRangedName(string name)=>Array.Exists(PassengerGuard.RangedHumans,n=>string.Equals(n,name,StringComparison.OrdinalIgnoreCase));
        public bool Engaged(GameObject actor)=>actor!=null&&actor.GetComponent<PassengerGuard>()!=null;
        public bool CanAnswer(GameObject actor,GameObject enemy){var guard=actor!=null?actor.GetComponent<PassengerGuard>():null;return guard!=null&&guard.CanAnswer(enemy);}
        public void Promote(GameObject actor,Transform seat){var guard=actor!=null?actor.GetComponent<PassengerGuard>():null;if(guard!=null&&PassengerGuard.Policy!=null)guard.SetDriverMode(seat);}
        public void Release(GameObject actor){var guard=actor!=null?actor.GetComponent<PassengerGuard>():null;if(guard!=null){guard.enabled=false;UnityEngine.Object.Destroy(guard);}}
        public void Mute(GameObject actor,bool combat)=>NpcSeatState.MuteAi(actor,combat?PassengerGuard.CombatFsms:null);
        public void Mute(PlayMakerFSM[] fsms,bool combat)=>NpcSeatState.MuteAi(fsms,combat?PassengerGuard.CombatFsms:null);
        public Component Follow(GameObject actor,GameObject car,Transform seat){var component=Attach(actor,car,seat);var guard=component as PassengerGuard;if(guard!=null)guard.QuestRanged();return component;}
    }
}
