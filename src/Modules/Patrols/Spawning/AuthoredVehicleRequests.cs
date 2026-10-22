using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Parses current authored JSON once per definition and queues real mechanical preparation and Patrols completion.</summary>
    internal sealed class AuthoredVehicleRequests : IAuthoredVehicleRequests
    {
        private readonly Dictionary<string,CarTemplate> _templates=new Dictionary<string,CarTemplate>(StringComparer.Ordinal);
        /// <summary>Resolves a definition without modifying the globally authored park.</summary>
        private CarTemplate Resolve(string definition) { if(string.IsNullOrEmpty(definition))return null;CarTemplate value;if(_templates.TryGetValue(definition,out value))return value;value=CarTemplates.Parse(definition,"Authored vehicle","POI","API");if(value!=null)_templates.Add(definition,value);return value; }
        /// <summary>Prepares every structural variant of the authored definition through Vehicles.</summary>
        public bool Prepare(string definition) { var t=Resolve(definition);if(t==null||!t.Available)return false;if(ModApi.Get<IVehicleAssembly>()==null)return false;VehiclePreparation.Warm(t);return true; }
        /// <summary>Queues a clone of the immutable authored template for the normal build/crew/cargo pipeline.</summary>
        public void Spawn(string definition,Vector3 position,Quaternion rotation,Action<GameObject> completed) { Spawn(new AuthoredVehicleSpawn {Definition=definition},position,rotation,completed); }
        /// <summary>Copies encounter overrides into one actual queued instance, preserving cached definitions.</summary>
        public void Spawn(AuthoredVehicleSpawn request,Vector3 position,Quaternion rotation,Action<GameObject> completed)
        {
            var template=request!=null?Resolve(request.Definition):null;if(template==null||!PatrolsModule.Running){if(completed!=null)completed(null);return;}
            var t=template.CloneForSpawn();t.SetupOnly=request.SetupOnly;
            if(request.Loot!=null)t.SpawnLoot=new Dictionary<string,int>(request.Loot,StringComparer.OrdinalIgnoreCase);
            if(request.TrailerLoot!=null&&t.Trailer!=null)t.Trailer.SpawnLoot=new Dictionary<string,int>(request.TrailerLoot,StringComparer.OrdinalIgnoreCase);
            PatrolsModule.Instance.SpawnPoi(t,position,rotation,completed??Ignore);
        }
        /// <summary>Accepts an optional completion callback.</summary>
        private static void Ignore(GameObject vehicle) { }
    }
}
