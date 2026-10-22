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
    internal sealed class PoiDressMarkers
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

        private const string SaveKey = "WildWasteland.Towns.PoiDressMarkers.v1";
        private sealed class Live { internal string Key; internal PlayMakerFSM Fsm; }
        private readonly Dictionary<string, SpawnerState> _saved = new Dictionary<string, SpawnerState>(StringComparer.Ordinal);
        private readonly List<Live> _live = new List<Live>();
        internal readonly PoiSpawnCatalog Catalog = new PoiSpawnCatalog();
        internal void Capture()
        {
            foreach (var row in _live) if (row.Fsm != null)
                _saved[row.Key] = new SpawnerState { OnOff = TownPopulation.Read(row.Fsm, "OnOff"), Number = TownPopulation.Read(row.Fsm, "number"), NumberEnd = TownPopulation.Read(row.Fsm, "numberEnd") };
            _live.RemoveAll(row => row.Fsm == null);
        }
        private static string Identity(PoiDressing.Site site, PoiDressProp marker)
        {
            var world = Origin.World(site.Root.position);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(site.Type)) + ":" + Mathf.RoundToInt(world.x * 10) + ":" + Mathf.RoundToInt(world.z * 10) + ":" + marker.Id;
        }
        internal bool Build(PoiDressing.Site site, PoiDressProp marker)
        {
            if (!marker.Marker) return false;
            var holder = new GameObject("POI " + marker.Kind + " " + marker.Id); holder.SetActive(false); holder.transform.SetParent(site.Dressing.transform, false);
            PoiPropCatalog.Pose(holder.transform, marker);
            if (marker.Kind == "Hide")
            {
                // Vanilla and WW brains find Hide-tagged points, not trigger volumes. Five floor-level samples describe the area.
                foreach (var p in new[] { Vector3.zero, new Vector3(-.35f,0,-.35f), new Vector3(.35f,0,-.35f), new Vector3(-.35f,0,.35f), new Vector3(.35f,0,.35f) })
                { var point = new GameObject("Hide"); point.tag = "Hide"; point.transform.SetParent(holder.transform, false); point.transform.localPosition = p; }
                holder.SetActive(true); return true;
            }
            if (marker.Kind == "Exit") { holder.SetActive(true); return true; }
            string key = Identity(site, marker);
            var profile = Catalog.Find(marker.Kind, marker.Profile);
            if (profile == null) { Object.Destroy(holder); throw new InvalidOperationException("Unavailable marker list: " + marker.Profile); }
            SpawnerState saved; bool existing = _saved.TryGetValue(key, out saved);
            var random = new System.Random(TownPlanner.Hash(key));
            int count = random.Next(100) >= marker.Chance ? 0 : random.Next(marker.Minimum, marker.Maximum + 1);
            if (!existing) { saved = new SpawnerState { NumberEnd = count, OnOff = count == 0 ? 1 : 0 }; _saved.Add(key, saved); }
            count = saved.NumberEnd;
            // Every saved group is one-shot. Changing a layout/list never rerolls already committed progress.
            if (saved.OnOff > 0 || saved.Number >= count) { Object.Destroy(holder); return true; }
            var go = Object.Instantiate(profile.Template.gameObject, holder.transform, false);
            go.name = (marker.Kind == "Monsters" ? "EnemySpawn" : "ItemSpawn") + "_dress_" + marker.Id;
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            foreach (var f in go.GetComponentsInChildren<PlayMakerFSM>(true)) if (f.FsmName != "ItemSpawner") Object.DestroyImmediate(f);
            var fsm = FsmUtil.Find(go, "ItemSpawner");
            if (fsm == null) { Object.Destroy(holder); throw new InvalidOperationException("Marker template has no native ItemSpawner"); }
            var points = new GameObject("Marker spawn points"); points.transform.SetParent(holder.transform, false);
            // Native GetGameObjectSibling(+1) points to this sibling, not to unrelated POI children.
            for (int i = 0; i < count; i++)
            {
                var point = new GameObject("Spawn " + i); point.transform.SetParent(points.transform, false);
                point.transform.localPosition = new Vector3((float)random.NextDouble() - .5f, .03f, (float)random.NextDouble() - .5f);
                RaycastHit floor; var position = point.transform.position;
                if (Physics.Raycast(position + Vector3.up * .4f, Vector3.down, out floor, .8f, TownNavigation.Mask, QueryTriggerInteraction.Ignore) && floor.normal.y >= .65f)
                    point.transform.position = floor.point + Vector3.up * .06f;
            }
            foreach (var state in fsm.FsmStates) foreach (var action in state.Actions)
            {
                var select = action as SelectRandomInt;
                if (select != null && select.storeInt != null && select.storeInt.Name == "index")
                {
                    select.ints = new FsmInt[profile.Indices.Length]; select.weights = new FsmFloat[profile.Weights.Length];
                    for (int i = 0; i < select.ints.Length; i++) { select.ints[i] = new FsmInt(profile.Indices[i]); select.weights[i] = new FsmFloat(profile.Weights[i]); }
                }
            }
            TownPopulation.Write(fsm, "number", saved.Number); TownPopulation.Write(fsm, "numberEnd", count); TownPopulation.Write(fsm, "OnOff", saved.OnOff);
            TownPopulation.Force(fsm, count, marker.Kind == "Monsters" ? site.Population : null);
            // Ignore stale editor-template spawnList/spawn values; the native spawn action resolves its own fresh children.
            var list = fsm.FsmVariables.FindFsmGameObject("spawnList"); if (list != null) list.Value = points;
            _live.Add(new Live { Key = key, Fsm = fsm });
            holder.SetActive(true); InteriorPopulation.OwnMarker(fsm, site);
            Log.Always("[Towns] POI marker " + site.Type + "/" + marker.Kind + ": " + (marker.Kind == "Monsters" && site.Town != null ? "town allegiance " + site.Town.Plan.Faction : profile.Name) + "; " + saved.Number + "/" + count + ", native arrival queue; " + marker.Id);
            return true;
        }
        internal void Save(string slot)
        {
            Capture(); var text = new StringBuilder("WWPM1\n");
            foreach (var row in _saved) text.Append(row.Key).Append('\t').Append(row.Value.OnOff).Append('\t').Append(row.Value.Number).Append('\t').Append(row.Value.NumberEnd).Append('\n');
            string value = text.ToString(); var cache = new ES3Settings(slot) { location = ES3.Location.Cache };
            ES3.Save<string>(SaveKey, value, cache);
            SaveBus.AfterSave(() =>
            {
                try
                {
                    ES3.Save<string>(SaveKey, value, cache); ES3.StoreCachedFile(slot);
                    if (ES3.Load<string>(SaveKey, new ES3Settings(slot) { location = ES3.Location.File }) != value) throw new IOException("Marker progress readback mismatch");
                }
                catch (Exception e) { SaveBus.ReportFailure("POI marker save failed: " + e.Message); }
            });
        }
        internal void Load(string slot)
        {
            _saved.Clear(); _live.Clear();
            if (string.IsNullOrEmpty(slot)) return;
            var settings = new ES3Settings(slot) { location = ES3.Location.File };
            if (!File.Exists(settings.FullPath) || !ES3.KeyExists(SaveKey, settings)) return;
            string[] lines = ES3.Load<string>(SaveKey, settings).Split('\n');
            if (lines.Length == 0 || lines[0] != "WWPM1" || lines.Length > 100000) throw new InvalidDataException("Invalid marker progress header/count");
            foreach (string line in lines)
            {
                if (line.Length == 0 || line == "WWPM1") continue;
                var v = line.Split('\t'); int on, number, end;
                if (v.Length != 4 || !int.TryParse(v[1], out on) || !int.TryParse(v[2], out number) || !int.TryParse(v[3], out end)
                    || on < 0 || on > 1 || number < 0 || end < 0 || end > 32 || number > 32 || _saved.ContainsKey(v[0])) throw new InvalidDataException("Invalid marker progress row");
                _saved.Add(v[0], new SpawnerState { OnOff = on, Number = number, NumberEnd = end });
            }
        }
        internal void Clear(bool reset) { Capture(); _live.Clear(); if (reset) _saved.Clear(); Catalog.Clear(); }
    }
}
