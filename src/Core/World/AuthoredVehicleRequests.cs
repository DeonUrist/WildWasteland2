using System;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Requests authored vehicle definitions without exposing Patrols' mutable template model.</summary>
    public interface IAuthoredVehicleRequests
    {
        /// <summary>Parses and prepares a current authored definition; false for invalid or unavailable definitions.</summary>
        bool Prepare(string definition);
        /// <summary>Queues a complete vehicle at a scene pose and invokes completion with the result or null.</summary>
        void Spawn(string definition,Vector3 scenePosition,Quaternion rotation,Action<GameObject> completed);
    }
}
