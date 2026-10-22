using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    internal sealed class TrafficActor : MonoBehaviour
    {
        internal string Faction;
        internal string Battle;
        internal int Side;
        internal FsmFloat Health;
        private readonly List<KeyValuePair<Behaviour,System.Reflection.PropertyInfo>> _sensors=new List<KeyValuePair<Behaviour,System.Reflection.PropertyInfo>>();
        private bool _relationSet,_hostile;
        private float _next;
        private static readonly List<PlayMakerFSM> Fs=new List<PlayMakerFSM>();
        /// <summary>Start: health variable and the sensors whose AllowedTags follow the faction relation.</summary>
        private void Start()
        {
            foreach(var f in GetComponents<PlayMakerFSM>())if(f.FsmName=="Health")Health=f.FsmVariables.GetFsmFloat("Health");
            foreach(var sensor in GetComponentsInChildren<Behaviour>(true))if(sensor!=null&&(sensor.GetType().Name=="RangeSensor"||sensor.GetType().Name=="RaySensor"))
            {var property=sensor.GetType().GetProperty("AllowedTags");if(property!=null&&property.CanWrite&&property.PropertyType==typeof(string[]))_sensors.Add(new KeyValuePair<Behaviour,System.Reflection.PropertyInfo>(sensor,property));}
            Relations();
        }
        /// <summary>Applies the faction's enemy tags (the player only while hostile) to the vanilla sensors.</summary>
        private void Relations()
        {
            bool hostile=TrafficFactions.PlayerHostile(Faction);if(_relationSet&&hostile==_hostile)return;_relationSet=true;_hostile=hostile;
            var tags=Faction=="Coyotes"?new List<string>{"Scrapyard","Mutant","Carnivore"}:new List<string>{"Coyotes","Mutant","Carnivore"};if(hostile)tags.Add("Player");var values=tags.ToArray();
            foreach(var sensor in _sensors)if(sensor.Key!=null)sensor.Value.SetValue(sensor.Key,values,null);
        }
        /// <summary>Registers the actor with the faction actor list.</summary>
        private void OnEnable(){if(!TrafficFactions.Actors.Contains(this))TrafficFactions.Actors.Add(this);}
        /// <summary>Removes the actor from the faction actor list.</summary>
        private void OnDisable(){TrafficFactions.Actors.Remove(this);}
        /// <summary>Every 0.6 s: relations refreshed and, on foot, the simple-bandit goal set to the faction target.</summary>
        private void Update()
        { if (!PatrolsModule.Running) return;
            if(Time.time<_next)return;_next=Time.time+0.6f;Relations();if(transform.parent!=null)return;
            if(Health==null){Fs.Clear();GetComponents(Fs);foreach(var f in Fs)if(f.FsmName=="Health")Health=f.FsmVariables.GetFsmFloat("Health");Fs.Clear();}
            if(Health!=null&&Health.Value<=0f)return;
            SimpleBanditBridge.Target(gameObject,TrafficFactions.FootTarget(Faction,transform.position));   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
        }
    }
}
