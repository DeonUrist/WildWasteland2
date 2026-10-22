using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Optional crew presentation; inactive Presentation leaves native actors usable.</summary>
    internal static class CrewLooks
    {
        /// <summary>Resolves the Presentation alias or preserves an ordinary native prefab name.</summary>
        internal static string BasePrefab(string name) { var api=ModApi.Get<ICrewAppearance>();return api!=null?api.BasePrefab(name):name; }
        /// <summary>Reads appearance identity when available; native fallback remains authoritative otherwise.</summary>
        internal static string Identity(GameObject actor,string fallback) { var api=ModApi.Get<ICrewAppearance>();return api!=null?api.Identity(actor,fallback):fallback; }
        /// <summary>Requests optional dressing after the native actor exists.</summary>
        internal static void Dress(GameObject actor,string identity,bool randomize=true) { var api=ModApi.Get<ICrewAppearance>();if(api!=null)api.Dress(actor,identity,randomize); }
        /// <summary>Transfers optional appearance to a native replacement actor.</summary>
        internal static void Transfer(GameObject source,GameObject destination) { var api=ModApi.Get<ICrewAppearance>();if(api!=null)api.Transfer(source,destination); }
    }
}
