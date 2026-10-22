using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    /// <summary>
    /// Stages, activates and removes town lot prefabs (camp wall strip, boss/lab doors off, gas-station parking shell).
    /// Wave A (A6): POI part of Towns/TownFactory.cs; spawner progress in Loot/_fromTowns/TownFactory.Spawners.cs.
    /// </summary>
    internal static partial class TownFactory
    {
        // A lot's prefab clone is deserialized one build slot ahead (Stage) and activated in the next (Building): the two
        // indivisible native costs land in different frames.
        /// <summary>Tests whether a native lot needs an inactive clone before activation.</summary>
        internal static bool NeedsStage(TownSite site, int index)
        { var lot = site.Plan.Lots[index]; return !lot.Skipped && !lot.IsParking && site.Buildings[index] == null; }
        /// <summary>Clones the native lot under an inactive staging root before a later activation frame.</summary>
        internal static GameObject Stage(TownSite site, int index, TownAssets assets, Transform staging)
        {
            var lot = site.Plan.Lots[index]; BuildingAsset asset;
            if (!NeedsStage(site, index)) return null;
            if (!assets.Buildings.TryGetValue(lot.Prefab, out asset)) throw new InvalidOperationException("Saved town prefab unavailable: " + lot.Prefab);
            return UnityEngine.Object.Instantiate(asset.Prefab, staging, false);
        }
        /// <summary>Activates one committed lot with its saved spawn progress and town ownership.</summary>
        internal static void Building(TownSite site, int index, TownAssets assets, TownSettings settings, TownRecord record, Transform staging, Transform parent, GameObject staged)
        {
            var lot = site.Plan.Lots[index]; BuildingAsset asset;
            if (lot.Skipped) { site.Spawners[index] = new PlayMakerFSM[0]; if (staged != null) UnityEngine.Object.Destroy(staged); return; }
            if (lot.IsParking)
            {
                // Keep the open parking apron; the station stands beside its north edge, outside the walls.
                site.Spawners[index] = new PlayMakerFSM[0]; if (staged != null) UnityEngine.Object.Destroy(staged);
                if (site.Buildings[index] != null) { site.Buildings[index].transform.SetParent(parent, true); site.Buildings[index].SetActive(true); return; }
                Log.Main(null,"Town ground parking slot " + site.Plan.Key + "/" + index + "; companion POIs are independent persisted lots.");
                return;
            }
            if (site.Buildings[index] != null)
            { if (staged != null) UnityEngine.Object.Destroy(staged); site.Buildings[index].transform.SetParent(parent, true); site.Buildings[index].SetActive(true); TownPopulation.Configure(site, index, assets, settings); InteriorPopulation.OwnTown(site, index); TownBakeBridge.Discover(site.Buildings[index]); return; }
            if (!assets.Buildings.TryGetValue(lot.Prefab, out asset)) throw new InvalidOperationException("Saved town prefab unavailable: " + lot.Prefab);
            var go = staged != null ? staged : UnityEngine.Object.Instantiate(asset.Prefab, staging, false);
            site.Buildings[index] = go;
            foreach (string path in asset.Strip) { var wall = go.transform.Find(path); if (wall != null) wall.gameObject.SetActive(false); }   // (WW) a camp without its perimeter
            TownMarkers.Building(go.transform);
            // Boss trap FSMs must never close doors or alter global boss progression in towns.
            foreach (var fsm in go.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (fsm.FsmName == "BossDoor") fsm.enabled = false;
                // Town copies keep the laboratory door as solid scenery. Retain the exit for saves already inside.
                if (fsm.FsmName != "BasementDoor" || fsm.transform.parent == null || fsm.transform.parent.name != "to_basement") continue;
                fsm.enabled = false;
                var label = FsmUtil.Find(fsm.gameObject, "ItemName"); if (label != null) label.enabled = false;
                Log.Main(null, "[POI] laboratory entrance disabled on town copy " + lot.Prefab);
            }
            foreach (string path in asset.Markers)
            { var icon = go.transform.Find(path); var renderer = icon == null ? null : icon.GetComponent<Renderer>(); if (renderer != null) renderer.forceRenderingOff = true; }
            go.name = lot.Prefab + " (Towns " + TownPlanner.Hash(site.Plan.Key) + "/" + index + ")";
            go.transform.position = site.Scene(lot.LocalPosition);
            go.transform.rotation = site.Anchor.rotation * Quaternion.Euler(0, site.Plan.Yaw + lot.Yaw, 0);
            go.SetActive(true); // Still under the inactive staging root: no spawn callback yet.
            var spawners = new PlayMakerFSM[asset.Spawners.Length];
            site.Spawners[index] = spawners;
            for (int i = 0; i < spawners.Length; i++)
            {
                var node = asset.Spawners[i].Length == 0 ? go.transform : go.transform.Find(asset.Spawners[i]);
                if (node == null) throw new InvalidOperationException("Missing cached town spawner path " + asset.Spawners[i]);
                spawners[i] = FsmUtil.Find(node.gameObject, "ItemSpawner");
                if (spawners[i] != null && asset.Budgets[i].Kind == 6)
                { Set(spawners[i], "OnOff", 1); spawners[i].enabled = false; }
            }
            go.transform.SetParent(parent, true);
            // Native test spawners start in wait (one scaled second before their trigger). Restore before
            // their first Update. Keep their components/names and native spawned-item save lists intact.
            SpawnerState[] states;
            if (record.Spawners.TryGetValue(index, out states))
                foreach (var state in states)
                    for (int i = 0; i < spawners.Length; i++) if (asset.Spawners[i] == state.Path && spawners[i] != null)
                    {
                        Set(spawners[i], "OnOff", state.OnOff); Set(spawners[i], "number", state.Number); Set(spawners[i], "numberEnd", state.NumberEnd);
                        if (state.OnOff > 0 || (state.NumberEnd > 0 && state.Number >= state.NumberEnd))
                        { Set(spawners[i], "OnOff", 1); spawners[i].Fsm.SetState("spawned"); }
                        // A partially completed native synchronous spawn loop resumes only after isPlay.
                    }
            TownPopulation.Configure(site, index, assets, settings);
            InteriorPopulation.OwnTown(site, index);
            TownBakeBridge.Discover(go);
            Log.Main(null, "[POI] lot " + site.Plan.Key + "/" + index + ": " + lot.Prefab + ", role=" + lot.Role + ", town=" + site.Plan.Condition);
        }

        /// <summary>Creates the planned static street-side dressing without extra road surfaces.</summary>
        internal static GameObject Streets(TownSite site, TownAssets assets, Transform parent)
        {
            var root = new GameObject("Towns parking and wreck pockets " + site.Plan.Key); root.transform.SetParent(parent, false);
            TownDebris.Build(site, assets, root.transform);
            return root;
        }
        /// <summary>Retains usable scenery while disabling unsafe or failed native spawn groups.</summary>
        internal static void KeepGeometry(TownSite site, int index, Transform parent)
        {
            var go = site.Buildings[index]; if (go == null) { site.Plan.Lots[index].Skipped = true; return; }
            foreach (var f in go.GetComponentsInChildren<PlayMakerFSM>(true)) if (f.FsmName == "ItemSpawner") { Set(f, "OnOff", 1); f.enabled = false; }
            go.transform.SetParent(parent, true); go.SetActive(true); TownBakeBridge.Discover(go);
        }
        /// <summary>Releases the site population/navigation contexts before destroying its owned geometry.</summary>
        internal static void Destroy(TownSite site)
        {
            if(site.NavContext!=null)ModApi.Get<ITownNavigation>()?.Release(site.NavContext);
            if(site.PopulationArea!=null)ModApi.Get<INativePopulation>()?.Forget(site.PopulationArea);
            if (site.Staged != null) { UnityEngine.Object.Destroy(site.Staged); site.Staged = null; }
            if (site.Walls != null) site.Walls.Dispose();
            if (site.Overlap != null) site.Overlap.Dispose();
            TownMarkers.Remove(site);
            foreach (var building in site.Buildings) if (building != null) { ModApi.Get<ITownNavigation>()?.Forget(building.transform); UnityEngine.Object.Destroy(building); }
            if (site.Streets != null)
                UnityEngine.Object.Destroy(site.Streets);
        }
    }
}
