using System;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Per-spawn overrides on an authored current definition; null cargo retains its ordinary authored roll.</summary>
    public sealed class AuthoredVehicleSpawn
    {
        public string Definition;
        public bool SetupOnly;
        public System.Collections.Generic.Dictionary<string,int> Loot,TrailerLoot;
    }
    /// <summary>Requests authored vehicle definitions without exposing Patrols' mutable template model.</summary>
    public interface IAuthoredVehicleRequests
    {
        /// <summary>Parses and prepares a current authored definition; false for invalid or unavailable definitions.</summary>
        bool Prepare(string definition);
        /// <summary>Queues a complete vehicle at a scene pose and invokes completion with the result or null.</summary>
        void Spawn(string definition,Vector3 scenePosition,Quaternion rotation,Action<GameObject> completed);
        /// <summary>Queues a complete authored instance with detached encounter-specific overrides.</summary>
        void Spawn(AuthoredVehicleSpawn request,Vector3 scenePosition,Quaternion rotation,Action<GameObject> completed);
    }
}
