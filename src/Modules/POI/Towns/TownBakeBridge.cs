using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Typed navigation queries used during town construction.</summary>
    internal static class TownBakeBridge
    {
        internal static bool Enabled=>ModApi.Get<ITownNavigation>()!=null;
        internal static float Tuning(string name,float fallback)=>ModApi.Get<ITownNavigation>()?.Tuning(name,fallback)??fallback;
        internal static void Discover(GameObject root){if(root!=null)ModApi.Get<INavApi>()?.Discover(root.transform);}
        internal static void Actor(GameObject actor)=>ModApi.Get<ITownNavigation>()?.Actor(actor);
        internal static Transform Root(Vector3 point)=>ModApi.Get<ITownNavigation>()?.Root(point);
        internal static int Cells(Transform root,bool roofed,List<Vector3> into,out bool known,out bool baked){var api=ModApi.Get<ITownNavigation>();if(api!=null)return api.Cells(root,roofed,into,out known,out baked);into.Clear();known=baked=false;return 0;}
    }
}
