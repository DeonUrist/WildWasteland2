using UnityEngine;
using System.Collections.Generic;
using HutongGames.PlayMaker;
namespace WildWasteland.Modules.NPC
{
    internal static partial class Senses
    {
        private sealed class CrewCommand { internal GameObject Npc,Enemy;internal bool Boarding;internal float Until; }
        private static readonly Dictionary<int,CrewCommand> _crewOrders=new Dictionary<int,CrewCommand>();
        private static void PruneCrewOrders(float now)
        {
            if(_crewOrders.Count==0)return;_dead.Clear();
            foreach(var p in _crewOrders)if(p.Value.Npc==null||now>p.Value.Until)_dead.Add(p.Key);
            for(int i=0;i<_dead.Count;i++)_crewOrders.Remove(_dead[i]);
        }
        private static void RecallCrewOrder(Agent a)
        { CrewCommand c;if(!_crewOrders.TryGetValue(a.Owner.GetInstanceID(),out c))return;_crewOrders.Remove(a.Owner.GetInstanceID());if(c.Npc==a.Owner&&Time.time<=c.Until)CrewOrder(c.Npc,c.Enemy,c.Boarding); }
        internal static bool CrewOrder(GameObject npc,GameObject enemy,bool boarding)
        {
            if(npc==null)return false;
            int id=npc.GetInstanceID();
            var a=Get(npc);
            if(a==null&&enemy==null&&!boarding){_crewOrders.Remove(id);return true;}
            if(a==null)a=TryRegister(npc);
            if(a==null)
            {
                CrewCommand c;if(!_crewOrders.TryGetValue(id,out c)){c=new CrewCommand{Npc=npc};_crewOrders[id]=c;}
                c.Enemy=enemy;c.Boarding=boarding;c.Until=Time.time+10f;return false;
            }
            if(a.CrewBoarding==boarding&&ReferenceEquals(a.CrewEnemy,enemy))return true;
            a.CrewBoarding=boarding;a.CrewEnemy=enemy;a.CrewEnemyHealth=null;
            if(enemy!=null)foreach(var f in enemy.GetComponents<PlayMakerFSM>())if(f!=null&&f.FsmName=="Health"){a.CrewEnemyHealth=f.FsmVariables.GetFsmFloat("Health");break;}
            Release(a);a.State=State.Idle;a.Target=null;a.Pursue=null;a.Pursuits=0;a.SeenFor=0f;
            if(a.DetectedVar!=null)a.DetectedVar.Value=null;
            a.NextLook=Time.time;
            if(boarding)
            {
                _remember.Remove(id);_handover.Remove(id);
                a.Fought=null;a.FoughtUntil=0f;
                foreach(var f in npc.GetComponents<PlayMakerFSM>())if(f!=null&&(f.FsmName=="Attack"||f.FsmName=="Damage"||f.FsmName=="Damage Ranged"||f.FsmName=="RangedAttackWait"))f.enabled=false;
            }
            else if(enemy!=null){Engage(a,enemy);if(a.DetectedVar!=null)a.DetectedVar.Value=enemy;}
            return true;
        }
    }
}
