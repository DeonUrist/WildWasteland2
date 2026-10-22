using HutongGames.PlayMaker;
using UnityEngine;
namespace WildWasteland.Modules.Patrols
{
    internal partial class Crew
    {
        private GameObject _combatEnemy;
        private FsmFloat _enemyHealth;
        private float _attackedAt=-1e6f,_slowSince=-1f,_nextCombat;
        private bool _resumeChase,_fullyStuck;
        private int _frontExitIndex;
        internal bool CombatExit;
        internal bool BusDefence;
        internal bool FullyStuck { get { return _fullyStuck||_outOfFuel||Marker!=null&&Marker.StuckBails>0; } }
        internal Vector3 FrontExitOffset()
        { int n=_frontExitIndex++%9;return transform.forward*(n%3==1?0.8f:n%3==2?-0.8f:0f)+transform.right*(n/3*0.8f); }
        internal GameObject CombatEnemy { get { return _combatEnemy!=null&&_combatEnemy.activeInHierarchy&&(_enemyHealth==null||_enemyHealth.Value>0f)?_combatEnemy:null; } }
        internal bool CombatPursuit { get { return _resumeChase&&CombatEnemy!=null&&Regroup==null; } }

        internal static void Attacked(GameObject victim,GameObject attacker)
        {
            if(!PatrolsModule.Running||victim==null||attacker==null)return;
            var crew=victim.GetComponentInParent<Crew>()??CrewReturn.Owner(victim);
            if(crew==null||crew._car==null||attacker.transform.IsChildOf(crew.transform)||crew.Marker==null||crew.Marker.PlayerEntered)return;
            if(crew.Marker.SetupOnly&&crew.GetComponent<MerchantHost>()==null)return;
            if(PlayerRef.Player!=null&&attacker==PlayerRef.Player.gameObject&&!crew._partRetaliation&&PatrolPoi.Ignored(crew._car))return;
            if(attacker.CompareTag(crew.Marker.Faction))return;
            crew._combatEnemy=attacker;crew._enemyHealth=CrewReturn.HealthVar(attacker);crew._attackedAt=Time.time;
            crew._nextCombat=0f;
            if(PlayerRef.Player!=null&&attacker==PlayerRef.Player.gameObject)crew.Marker.PlayerContactAt=Time.time;
            if(crew.Marker.SetupOnly)crew.Marker.SetupOnly=false;
            if((attacker.transform.position-crew.transform.position).sqrMagnitude>2500f&&crew.Regroup==null&&!crew.MerchantWaiting&&!crew.TownParking)crew.ResumePursuit();
        }

        private bool CombatTick()
        {
            if(Time.time<_nextCombat)return false;_nextCombat=Time.time+0.5f;
            bool slow=_rb==null||_rb.velocity.sqrMagnitude<1f;
            if(Marker==null||Marker.PlayerEntered||PlayerInside())return false;
            RepairWheels();
            if(slow&&(MerchantWaiting||TownParking||BusDefence))FindBusThreat();
            if(!slow){_slowSince=-1f;if(!CombatExit)_fullyStuck=false;}else if(_slowSince<0f)_slowSince=Time.time;
            if(PoiTick(slow))return true;
            var enemy=CombatEnemy;
            if(enemy!=null&&PlayerRef.Player!=null&&enemy==PlayerRef.Player.gameObject&&!_partRetaliation&&PatrolPoi.Ignored(_car)){_combatEnemy=null;_resumeChase=false;return false;}
            if(enemy==null){_resumeChase=false;return false;}
            if(Regroup!=null||_done||!slow||Time.time-_slowSince<1f||Time.time-_attackedAt>4f||PlayerInside())return false;
            if((enemy.transform.position-transform.position).sqrMagnitude>2500f&&!FullyStuck)return false;
            bool answer=false,close=false,alive=false;
            for(int i=-2;i<Marker.Riders.Length;i++)
            {
                var person=i==-2?_driver:i==-1?Marker.Passenger:Marker.Riders[i];if(person==null)continue;
                var health=CrewReturn.HealthVar(person);if(health!=null&&health.Value<=0f)continue;
                alive=true;if((person.transform.position-enemy.transform.position).sqrMagnitude<12.25f)close=true;
                var gun=person.GetComponent<PassengerGuard>();if(gun!=null&&gun.CanAnswer(enemy))answer=true;
            }
            if(!alive||(!close&&answer&&!MerchantWaiting&&!TownParking))return false;
            BusDefence=Marker.Faction=="Coyotes"&&(MerchantWaiting||TownParking);
            _attackedAt=-1e6f;return ExitCrew(false);
        }

        internal void ResumePursuit()
        {
            if(BusDefence){BusDefence=false;_combatEnemy=null;_enemyHealth=null;_resumeChase=false;_partRetaliation=false;return;}
            _resumeChase=CombatEnemy!=null;
            if(_resumeChase)Hold=false;
            var merchant=GetComponent<MerchantHost>();
            if(merchant!=null)merchant.Park();
            if(_resumeChase){Hold=false;WaitForMerchant(false);}
        }

        internal bool PursuitTarget(out Vector3 position,out Vector3 velocity,out GameObject car)
        {
            position=velocity=Vector3.zero;car=null;
            var enemy=CombatEnemy;if(!CombatPursuit||enemy==null)return false;
            if(PlayerRef.Player!=null&&enemy==PlayerRef.Player.gameObject)return (_partRetaliation||!PatrolPoi.Ignored(_car))&&PlayerRef.Target(out position,out velocity,out car);
            position=enemy.transform.position;var marker=enemy.GetComponentInParent<PatrolMarker>();
            if(marker!=null)car=marker.gameObject;
            var body=car!=null?car.GetComponent<Rigidbody>():enemy.GetComponent<Rigidbody>();if(body!=null)velocity=body.velocity;
            return true;
        }
    }
}
