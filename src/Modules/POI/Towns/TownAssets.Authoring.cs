using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    internal sealed partial class TownAssets
    {
        internal void AddAuthored()
        {
            var prefabs = new List<GameObject>(); CustomPoiAssets.Current?.Prefabs(prefabs);
            foreach (var asset in Buildings.Values) if (asset.Prefab != null && !prefabs.Contains(asset.Prefab)) prefabs.Add(asset.Prefab);
            foreach (var prefab in prefabs)
            {
                var spec = PoiDressing.PlanningSpec(prefab) ?? Measure(prefab); if (spec == null) continue;
                spec.Bake = TownBakeData.Load(spec, Mathf.Max(1, TownBakeBridge.Tuning("NavMargin", 4)), Mathf.Max(.25f, TownBakeBridge.Tuning("NavCellSize", .5f)));
                if (spec.Bake == null || !spec.Bake.FindEntry(spec)) { Log.Warn(null, "[POI] custom POI needs a current navigation bake before town placement: " + prefab.name); continue; }
                spec.Frontages = new[] { TownFrontage.Make(spec, 0), TownFrontage.Make(spec, 1) };
                spec.TiltedFrontages = new[] { TownFrontage.Make(spec, 0, -9), TownFrontage.Make(spec, 0, 9), TownFrontage.Make(spec, 1, -9), TownFrontage.Make(spec, 1, 9) };
                BuildingAsset old;
                if (Buildings.TryGetValue(prefab.name, out old)) { spec.DefaultAllowed = old.Spec.DefaultAllowed; spec.Wreck = old.Spec.Wreck; spec.Crane = old.Spec.Crane; spec.Merchant = old.Spec.Merchant; spec.Camp = old.Spec.Camp; Buildings[prefab.name] = new BuildingAsset { Prefab = prefab, Spec = spec, Spawners = old.Spawners, Markers = old.Markers, Budgets = old.Budgets, Strip = old.Strip }; }
                else Buildings[prefab.name] = new BuildingAsset { Prefab = prefab, Spec = spec, Spawners = new string[0], Markers = new string[0], Budgets = new TownPopulation.NativeBudget[0] };
            }
            var names = new List<string>(Buildings.Keys); names.Sort(StringComparer.Ordinal);
            var specs = new BuildingSpec[names.Count]; for (int i = 0; i < names.Count; i++) specs[i] = Buildings[names[i]].Spec; Specs = specs;
        }
    }
    internal sealed partial class TownRuntime
    {
        internal int AuthoringSpawnChance { get { return ((PoiModule)Owner).AuthoringSpawnChance; } }
        internal void AuthoredChanged() { foreach (var registration in _graphs.Values) registration.Assets.AddAuthored(); _revision++; }
        internal GameObject NativeTemplate(string type)
        {
            foreach (var registration in _graphs.Values)
            {
                BuildingAsset asset; if (registration.Assets.Buildings.TryGetValue(type, out asset)) return asset.Prefab;
                foreach (var prefab in registration.Assets.NativePoiPrefabs) if (prefab != null && prefab.name == type) return prefab;
            }
            return null;
        }
        internal void AuthoringTypes(List<string> into)
        {
            into.Clear(); var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var registration in _graphs.Values)
            {
                foreach (var prefab in registration.Assets.NativePoiPrefabs) if (prefab != null) names.Add(prefab.name);
                foreach (string name in registration.Assets.Buildings.Keys) names.Add(name);
            }
            var roots = new List<GameObject>(); PrefabCatalog.AllRoots(roots);
            foreach (var root in roots) if (root != null && (root.name.StartsWith("Building_", StringComparison.Ordinal) || root.name.StartsWith("Camp_", StringComparison.Ordinal) || root.name.StartsWith("Cave_", StringComparison.Ordinal) || root.name.StartsWith("Wreck_", StringComparison.Ordinal))) names.Add(root.name);
            foreach (string name in TownPrefabCatalog.Prefabs.Keys) names.Add(name);
            var custom = new List<string>(); _dressing.Authored.Names(custom); foreach (string name in custom) names.Add(name);
            names.Add("TownsGasStation"); into.AddRange(names); into.Sort(StringComparer.Ordinal);
        }
    }
}
