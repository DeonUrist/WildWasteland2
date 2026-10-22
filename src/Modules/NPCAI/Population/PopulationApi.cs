using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.NPC;

namespace WildWasteland.Modules.POI
{
    /// <summary>
    /// IPopulation provider (README 6.2 population rules): interior roots registered by other modules join the interior population
    /// (arrival-gated native spawns, interior hearing), spawner catalogs feed the native-POI variety pools, and live counts / populated
    /// state answer from NPCAI's registries. Provided by NPCAIModule.ProvideApis.
    /// </summary>
    internal sealed class PopulationApi : IPopulation
    {
        /// <summary>Registers an interior root; its EnemySpawn groups spawn on arrival (deafOutside is the standing interior hearing rule).</summary>
        public void RegisterInterior(IModule owner, Transform root, string locationKey, bool deafOutside) { InteriorPopulation.RegisterRoot(root); }
        /// <summary>Removes an interior registration.</summary>
        public void UnregisterInterior(Transform root) { InteriorPopulation.UnregisterRoot(root); }
        /// <summary>Catalogs the named creature prefabs (Core PrefabCatalog, exact then contains) into the native-POI pools by their tag group.</summary>
        public void RegisterCatalog(IModule owner, string spawnerKey, IList<string> npcPrefabs)
        {
            if (npcPrefabs == null) return;
            for (int i = 0; i < npcPrefabs.Count; i++)
            {
                var prefab = PrefabCatalog.Exact(npcPrefabs[i]) ?? PrefabCatalog.Find(npcPrefabs[i]);
                if (prefab != null) NativePoiPopulation.CatalogTagged(prefab);
            }
        }
        /// <summary>Live registered NPCs of a faction tag (null = all) within a radius of a scene point.</summary>
        public int CountNear(Vector3 scene, float radius, string faction) { return Senses.CountNear(scene, radius, faction); }
        /// <summary>True once every interior group of the root has spawned.</summary>
        public bool IsPopulated(Transform root) { return InteriorPopulation.IsPopulated(root); }
    }
}
