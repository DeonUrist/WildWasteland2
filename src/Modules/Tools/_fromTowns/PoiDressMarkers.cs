using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// POI editor marker palette: marker kinds and scenery categories for the dresser catalog.
    /// Wave A (A6): Tools part of Towns/PoiDressMarkers.cs; per-world marker spawn groups and progress in Loot/_fromTowns/PoiDressMarkers.Spawns.cs.
    /// </summary>
    internal sealed partial class PoiDressMarkers
    {
        internal static readonly string[] Kinds = { "Hide", "Exit", "Items", "CarParts", "Monsters" };
        internal static readonly string[] Categories = { "Markers", "Structures & walls", "Furniture & storage", "Bones & trophies", "Vegetation & rocks", "Vehicle bodies", "Car parts", "Weapons & equipment", "Items & supplies", "Other scenery" };
        internal static bool IsKind(string kind) { return Array.IndexOf(Kinds, kind) >= 0; }
        internal static string Category(GameObject root, string label)
        {
            string name = label.ToLowerInvariant();
            if (PoiSpawnCatalog.Contains(name, "skull", "bone", "spike", "corpse", "carcass", "skeleton")) return "Bones & trophies";
            if (PoiSpawnCatalog.Contains(name, "toilet", "chair", "table", "bed", "shelf", "cabinet", "barrel", "crate", "container", "locker", "sink")) return "Furniture & storage";
            if (PoiSpawnCatalog.Contains(name, "tree", "bush", "grass", "rock", "stone", "cactus", "stump")) return "Vegetation & rocks";
            if (PoiSpawnCatalog.Contains(name, "bumper", "radiator", "exhaust", "engine", "wheel", "gearlever", "steering", "hood_")) return "Car parts";
            if (PoiSpawnCatalog.Contains(name, "fence", "wall", "building", "stairs", "floor", "roof", "ladder", "tower")) return "Structures & walls";
            if (root.GetComponent<NWH.VehiclePhysics2.VehicleController>() != null || PoiSpawnCatalog.Contains(name, "car wreck", "car_wreck")) return "Vehicle bodies";
            string category = PoiSpawnCatalog.ItemCategory(root);
            if (category != "Items & supplies") return category;
            return root.GetComponent<ES3Internal.ES3Prefab>() != null ? category : "Other scenery";
        }
    }
}
