using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Custom town-building registrations captured before workers start using the graph catalog.</summary>
    internal sealed class TownPrefabCatalog:ITownPrefabCatalog
    {
        internal static readonly Dictionary<string,GameObject> Prefabs=new Dictionary<string,GameObject>(StringComparer.Ordinal);
        internal static bool Captured;
        public bool Register(IModule owner,string id,GameObject prefab){if(owner==null||prefab==null||string.IsNullOrEmpty(id)||id!=prefab.name||Captured)return false;Prefabs[id]=prefab;return true;}
        public void Names(List<string> into){into.Clear();foreach(var row in Prefabs)if(row.Value!=null)into.Add(row.Key);into.Sort(StringComparer.Ordinal);}
        internal static void Clear(){Prefabs.Clear();Captured=false;}
    }
}
