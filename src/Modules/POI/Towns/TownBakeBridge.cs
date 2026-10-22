using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Typed navigation queries used during town construction.</summary>
    internal static class TownBakeBridge
    {
        /// <summary>Returns i town navigation for the current owner state.</summary>
        internal static bool Enabled=>ModApi.Get<ITownNavigation>()!=null;
        /// <summary>Queries actual NPC navigation tuning with an explicit provider-off fallback.</summary>
        internal static float Tuning(string name,float fallback)=>ModApi.Get<ITownNavigation>()?.Tuning(name,fallback)??fallback;
        /// <summary>Advances or requests world discovery through the owning runtime.</summary>
        internal static void Discover(GameObject root){if(root!=null)ModApi.Get<INavApi>()?.Discover(root.transform);}
        /// <summary>Registers a freshly spawned actor with the NPC behavior owner.</summary>
        internal static void Actor(GameObject actor)=>ModApi.Get<ITownNavigation>()?.Actor(actor);
        /// <summary>Queries the navigation root containing a scene point.</summary>
        internal static Transform Root(Vector3 point)=>ModApi.Get<ITownNavigation>()?.Root(point);
        /// <summary>Copies bounded floor candidates from the native navigation provider.</summary>
        internal static int Cells(Transform root,bool roofed,List<Vector3> into,out bool known,out bool baked){var api=ModApi.Get<ITownNavigation>();if(api!=null)return api.Cells(root,roofed,into,out known,out baked);into.Clear();known=baked=false;return 0;}
    }
}
