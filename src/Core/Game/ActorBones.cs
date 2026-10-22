using System;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Native bone-name lookup shared by visual and actor adapters.</summary>
    internal static class ActorBones
    {
        /// <summary>Finds the same bone suffix in hierarchy order.</summary>
        internal static Transform Find(Transform root,string bone)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true)){var name=t.name;int i=name.LastIndexOf(':');if(i<0)i=name.LastIndexOf('_');if((i>=0?name.Substring(i+1):name)==bone)return t;}return null;
        }
    }
}
