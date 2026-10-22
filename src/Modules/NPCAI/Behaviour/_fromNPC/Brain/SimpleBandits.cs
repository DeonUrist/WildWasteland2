using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    internal static class SimpleBandits
    {
        private static readonly Dictionary<int,GameObject> Members=new Dictionary<int,GameObject>();
        private static readonly HashSet<int> Combat=new HashSet<int>();
        private static readonly Dictionary<int,GameObject> Targets=new Dictionary<int,GameObject>();
        internal static void Target(GameObject npc,GameObject target){if(Has(npc))Targets[npc.GetInstanceID()]=target;}
        internal static GameObject Goal(GameObject npc){if(npc!=null&&Targets.TryGetValue(npc.GetInstanceID(),out var target))return target;return Senses.PlayerObj;}
        internal static void Set(GameObject npc,bool enabled)
        {Set(npc,enabled,false);}
        internal static void Set(GameObject npc,bool enabled,bool combat)
        {if(npc==null)return;int id=npc.GetInstanceID();if(enabled)Members[id]=npc;else{Members.Remove(id);Targets.Remove(id);}if(enabled&&combat)Combat.Add(id);else Combat.Remove(id);}
        internal static bool Has(GameObject npc)
        {return npc!=null&&Members.TryGetValue(npc.GetInstanceID(),out var member)&&member==npc;}
        internal static bool Fighting(GameObject npc){return Has(npc)&&npc.transform.parent==null&&Combat.Contains(npc.GetInstanceID());}
        internal static void Clear(){Members.Clear();Combat.Clear();Targets.Clear();}
        internal static void Remove(GameObject npc){if(ReferenceEquals(npc,null))return;int id=npc.GetInstanceID();Members.Remove(id);Combat.Remove(id);Targets.Remove(id);}
    }

    internal static partial class Senses
    {
        private static void SimpleLook(Agent a,float now)
        {
            float interval=0.8f+a.Stagger;
            a.NextLook=now+interval;
            bool sanity=a.Tag=="MutantSanity";
            if(!sanity&&!SimpleBandits.Fighting(a.Owner))return;
            var target=sanity?PlayerObj:SimpleBandits.Goal(a.Owner);
            // (crew AI) Patrols' goal is the player whenever the faction is hostile (any distance): the crew on foot take him only through the
            // senses (SimplePlayer) or while they remember fighting him (then also over a faction target Patrols picked meanwhile)
            if(!sanity&&target!=null&&_player!=null&&(IsPlayer(target)||(a.Target==_player&&Remembers(a,_player,now))))target=SimplePlayer(a,now,interval);
            if(target==null){Release(a);a.State=State.Idle;a.Target=null;if(a.DetectedVar!=null)a.DetectedVar.Value=null;return;}
            // Combat dismounts and insanity enemies keep their real target; no ghosts or search (the player: see SimplePlayer).
            if(a.Ghost!=null||a.State!=State.Combat||a.Target!=target)
            {Release(a);if(sanity)Log(a,"pursues the player (insanity)");}
            a.State=State.Combat;a.Target=target;a.UnseenFor=0f;a.LastSeen=target.transform.position;
            if(a.DetectedVar!=null)a.DetectedVar.Value=target;
        }
        // (crew AI) a convoy crew member on foot and the player: noticed as any NPC notices (sight in its cone over NoticeSeconds..NoticeFar,
        // his shots and hits through the memory), kept while seen (tracked outside the cone) or while it remembers fighting him (his real
        // position), dropped LoseSeconds after the last sight once that memory is over -> idle (no ghost, no search)
        private static GameObject SimplePlayer(Agent a,float now,float interval)
        {
            var p=_player;
            bool fighting=(a.State==State.Combat||a.State==State.Hide)&&a.Target==p;
            float sr=NpcModule.SightRange.Value;
            Vector3 to=p.transform.position-a.T.position;
            bool vis=to.sqrMagnitude<=sr*sr&&Visible(a,Eye(a),p,true,_playerInStorm);
            if(vis)
            {
                a.SeenFor+=interval;a.UnseenSince=-1f;a.MemTrack=false;
                if(fighting){FoughtFor(a,p,now,"fights him");return p;}
                if(Remembers(a,p,now)){if(NpcModule.SensesLog.Value)Log(a,"sees the player it remembers fighting, fights (convoy crew on foot)");return p;}
                if(a.SeenFor>=NoticeNeed(to.magnitude))
                {
                    if(NpcModule.SensesLog.Value)Log(a,"notices the player at "+to.magnitude.ToString("0")+" m (convoy crew on foot)");
                    FoughtFor(a,p,now,"engages him");return p;
                }
                return null;
            }
            a.SeenFor=Mathf.Max(0f,a.SeenFor-interval);
            if(fighting&&a.UnseenSince<0f)a.UnseenSince=now;
            if(Remembers(a,p,now))
            {
                if(fighting&&!a.MemTrack){a.MemTrack=true;if(NpcModule.SensesLog.Value){a.UnseenFor=now-a.UnseenSince;Log(a,SightLossText(a,p)+" - kept: remembers fighting the player ("+(a.FoughtUntil-now).ToString("0")+" s more), follows his real position (convoy crew on foot)");}}
                else if(!fighting&&NpcModule.SensesLog.Value)Log(a,"remembers fighting the player, goes for him (convoy crew on foot)");
                return p;
            }
            if(!fighting)return null;
            float unseen=now-a.UnseenSince;
            if(unseen<Mathf.Max(0f,NpcModule.LoseSeconds.Value))return p;
            a.UnseenFor=unseen;
            if(NpcModule.SensesLog.Value)Log(a,SightLossText(a,p)+(a.MemTrack?" - its memory of fighting the player is over":"")+" - convoy crew on foot: drops him, idle");
            a.MemTrack=false;a.UnseenSince=-1f;a.SeenFor=0f;
            return null;
        }
        internal static bool SimpleSight(Transform from,GameObject owner,GameObject target)
        {
            var eye=from.position+Vector3.up*1.3f;var to=target.transform.position+Vector3.up-eye;
            int n=Physics.RaycastNonAlloc(eye,to.normalized,SimpleHits,to.magnitude,Physics.DefaultRaycastLayers&~(1<<8),QueryTriggerInteraction.Ignore);
            if(n==SimpleHits.Length)return false;
            for(int i=0;i<n;i++){var c=SimpleHits[i].collider;if(c!=null&&!c.transform.IsChildOf(owner.transform)&&!c.transform.IsChildOf(target.transform))return false;}
            return true;
        }
        private static readonly RaycastHit[] SimpleHits=new RaycastHit[16];
    }
    internal static partial class Brain
    {
        // (crew AI) a convoy crew member on foot: holds and shoots inside 70 % of its gun's range with a clear line, otherwise chases with the
        // brain's own walk (MoveToward: the baked map's route out of caves / camps with its leg gate, the feelers, progress / blocked checks,
        // back-ups, rests and hops). No cover, ghosts or engine checks. Chasing it thinks at the brain's rate (same per-frame cap and phase);
        // holding it re-checks every 0.6 s (x TickScale) on its own phase, as before.
        private static void SimpleThink(Npc n,float now)
        {
            var target=n.Target.Value;if(target==null){if(n.Mode!=Mode.Off)SetMode(n,Mode.Off,"no target");return;}
            n.ToGhost=false;n.SpeedScale=1f;
            Vector3 tp=target.transform.position;
            Vector3 d3=tp-n.T.position;d3.y=0f;float d=d3.magnitude;n.Dist=d;
            if(n.Mode==Mode.BackUp){if(now<n.ModeUntil)return;SetMode(n,Mode.Chase,"backed up");}
            if(n.Mode==Mode.Rest){if(now<n.ModeUntil)return;SetMode(n,Mode.Chase,"rested");}
            bool hold=n.Ranged&&d<WeaponRanges.RangeOf(n.Kind)*0.7f&&Senses.SimpleSight(n.T,n.Owner,target);
            n.Los=hold;
            if(hold)
            {
                if(n.Mode!=Mode.Hold)SetMode(n,Mode.Hold,"simple convoy bandit");
                n.OnNav=false;
                n.NextTick+=Mathf.Max(0f,(0.6f-_interval)*n.TickScale);     // the hold's 0.6 s re-check, on this NPC's phase
                return;
            }
            if(n.Mode!=Mode.Chase)SetMode(n,Mode.Chase,"simple convoy bandit");
            MoveToward(n,tp,d3,d,now);
        }
    }
}
