using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Faction relations (Coyotes/Scrapyard normalisation, opposed check, player hostility from the game's Coyote relation FSM); Core Factions service in Wave B (Wave A split of Patrols Traffic/TrafficFactions.cs).</summary>
    internal static partial class TrafficFactions
    {
        private static PlayMakerFSM _relation;private static float _findRelation;
        private static PlayMakerFSM _relationOf;private static FsmInt _relationOnOff;   // Perf: "OnOff" resolved once per relation FSM (was FindFsmInt by name per call)

        internal static string Normalize(string faction)=>string.Equals(faction,"Coyotes",StringComparison.OrdinalIgnoreCase)?"Coyotes":"Scrapyard";
        internal static bool Opposed(string a,string b)=>Normalize(a)!=Normalize(b);
        internal static bool PlayerHostile(string faction)
        {
            if(Normalize(faction)!="Coyotes")return true;
            if(_relation==null&&Time.time>=_findRelation)
            {_findRelation=Time.time+2f;var manager=Core.GameRefs.GameManager;if(manager!=null)foreach(var f in manager.GetComponents<PlayMakerFSM>())if(f.FsmName=="FactionRelation_Coyotes"){_relation=f;break;}}
            if(_relation==null||!_relation.Fsm.Initialized)return false;
            if(_relation.ActiveStateName=="PlayerEnemy")return true;
            if(!ReferenceEquals(_relationOf,_relation)||_relationOnOff==null){_relationOnOff=_relation.FsmVariables.FindFsmInt("OnOff");_relationOf=_relationOnOff!=null?_relation:null;}
            return _relationOnOff!=null&&_relationOnOff.Value>0;
        }
    }
}
