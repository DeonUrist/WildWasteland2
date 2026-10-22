using System.Collections.Generic;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Explicit authored static POI prefabs eligible for future town lots.</summary>
    public interface ITownPrefabCatalog
    {
        /// <summary>Registers an inactive prefab before the first terrain catalog is captured; id must equal its stable prefab name.</summary>
        bool Register(IModule owner,string id,GameObject prefab);
        /// <summary>Copies available custom prefab names for template authoring.</summary>
        void Names(List<string> into);
    }
}
