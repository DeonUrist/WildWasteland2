using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Den.Tools;
using MapMagic.Nodes;
using MapMagic.Nodes.ObjectsGenerators;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;
using WildWasteland.Core;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.POI
{
    internal sealed partial class CustomPoiAssets
    {
        internal sealed class SpawnRequest
        {
            internal string Type, Key;
            internal TownGasStation.Request Ground;
            internal bool Existing;
        }
        private readonly Dictionary<string, bool> _spawnPolicy = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<TileData, HashSet<GameObject>> _disabledNative = new ConcurrentDictionary<TileData, HashSet<GameObject>>();
        private static string PolicyPath { get { return Path.Combine(Assets.ModuleDir("POI"), "spawns.json"); } }
        internal bool Spawns(string type) { bool value; return _spawnPolicy.TryGetValue(type, out value) ? value : !Contains(type); }
        internal void SetSpawns(string type, bool enabled)
        {
            var data = new Dictionary<string, object>(); foreach (var row in _spawnPolicy) data[row.Key] = row.Value; data[type] = enabled;
            Directory.CreateDirectory(Path.GetDirectoryName(PolicyPath)); File.WriteAllText(PolicyPath + ".tmp", Json.Write(data)); AtomicFile.Replace(PolicyPath + ".tmp", PolicyPath, PolicyPath + ".bak"); _spawnPolicy[type] = enabled;
            Log.Main(null, "[POI] " + type + " random spawns " + (enabled ? "enabled" : "disabled; reserved for towns"));
        }
        private void LoadSpawnPolicy()
        {
            if (!File.Exists(PolicyPath)) return;
            try { foreach (var row in Json.ParseObject(File.ReadAllText(PolicyPath))) { if (!(row.Value is bool)) throw new FormatException("Spawns value must be boolean"); _spawnPolicy[row.Key] = (bool)row.Value; } }
            catch (Exception e) { Log.Warn(null, "[POI] spawn policy rejected: " + e.Message); }
        }
        internal List<SpawnRequest> CaptureSpawns(TerrainTile tile, TileData data, string chunk, bool startingChunk)
        {
            var disabled = new HashSet<GameObject>(); var roots = new List<GameObject>(); PrefabCatalog.AllRoots(roots);
            foreach (var root in roots) if (root != null && !Spawns(root.name)) disabled.Add(root);
            _disabledNative[data] = disabled;
            var result = new List<SpawnRequest>(); if (data.isDraft) return result;
            foreach (var d in _definitions.Values)
            {
                string key; using (var hash = MD5.Create()) key = new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes(chunk + "/authored/" + d.Id))).ToString("N");
                Placement saved = _placements.Find(p => p.Key == key);
                float radius = Mathf.Sqrt(d.Width * d.Width + d.Depth * d.Depth) * .5f + 4;
                if (saved != null)
                {
                    var local = tile.mapMagic.transform.InverseTransformPoint(Origin.Scene(saved.World));
                    result.Add(new SpawnRequest { Type = d.Id, Key = key, Existing = true, Ground = new TownGasStation.Request { Key = chunk + "/" + d.Id, Radius = radius, Yaw = saved.Yaw, Pad = new TownPlan { WorldCenter = local, Radius = radius, Feather = 10 } } });
                }
                else if (!startingChunk && Spawns(d.Id) && ModApi.Get<INavApi>()?.WorldGenerationSuspended != true && TownPlanner.Hash(chunk + "/authored/chance/" + d.Id) % 100 < _owner.AuthoringSpawnChance)
                    result.Add(new SpawnRequest { Type = d.Id, Key = key, Ground = new TownGasStation.Request { Key = chunk + "/" + d.Id, Radius = radius } });
            }
            return result;
        }
        private void FilterNativeSpawns(Type type, TileData data, IApplyData apply, StopToken stop)
        {
            if (type != typeof(ObjectsOutput)) return;
            HashSet<GameObject> disabled; if (!_disabledNative.TryGetValue(data, out disabled) || disabled.Count == 0) return;
            var objects = apply as ObjectsOutput.ApplyObjectsData; if (objects == null) return;
            for (int i = 0; i < objects.prototypes.Length; i++) if (disabled.Contains(objects.prototypes[i].prefab)) objects.transitions[i] = new List<Transition>();
        }
        internal static void PlanSpawns(List<SpawnRequest> requests, TileData data, List<Vector3> avoid, TownAssets assets, TownPlan town, TownWorldRoads roads, StopToken stop)
        {
            if (requests == null) return;
            foreach (var request in requests)
            {
                if (stop != null && stop.stop) return;
                if (!request.Existing) TownGasStation.PlanDesert(request.Ground, data, avoid, town, roads, stop);
                if (request.Ground.Pad == null) continue;
                TownLateTerrain.Apply(data, request.Ground.Pad, assets, stop); avoid.Add(request.Ground.Pad.WorldCenter);
            }
        }
        internal void ApplySpawns(List<SpawnRequest> requests, TerrainTile tile)
        {
            if (requests == null) return;
            foreach (var request in requests)
            {
                if (request.Ground.Pad == null) continue; Definition d; if (!_definitions.TryGetValue(request.Type, out d)) continue;
                Placement p = _placements.Find(row => row.Key == request.Key);
                if (p == null) { p = new Placement { Key = request.Key, Type = d.Id, Saved = true, Yaw = request.Ground.Yaw, World = Origin.World(tile.mapMagic.transform.TransformPoint(request.Ground.Pad.WorldCenter)) }; _placements.Add(p); }
                if (p.Root != null) continue;
                Materialize(p, d, tile.main.terrain);
                Log.Main(null, "[POI] random custom POI " + d.Id + " placed: " + p.Key);
            }
        }
        internal void ForgetCapture(TileData data) { HashSet<GameObject> ignored; if (data != null) _disabledNative.TryRemove(data, out ignored); }
    }
}
