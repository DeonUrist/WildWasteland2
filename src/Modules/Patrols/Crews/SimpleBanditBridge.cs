using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    internal sealed class SimpleBanditTag : MonoBehaviour
    {
        internal bool Combat;
        private void Start(){SimpleBanditBridge.Apply(gameObject);}
        private void OnDestroy(){SimpleBanditBridge.Forget(gameObject);SimpleBanditBridge.Tags.Remove(this);}
    }
    // NPC Overhaul's "simple convoy bandit" mode for raider crews. WW: through Core.Services.Npc (the NPC module); null = NPC off,
    // exactly the old "NPCAI not installed" path (nothing is called).
    internal static class SimpleBanditBridge
    {
        internal static readonly List<SimpleBanditTag> Tags=new List<SimpleBanditTag>();
        internal static void Mark(GameObject who)
        {if(who==null)return;var tag=who.GetComponent<SimpleBanditTag>()??who.AddComponent<SimpleBanditTag>();if(!Tags.Contains(tag))Tags.Add(tag);Apply(who);}
        internal static void Fight(GameObject who)
        {if(who==null)return;Mark(who);who.GetComponent<SimpleBanditTag>().Combat=true;Apply(who);}
        internal static void Apply(GameObject who)
        {
            var npc=Core.Services.Npc;
            if(npc==null||who==null||!PatrolsModule.Running)return;
            try{npc.SetSimpleConvoyBanditState(who,PatrolsModule.SimpleConvoyBanditsAI.Value,who.GetComponent<SimpleBanditTag>()?.Combat==true);
                var actor=who.GetComponent<TrafficActor>();if(actor!=null)Target(who,TrafficFactions.FootTarget(actor.Faction,who.transform.position));}catch(Exception e){PatrolsModule.Verbose("Simple bandit adapter: "+e.Message);}
        }
        internal static void RefreshAll(){foreach(var tag in Tags)if(tag!=null)Apply(tag.gameObject);}
        internal static void Target(GameObject npc,GameObject target)
        {var api=Core.Services.Npc;if(api==null||npc==null)return;try{api.SetSimpleConvoyBanditTarget(npc,target);}catch(Exception e){PatrolsModule.Verbose("Faction target: "+e.Message);}}
        internal static void Forget(GameObject who)
        {var api=Core.Services.Npc;if(api==null||ReferenceEquals(who,null))return;try{api.SetSimpleConvoyBandit(who,false);}catch(Exception e){PatrolsModule.Verbose("Simple bandit release: "+e.Message);}}
        // WW: module shutdown - every tagged crew member goes back to the NPC module's normal AI
        internal static void ReleaseAll(){var api=Core.Services.Npc;if(api==null)return;foreach(var tag in Tags)if(tag!=null)try{api.SetSimpleConvoyBandit(tag.gameObject,false);}catch(Exception){}}
    }
}
