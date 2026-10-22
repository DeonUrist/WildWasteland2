using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    internal partial class Crew
    {
        private sealed class DamagedWheel
        {
            internal Transform Part, Hinge;
            internal Vector3 Position;
            internal Quaternion Rotation;
        }
        private readonly List<DamagedWheel> _damagedWheels=new List<DamagedWheel>();
        private bool _partRetaliation;
        private float _wheelHitAt;
        private static readonly Collider[] BusThreats=new Collider[48];
        private static readonly RaycastHit[] BusSight=new RaycastHit[24];

        internal static void PartHit(GameObject part,GameObject attacker)
        {
            if(!PatrolsModule.Running||part==null||attacker==null)return;
            var crew=part.GetComponentInParent<Crew>();
            if(crew==null||crew.Marker==null||crew.Marker.Faction!="Coyotes"||crew.Marker.PlayerEntered||Explode.IsWreck(crew.gameObject))return;
            if(attacker.transform.IsChildOf(crew.transform)||attacker.CompareTag(crew.Marker.Faction))return;
            var hinge=part.transform.parent;
            if(hinge!=null&&hinge.name.StartsWith("hinge_wheel",System.StringComparison.Ordinal))
            {
                bool known=false;foreach(var row in crew._damagedWheels)if(row.Part==part.transform){known=true;break;}
                if(!known)crew._damagedWheels.Add(new DamagedWheel{Part=part.transform,Hinge=hinge,Position=part.transform.localPosition,Rotation=part.transform.localRotation});
                crew._wheelHitAt=Time.time;
            }
            if(!attacker.transform.IsChildOf(crew.transform)&&!attacker.CompareTag(crew.Marker.Faction))crew._partRetaliation=true;
            Attacked(part,attacker);
            if(crew.CombatEnemy==attacker)
            {
                var api=Core.Services.Npc;
                if(api!=null){if(crew.Marker.Driver!=null)api.Remember(crew.Marker.Driver,attacker,15f);if(crew.Marker.Passenger!=null)api.Remember(crew.Marker.Passenger,attacker,15f);}
                PatrolsModule.Verbose("Crew: vehicle part hit; defending against "+attacker.name);
            }
        }

        private void FindBusThreat()
        {
            if(Marker.Faction!="Coyotes"||Core.Services.Npc==null)return;
            var current=CombatEnemy;
            if(current!=null&&(current.transform.position-transform.position).sqrMagnitude<=2500f)return;
            if(current!=null&&Time.time-_attackedAt<4f)return;
            _combatEnemy=null;_enemyHealth=null;
            int count=Physics.OverlapSphereNonAlloc(transform.position,25f,BusThreats,Physics.AllLayers,QueryTriggerInteraction.Collide);
            GameObject best=null;float distance=625f;
            for(int i=0;i<count;i++)
            {
                var collider=BusThreats[i];if(collider==null||collider.transform.IsChildOf(transform))continue;
                for(var t=collider.transform;t!=null;t=t.parent)
                {
                    if(!t.CompareTag("Mutant")&&!t.CompareTag("Carnivore")&&!t.CompareTag("Scrapyard"))continue;
                    var health=CrewReturn.HealthVar(t.gameObject);if(health==null||health.Value<=0f)continue;
                    float d=(t.position-transform.position).sqrMagnitude;if(d>=distance)break;
                    var from=transform.position+Vector3.up*2f;var delta=t.position+Vector3.up-from;
                    int hits=Physics.RaycastNonAlloc(from,delta.normalized,BusSight,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                    bool blocked=hits==BusSight.Length;
                    for(int j=0;j<hits;j++)if(!BusSight[j].transform.IsChildOf(transform)&&!BusSight[j].transform.IsChildOf(t))blocked=true;
                    if(!blocked){best=t.gameObject;distance=d;}break;
                }
            }
            if(best!=null){_combatEnemy=best;_enemyHealth=CrewReturn.HealthVar(best);_attackedAt=Time.time;}
        }

        private void RepairWheels()
        {
            if(_damagedWheels.Count==0||Core.SaveBus.Busy||Marker.Faction!="Coyotes"||Explode.IsWreck(_car)||Time.time-_wheelHitAt<10f||_rb!=null&&_rb.velocity.sqrMagnitude>1f)return;
            var player=PlayerRef.Player;if(player!=null&&(player.position-transform.position).sqrMagnitude<3600f)return;
            var enemy=CombatEnemy;if(enemy!=null&&(enemy.transform.position-transform.position).sqrMagnitude<=2500f)return;
            bool alive=DriverSeatedAlive;
            if(Regroup!=null)foreach(var member in Regroup.Members)if(member.Body!=null&&(member.Health==null||member.Health.Value>0f))alive=true;
            if(!alive)return;
            for(int i=_damagedWheels.Count-1;i>=0;i--)
            {
                var row=_damagedWheels[i];var part=row.Part;
                if(part==null||row.Hinge==null){_damagedWheels.RemoveAt(i);continue;}
                bool fitted=part.parent==row.Hinge&&part.CompareTag("vehPart");
                // A stolen/mounted wheel belongs to its new owner. Only recover the original loose wheel nearby.
                if(!fitted&&(part.parent!=null||!part.CompareTag("vehPartRemoved")||(part.position-transform.position).sqrMagnitude>900f))continue;
                bool occupied=false;for(int n=0;n<row.Hinge.childCount;n++){var child=row.Hinge.GetChild(n);if(child!=part&&child.CompareTag("vehPart"))occupied=true;}
                if(occupied)continue;
                var condition=Core.FsmUtil.Find(part.gameObject,"Condition")?.FsmVariables.FindFsmFloat("Condition");
                if(condition==null)continue;
                condition.Value=100f;
                if(!fitted)
                {
                    var body=part.GetComponent<Rigidbody>();if(body!=null){body.isKinematic=true;Object.Destroy(body);}
                    part.SetParent(row.Hinge,false);part.localPosition=row.Position;part.localRotation=row.Rotation;part.gameObject.layer=8;part.tag="vehPart";
                    var check=Core.FsmUtil.Find(part.gameObject,"CheckTag");if(check!=null&&check.Fsm.Initialized)check.Fsm.SetState("vehPart");
                }
                _damagedWheels.RemoveAt(i);PatrolsModule.Verbose("Crew: repaired and refitted "+part.name);return;
            }
        }
    }
}
