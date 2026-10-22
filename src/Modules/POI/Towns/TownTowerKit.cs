using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    // Only static tower geometry and its native player ladder; never clone a camp root.
    /// <summary>
    /// Small tower prefab extracted from Camp_9 (geometry + native ladder). Wave A (A6): POI part of Towns/TownTowerKit.cs;
    /// the isolated nav bake (Step/ReleaseBake/Ready/Progress) is in NPCAI/Navigation/_fromTowns/TownTowerKit.Bake.cs.
    /// </summary>
    internal sealed partial class TownTowerKit : IDisposable
    {
        internal static readonly string[] Parts = { "container_3", "tent_1_support", "tent_1", "decor/fence_small_4",
            "decor/fence_small_4 (1)", "decor/fence_small_3", "decor/ladder_L" };
        internal GameObject Prefab;
        internal bool Available { get { return Prefab != null; } }
        internal void Load(List<GameObject> prefabs)
        {
            foreach (var camp in prefabs) if (camp.name == "Camp_9")
            {
                Prefab = new GameObject("TownsTower"); Prefab.SetActive(false); Prefab.tag = "SpawnerStatic";
                foreach (string path in Parts)
                {
                    var source = camp.transform.Find(path);
                    if (source == null) throw new InvalidOperationException("Small tower part missing: " + path);
                    // Each whitelisted subtree contains meshes, solid colliders and the ladder trigger only.
                    var part = UnityEngine.Object.Instantiate(source.gameObject, Prefab.transform, false);
                    part.name = source.name; part.transform.localPosition = camp.transform.InverseTransformPoint(source.position);
                    part.transform.localRotation = Quaternion.Inverse(camp.transform.rotation) * source.rotation;
                    part.SetActive(true);
                }
                if (Prefab.GetComponentsInChildren<PlayMakerFSM>(true).Length != 0 || Prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                    throw new InvalidOperationException("Tower geometry unexpectedly contains camp logic or physics actors");
                Log.Main(null, "[POI] small tower extracted: container, canopy/supports, three railings and native ladder; no camp spawners, loot, signs or furnishings.");
                return;
            }
        }
        public void Dispose() { ReleaseBake(); if (Prefab != null) UnityEngine.Object.Destroy(Prefab); Prefab = null; }
    }
}
