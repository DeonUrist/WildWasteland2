using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ES3Internal;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Towns
{
    // The supplied gas station (GLB render shell + separate collider) in three roles:
    //  * a DESERT POI: [Towns / World] chance per generated 5 km block, on a flat pad beside a native road, facing it, with
    //    4 native loot spawners inside the shop and 2-3 native Scrapyard OR animal spawners (spawner progress is saved with the world);
    //  * a town PARKING LOT (TownFactory): the shell only, beside the separate open parking apron outside the walls;
    //  * the developer TEST pad near the starting camp (unsaved, three melee raiders after its bake).
    // All instances are named "TownsGasStation ..." so the one NPC bake, POI navigation and dressing layout apply to every copy.
    internal sealed class TownGasStation : IDisposable
    {
        internal sealed class Request
        {
            internal string Key;
            internal bool Desert;
            internal Vector3 Camp;
            internal float Scale, Radius, Yaw;
            internal GameObject Prefab; internal Vector3 Offset;   // a native camp test pad (Camp_10) instead of the station
            internal TownPlan Pad;
            internal string Failure;
        }
        private sealed class Station
        {
            internal string Key; internal bool Desert;
            internal GameObject Root; internal TerrainTile Tile; internal Request Request;
            internal readonly List<PlayMakerFSM> Spawners = new List<PlayMakerFSM>();
            internal int MapId, CompassId; internal string Label;
            internal Vector3 Position() { return Root == null ? Vector3.zero : Root.transform.position; }
        }
        private const string MarkerOwner = "WildWasteland.Towns.GasStation";
        internal const string SaveKey = "WildWasteland.Towns.GasStations.v1";
        internal static TownGasStation Current;
        // Station-local layout (x = -glTF x, after the slab top is moved to y 0). The shop is x 3..11, z -11..13 (door at x 3.5, z -8);
        // the canopy lane x -4..2 runs the whole length, open at -z (the pumps stand at x -6, the bags/fence close +z beyond 11).
        internal static readonly Vector3 LaneCenter = new Vector3(-1f, 0f, -1.25f);
        internal const float LaneSpread = 5.75f;   // resident bus at +4.5 (deep), visitors at -7 (by the open end)
        private static readonly Vector3[] LootPoints = { new Vector3(7f, .05f, 6.5f), new Vector3(7f, .05f, -3f), new Vector3(8.5f, .05f, 11f), new Vector3(6.5f, .05f, -6.5f) };
        private const bool SpawnEnemies = false;   // desert stations: loot only until their enemy spawners are fixed
        private static readonly Vector3[] EnemyPoints = { new Vector3(-9f, .05f, -10f), new Vector3(-9f, .05f, 11f), new Vector3(6f, .05f, 3f) };
        private readonly TownSettings _settings;
        private readonly List<Station> _stations = new List<Station>();
        private readonly Dictionary<string, SpawnerState[]> _saved = new Dictionary<string, SpawnerState[]>(StringComparer.Ordinal);
        // developer test pad
        private readonly List<Vector3> _cells = new List<Vector3>(256);
        private readonly List<Vector3> _spawnPoints = new List<Vector3>(3);
        private readonly List<GameObject> _actors = new List<GameObject>(3);
        private Station _test;
        private int _spawned; private bool _pointsReady;
        private GasStation.GlbModel _model, _collision;
        private Material[] _materials;
        private GameObject _raider;
        private Transform _camp;
        private IMapMarkers _map;
        private ICompassMarkers _compass;
        private bool _failed;
        private string _status = "Off (reload after enabling)";
        internal Transform Root { get { return _test != null && _test.Root != null ? _test.Root.transform : null; } }
        internal TownGasStation(TownSettings settings) { _settings = settings; Current = this; }
        internal bool Available { get { if (_model == null && !_failed) { try { LoadModel(); } catch (Exception e) { Fail("model setup", e); } } return _model != null && _materials != null; } }

        // ------------------------------------------------------------------ requests (main thread, before the tile is generated)
        internal List<Request> Capture(TerrainTile tile, TileData data, string key, bool startingChunk, TownAssets assets)
        {
            if (data.isDraft || _failed) return null;
            List<Request> list = null;
            try
            {
                int chance = _settings.DesertGasStationChance.Value;
                if (chance > 0 && !Services.PoiNavSetup && !startingChunk && TownPlanner.Hash(key + "/gasstation") % 100 < chance && Available)
                    (list = new List<Request>()).Add(new Request { Key = key, Desert = true, Scale = 1f, Radius = Radius(1f) });
                if (_settings.TestCamp10.Value && assets != null)
                {
                    Vector3 camp10;
                    GameObject prefab = null; foreach (var p in assets.NativePoiPrefabs) if (p != null && p.name == "Camp_10") { prefab = p; break; }
                    Bounds box;
                    if (prefab != null && CampInArea(tile, data, out camp10) && TownAssets.ColliderBox(prefab.transform, out box))
                    {
                        float r = 0; for (int c = 0; c < 4; c++) r = Mathf.Max(r, new Vector2((c & 1) == 0 ? box.min.x : box.max.x, (c & 2) == 0 ? box.min.z : box.max.z).magnitude);
                        if (list == null) list = new List<Request>();
                        list.Add(new Request { Key = "camp10", Camp = camp10, Scale = 1f, Radius = r + 3, Prefab = prefab, Offset = Vector3.zero });
                    }
                }
                if (_settings.TestGasStation.Value)
                {
                    if (_camp == null)
                        foreach (var fsm in PlayMakerFSM.FsmList)
                            if (fsm != null && fsm.FsmName == "StarterAreaDistance" && fsm.gameObject.scene.IsValid())
                            { _camp = fsm.transform; break; }
                    if (_camp == null) { _status = "Waiting for the starting camp; reload if already playing"; return list; }
                    var camp = tile.mapMagic.transform.InverseTransformPoint(_camp.position);
                    var area = data.area.active;
                    if (camp.x < area.worldPos.x || camp.z < area.worldPos.z
                        || camp.x >= area.worldPos.x + area.worldSize.x || camp.z >= area.worldPos.z + area.worldSize.z) return list;
                    if (!Available) return list;
                    float scale = _settings.GasStationScale.Value;
                    _status = "Preparing test pad near camp";
                    if (list == null) list = new List<Request>();
                    list.Add(new Request { Key = "test", Camp = camp, Scale = scale, Radius = Radius(scale) });
                }
            }
            catch (Exception e) { Fail("model setup", e); }
            return list;
        }
        private bool CampInArea(TerrainTile tile, TileData data, out Vector3 camp)
        {
            camp = Vector3.zero;
            if (_camp == null)
                foreach (var fsm in PlayMakerFSM.FsmList)
                    if (fsm != null && fsm.FsmName == "StarterAreaDistance" && fsm.gameObject.scene.IsValid())
                    { _camp = fsm.transform; break; }
            if (_camp == null) return false;
            camp = tile.mapMagic.transform.InverseTransformPoint(_camp.position);
            var area = data.area.active;
            return camp.x >= area.worldPos.x && camp.z >= area.worldPos.z && camp.x < area.worldPos.x + area.worldSize.x && camp.z < area.worldPos.z + area.worldSize.z;
        }
        private float Radius(float scale)
        {
            var bounds = _model.Bounds;
            float x = Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x));
            float z = Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z));
            return Mathf.Sqrt(x * x + z * z) * scale + 2;
        }

        // Worker: only numeric snapshots and the existing terrain-apply buffers, never scene queries.
        internal static void Plan(List<Request> requests, TileData data, List<Vector3> avoid, TownAssets assets, TownPlan saved, TownWorldRoads roads, StopToken stop)
        {
            if (requests == null) return;
            foreach (var request in requests)
            {
                if (stop != null && stop.stop) return;
                if (request.Desert) PlanDesert(request, data, avoid, saved, roads, stop); else PlanTest(request, data, avoid, saved, stop);
                if (request.Pad == null) continue;
                TownLateTerrain.Apply(data, request.Pad, assets, stop);
                // Keep new towns (and other stations) clear of the whole pad.
                float outer = request.Radius + 8; var best = request.Pad.WorldCenter;
                avoid.Add(best);
                for (int i = 0; i < 8; i++)
                { float yaw = i * Mathf.PI * .25f; avoid.Add(best + new Vector3(Mathf.Sin(yaw) * outer, 0, Mathf.Cos(yaw) * outer)); }
            }
        }
        private static void PlanDesert(Request request, TileData data, List<Vector3> avoid, TownPlan saved, TownWorldRoads roads, StopToken stop)
        {
            var area = data.area.active; float outer = request.Radius + 8, border = outer + 24;
            var random = new System.Random(TownPlanner.Hash(request.Key + "/gasstation/site"));
            var points = roads.Sites(outer, outer + 70);
            for (int c = 0; c < 48; c++)
                points.Add(new Vector3(area.worldPos.x + border + (float)random.NextDouble() * (area.worldSize.x - border * 2), 0,
                    area.worldPos.z + border + (float)random.NextDouble() * (area.worldSize.z - border * 2)));
            float bestCost = float.PositiveInfinity; Vector3 best = Vector3.zero, bestRoad = Vector3.zero;
            foreach (var p in points)
            {
                if (stop != null && stop.stop) return;
                if (p.x < area.worldPos.x + border || p.z < area.worldPos.z + border
                    || p.x > area.worldPos.x + area.worldSize.x - border || p.z > area.worldPos.z + area.worldSize.z - border) continue;
                bool blocked = false;
                foreach (var poi in avoid) if (Flat(p - poi) < outer + 70) { blocked = true; break; }
                if (saved != null && saved.HasTown && Flat(p - saved.WorldCenter) < outer + saved.Radius + saved.Feather + 20) blocked = true;
                if (blocked) continue;
                Vector3 road;
                if (!roads.Nearest(p, outer + 80, out road)) continue;            // a roadside stop, never out in the dunes
                float distance = Flat(road - p); if (distance < outer + 4) continue;
                float low = float.PositiveInfinity, high = float.NegativeInfinity;
                for (int z = -2; z <= 2; z++) for (int x = -2; x <= 2; x++)
                {
                    float y = data.heights.GetWorldInterpolatedValue(p.x + x * outer * .5f, p.z + z * outer * .5f) * data.globals.height;
                    low = Mathf.Min(low, y); high = Mathf.Max(high, y);
                }
                if (high - low > (VistasWorldGen.Present ? 1.5f : 8)) continue;
                float cost = (high - low) + distance * .02f;
                if (cost < bestCost) { bestCost = cost; best = new Vector3(p.x, (low + high) * .5f, p.z); bestRoad = road; }
            }
            if (float.IsPositiveInfinity(bestCost)) { request.Failure = "no flat roadside site"; return; }
            // the canopy's open (-x) side faces the road
            var toRoad = bestRoad - best; toRoad.y = 0; toRoad.Normalize();
            request.Yaw = Mathf.Atan2(toRoad.z, -toRoad.x) * Mathf.Rad2Deg;
            request.Pad = new TownPlan { WorldCenter = best, Radius = request.Radius, Feather = 10 };
        }
        private static void PlanTest(Request request, TileData data, List<Vector3> avoid, TownPlan saved, StopToken stop)
        {
            float nearest = float.PositiveInfinity;
            foreach (var p in avoid) nearest = Mathf.Min(nearest, Flat(p - request.Camp));
            if (saved != null && saved.HasTown) nearest = Mathf.Min(nearest, Flat(saved.WorldCenter - request.Camp));
            float outer = request.Radius + 8, first = outer + 32;
            Vector3 best = Vector3.zero; float bestRelief = float.PositiveInfinity;
            var area = data.area.active;
            for (int ring = 0; ring < 12; ring++)
            {
                float distance = first + ring * 12;
                if (distance >= nearest - 1) break; // This test must be closer to camp than any existing POI.
                for (int angle = 0; angle < 32; angle++)
                {
                    if (stop != null && stop.stop) return;
                    float yaw = angle * Mathf.PI / 16;
                    var p = request.Camp + new Vector3(Mathf.Sin(yaw) * distance, 0, Mathf.Cos(yaw) * distance);
                    if (p.x - outer - 2 < area.worldPos.x || p.z - outer - 2 < area.worldPos.z
                        || p.x + outer + 2 > area.worldPos.x + area.worldSize.x || p.z + outer + 2 > area.worldPos.z + area.worldSize.z) continue;
                    bool blocked = false;
                    foreach (var poi in avoid) if (Flat(p - poi) < outer + 70) { blocked = true; break; }
                    if (saved != null && saved.HasTown && Flat(p - saved.WorldCenter) < outer + saved.Radius + saved.Feather + 10) blocked = true;
                    if (blocked) continue;
                    float low = float.PositiveInfinity, high = float.NegativeInfinity;
                    for (int z = -2; z <= 2; z++) for (int x = -2; x <= 2; x++)
                    {
                        float y = data.heights.GetWorldInterpolatedValue(p.x + x * outer * .5f, p.z + z * outer * .5f) * data.globals.height;
                        low = Mathf.Min(low, y); high = Mathf.Max(high, y);
                    }
                    if (high - low < bestRelief) { bestRelief = high - low; best = new Vector3(p.x, (low + high) * .5f, p.z); }
                }
                if (!float.IsPositiveInfinity(bestRelief)) break; // nearest safe ring, then least earthwork
            }
            if (float.IsPositiveInfinity(bestRelief))
            { request.Failure = "No safe pad closer to camp than the existing POIs; native POIs and saved towns retained."; return; }
            request.Pad = new TownPlan { WorldCenter = best, Radius = request.Radius, Feather = 8 };
        }
        private static float Flat(Vector3 delta) { return Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z); }

        // ------------------------------------------------------------------ placement (main thread, tile applied)
        internal void Applied(List<Request> requests, TerrainTile tile, TownAssets assets, PoiDressing dressing)
        {
            if (requests == null || _failed) return;
            foreach (var request in requests)
            {
                if (request.Failure != null)
                {
                    if (!request.Desert) { _status = request.Failure; Log.Warn("[Towns] Gas station test: " + _status); }
                    else Log.Info("[Towns] desert gas station " + request.Key + " skipped: " + request.Failure);
                    continue;
                }
                if (request.Pad != null && request.Prefab != null) { if (_settings.TestCamp10.Value) PlaceCamp(request, tile); continue; }
                if (request.Pad == null || !Available) continue;
                if (!request.Desert && (!_settings.TestGasStation.Value || Mathf.Abs(request.Scale - _settings.GasStationScale.Value) > .0001f)) continue;
                Remove(request.Key, true);
                try
                {
                    var root = Shell(tile.objectsPool.transform, request.Desert ? "TownsGasStation (desert " + request.Key + ")" : "TownsGasStation");
                    root.transform.position = tile.mapMagic.transform.TransformPoint(request.Pad.WorldCenter) + Vector3.up * .1f;
                    if (VistasWorldGen.Present) { var p = root.transform.position; p.y = tile.main.terrain.SampleHeight(p) + tile.main.terrain.transform.position.y + .1f; root.transform.position = p; }
                    root.transform.rotation = tile.mapMagic.transform.rotation * Quaternion.Euler(0, request.Yaw, 0);
                    root.transform.localScale = Vector3.one * request.Scale;
                    var station = new Station { Key = request.Key, Desert = request.Desert, Root = root, Tile = tile, Request = request };
                    if (request.Desert) Populate(station, assets);
                    root.SetActive(true);
                    Physics.SyncTransforms(); TownBakeBridge.Discover(root);
                    if (dressing != null) dressing.Attach(root.transform);
                    _stations.Add(station);
                    if (!request.Desert)
                    {
                        _test = station; _status = "Waiting for ground-floor navigation bake";
                        Log.Always("[Towns] Gas station test placed at " + request.Pad.WorldCenter + "; camp distance "
                            + Flat(request.Pad.WorldCenter - request.Camp).ToString("0.0", CultureInfo.InvariantCulture) + " m; scale " + request.Scale.ToString("0.###", CultureInfo.InvariantCulture));
                    }
                    else Log.Always("[Towns] desert gas station " + request.Key + " at " + request.Pad.WorldCenter + ", facing its road; " + station.Spawners.Count + " native spawners.");
                }
                catch (Exception e) { Log.Warn("[Towns] gas station " + request.Key + " placement: " + e.Message); Remove(request.Key, false); }
            }
        }
        // the Camp_10 test pad: the native camp, stripped of its scripts (no spawners, merchant or other logic), on the level pad
        private void PlaceCamp(Request request, TerrainTile tile)
        {
            Remove(request.Key, false);
            var holder = new GameObject("Towns camp test staging"); holder.SetActive(false);
            try
            {
                var go = Object.Instantiate(request.Prefab, holder.transform, false);   // under an inactive parent: no Awake before stripping
                go.name = request.Prefab.name + " (WW camp test)";
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) Object.DestroyImmediate(mb);
                // rigidbodies and colliders stay exactly as authored: the bake must match the town template (same collider set)
                go.transform.SetParent(tile.objectsPool.transform, false);
                go.transform.position = tile.mapMagic.transform.TransformPoint(request.Pad.WorldCenter);
                if (VistasWorldGen.Present) { var p = go.transform.position; p.y = tile.main.terrain.SampleHeight(p) + tile.main.terrain.transform.position.y; go.transform.position = p; }
                go.transform.rotation = tile.mapMagic.transform.rotation;
                go.SetActive(true);
                Physics.SyncTransforms(); TownBakeBridge.Discover(go);
                _stations.Add(new Station { Key = request.Key, Root = go, Tile = tile, Request = request, Label = "Camp_10 test" });
                Log.Always("[Towns] Camp_10 test placed on a level pad at " + request.Pad.WorldCenter + " (geometry only): stand near it until its navigation is baked.");
            }
            catch (Exception e) { Log.Warn("[Towns] Camp_10 test placement: " + e.Message); }
            finally { Object.Destroy(holder); }
        }
        /// <summary>The bare station (render shell + collider), inactive, under `parent`. Used for desert stations and town parking lots.</summary>
        internal GameObject Shell(Transform parent, string name)
        {
            var root = new GameObject(name); root.SetActive(false); root.tag = "SpawnerStatic";
            root.transform.SetParent(parent, false);
            root.AddComponent<MeshFilter>().sharedMesh = _model.Mesh;
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterials = _materials;
            // Preserve the GLB's double-sided shell for shadows, including the wall beside the entrance.
            renderer.shadowCastingMode = _model.DoubleSided ? UnityEngine.Rendering.ShadowCastingMode.TwoSided : UnityEngine.Rendering.ShadowCastingMode.On;
            var collider = root.AddComponent<MeshCollider>(); collider.convex = false; collider.sharedMesh = _collision.Mesh;
            return root;
        }

        // 4 loot spawners in the shop + 2-3 Scrapyard OR animal spawners, cloned from native town templates; saved progress restored
        private void Populate(Station station, TownAssets assets)
        {
            Transform loot = null, enemy = null; int lootKids = int.MaxValue, enemyKids = int.MaxValue;
            foreach (var asset in assets.Buildings.Values)
                for (int i = 0; i < asset.Spawners.Length; i++)
                {
                    int kind = asset.Budgets[i].Kind; if (kind != 1 && kind != 2) continue;
                    var node = asset.Spawners[i].Length == 0 ? null : asset.Prefab.transform.Find(asset.Spawners[i]); if (node == null) continue;
                    int kids = node.GetComponentsInChildren<Transform>(true).Length;
                    if (kind == 2 && kids < lootKids) { loot = node; lootKids = kids; }
                    if (kind == 1 && kids < enemyKids) { enemy = node; enemyKids = kids; }
                }
            if (loot == null && enemy == null) { Log.Warn("[Towns] desert gas station: no native spawner templates in the catalog"); return; }
            var random = new System.Random(TownPlanner.Hash(station.Key + "/gasstation/population"));
            bool animals = random.Next(2) == 0;
            List<GameObject> pool;
            if (!assets.Population.TryGetValue(animals ? TownFaction.Animals : TownFaction.Scrapyard, out pool) || pool.Count == 0)
                assets.Population.TryGetValue(animals ? TownFaction.Scrapyard : TownFaction.Animals, out pool);
            int enemies = 2 + random.Next(2);
            SpawnerState[] saved; _saved.TryGetValue(station.Key, out saved);
            int n = 0;
            bool authoredLoot = false;
            try { foreach (var prop in PoiDressStore.Load("TownsGasStation").Props) if (prop.Kind == "Items" || prop.Kind == "CarParts") { authoredLoot = true; break; } }
            catch (Exception e) { Log.Warn("[Towns] gas station retains native loot: " + e.Message); }
            for (int i = 0; !authoredLoot && i < LootPoints.Length && loot != null; i++, n++) Add(station, loot, LootPoints[i], "ItemSpawn_gas_" + i, 1 + random.Next(2), null, saved, n);
            // (WW) enemy spawners are OFF for now (they spawned badly); loot only. Re-enable by setting SpawnEnemies.
            for (int i = 0; SpawnEnemies && i < enemies && enemy != null && pool != null && pool.Count > 0; i++, n++) Add(station, enemy, EnemyPoints[i], "EnemySpawn_gas_" + i, 1 + random.Next(2), pool, saved, n);
        }
        private void Add(Station station, Transform template, Vector3 local, string name, int count, List<GameObject> pool, SpawnerState[] saved, int index)
        {
            var go = Object.Instantiate(template.gameObject, station.Root.transform, false);
            go.name = name; go.transform.localPosition = local; go.transform.localRotation = Quaternion.identity; go.SetActive(true);
            var f = FsmUtil.Find(go, "ItemSpawner"); if (f == null) { Object.Destroy(go); return; }
            if (saved != null && index < saved.Length && saved[index] != null && saved[index].Path == name)
            {
                TownPopulation.Write(f, "OnOff", saved[index].OnOff); TownPopulation.Write(f, "number", saved[index].Number); TownPopulation.Write(f, "numberEnd", saved[index].NumberEnd);
            }
            TownPopulation.Force(f, count, pool);
            station.Spawners.Add(f);
        }

        // ------------------------------------------------------------------ saving (spawner progress of the desert stations)
        private void CaptureStates(Station station)
        {
            if (!station.Desert || station.Spawners.Count == 0) return;
            var states = new SpawnerState[station.Spawners.Count];
            for (int i = 0; i < states.Length; i++)
            {
                var f = station.Spawners[i];
                states[i] = new SpawnerState { Path = f != null ? f.gameObject.name : "", OnOff = TownPopulation.Read(f, "OnOff"), Number = TownPopulation.Read(f, "number"), NumberEnd = TownPopulation.Read(f, "numberEnd") };
            }
            _saved[station.Key] = states;
        }
        internal void Save(string slot)
        {
            foreach (var station in _stations) CaptureStates(station);
            var sb = new StringBuilder();
            foreach (var kv in _saved)
            {
                sb.Append(kv.Key);
                foreach (var s in kv.Value) sb.Append('\t').Append(s.Path).Append(',').Append(s.OnOff).Append(',').Append(s.Number).Append(',').Append(s.NumberEnd);
                sb.Append('\n');
            }
            string value = sb.ToString();
            var cache = new ES3Settings(slot) { location = ES3.Location.Cache };
            ES3.Save<string>(SaveKey, value, cache);
            SaveBus.AfterSave(() => { try { ES3.Save<string>(SaveKey, value, cache); } catch (Exception e) { Log.Warn("[Towns] gas station save: " + e.Message); } });
        }
        internal void Load(string slot)
        {
            _saved.Clear();
            if (string.IsNullOrEmpty(slot)) return;
            try
            {
                var disk = new ES3Settings(slot) { location = ES3.Location.File };
                if (!File.Exists(disk.FullPath) || !ES3.KeyExists(SaveKey, disk)) return;
                foreach (string line in ES3.Load<string>(SaveKey, disk).Split('\n'))
                {
                    var parts = line.Split('\t'); if (parts.Length < 2) continue;
                    var states = new SpawnerState[parts.Length - 1];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var v = parts[i].Split(','); if (v.Length < 4) continue;
                        states[i - 1] = new SpawnerState { Path = v[0], OnOff = int.Parse(v[1], CultureInfo.InvariantCulture), Number = int.Parse(v[2], CultureInfo.InvariantCulture), NumberEnd = int.Parse(v[3], CultureInfo.InvariantCulture) };
                    }
                    _saved[parts[0]] = states;
                }
            }
            catch (Exception e) { Log.Warn("[Towns] gas station spawner states not loaded: " + e.Message); _saved.Clear(); }
        }
        internal void NewWorld() { Clear(false); _saved.Clear(); }

        // ------------------------------------------------------------------ per tick: markers, the test pad's raiders
        internal void Tick()
        {
            if (_stations.Count == 0 || !GameRefs.InGame || SaveBus.Busy || _failed) return;
            if (_test != null && !_settings.TestGasStation.Value) Remove(_test.Key, false);
            if (_map != Services.Map) { if (_map != null) _map.RemoveAll(MarkerOwner); _map = Services.Map; foreach (var s in _stations) s.MapId = 0; }
            if (_compass != Services.Compass) { if (_compass != null) _compass.RemoveAll(MarkerOwner); _compass = Services.Compass; foreach (var s in _stations) s.CompassId = 0; }
            var player = GameRefs.Player;
            foreach (var s in _stations)
            {
                if (s.Root == null) continue;
                Station captured = s;
                if (_map != null && _map.Markers != MapDetail.Off && !_map.Exists(s.MapId)) s.MapId = _map.Add(MarkerOwner, captured.Position, "building", Color.white, s.Label ?? (s.Desert ? "Gas station" : "Gas station test"));
                bool near = player != null && (player.transform.position - s.Position()).sqrMagnitude < 400f * 400f;
                if (_compass != null && near && !_compass.Exists(s.CompassId)) s.CompassId = _compass.Add(MarkerOwner, captured.Position, "building", Color.white);
                else if (_compass != null && !near && _compass.Exists(s.CompassId)) { _compass.Remove(s.CompassId); s.CompassId = 0; }
            }
            if (_test != null) TestTick(player);
        }
        private void TestTick(GameObject player)
        {
            var root = _test.Root;
            if (root == null || !root.activeInHierarchy) return;
            if (!TownBakeBridge.Enabled || Services.Npc == null) { _status = "Enable NPC navigation to bake the test station"; return; }
            if (player == null || (player.transform.position - root.transform.position).sqrMagnitude > 500f * 500f) return;
            try
            {
                if (!_pointsReady)
                {
                    bool known, baked; TownBakeBridge.Cells(root.transform, true, _cells, out known, out baked);
                    if (!known) TownBakeBridge.Discover(root);
                    if (!baked) return;
                    for (int i = _cells.Count - 1; i >= 0; i--)
                    {
                        var cell = _cells[i]; var local = root.transform.InverseTransformPoint(cell); RaycastHit hit;
                        if (local.y < -.05f || local.y > .15f
                            || !Physics.Raycast(cell + Vector3.up * .08f, Vector3.down, out hit, .16f, TownNavigation.Mask, QueryTriggerInteraction.Ignore)
                            || hit.collider.gameObject != root) _cells.RemoveAt(i);
                    }
                    while (_spawnPoints.Count < 3 && _cells.Count > 0)
                    {
                        int best = -1; float score = float.NegativeInfinity;
                        for (int i = 0; i < _cells.Count; i++)
                        {
                            float distance = float.PositiveInfinity;
                            foreach (var point in _spawnPoints) distance = Mathf.Min(distance, (_cells[i] - point).sqrMagnitude);
                            if (_spawnPoints.Count > 0 && distance < 9) continue;
                            float value = _spawnPoints.Count == 0 ? -Flat(_cells[i] - root.transform.position) : distance;
                            if (value > score) { score = value; best = i; }
                        }
                        if (best < 0) break;
                        _spawnPoints.Add(_cells[best]); _cells.RemoveAt(best);
                    }
                    _pointsReady = true;
                    if (_spawnPoints.Count < 3) { _status = "Bake has fewer than 3 spaced ground-floor spawn cells; check scale"; Log.Warn("[Towns] Gas station test: " + _status); return; }
                    var manager = ES3ReferenceMgrBase.Current;
                    var prefab = manager == null ? null : manager.GetPrefab(2524626245796737373L, true);
                    if (prefab == null || prefab.name != "Scraffa") throw new InvalidOperationException("Native Scraffa melee prefab unavailable");
                    _raider = prefab.gameObject;
                }
                if (_spawnPoints.Count < 3 || _spawned >= 3) return;
                var feet = _spawnPoints[_spawned];
                if ((player.transform.position - feet).sqrMagnitude < 4) return;
                var actor = Object.Instantiate(_raider, feet + Vector3.up * 1.05f, Quaternion.identity);
                _actors.Add(actor); _spawned++;
                var save = FsmUtil.Find(actor, "saveItemVar"); if (save != null) save.enabled = false;
                TownBakeBridge.Actor(actor);
                _status = _spawned == 3 ? "Ready: 3 melee Scrapyard raiders (unsaved test)" : "Spawning melee raiders " + _spawned + "/3";
            }
            catch (Exception e) { _status = "Failed navigation/population: " + e.Message; Log.Warn("[Towns] Gas station test " + _status); _spawnPoints.Clear(); _spawned = 3; }
        }

        private void LoadModel()
        {
            if (_model != null) return;
            _model = LoadMesh("gasstation.glb", false);
            _collision = LoadMesh("gasstation_collider.glb", true);
            // Supplied exports have identical extents but different object origins. Keep their authored size,
            // align the collider to the visible shell, then anchor both to the top of the foundation slab.
            if ((_model.Bounds.size - _collision.Bounds.size).sqrMagnitude > .001f)
                throw new InvalidDataException("Gas station collider dimensions do not match the render model; export both at the same scale");
            var alignment = _model.Bounds.center - _collision.Bounds.center;
            var vertices = _model.Mesh.vertices; float floor = float.NegativeInfinity;
            for (int sub = 0; sub < _model.NodeNames.Count; sub++) if (_model.NodeNames[sub] == "concrete_1" || _model.MaterialNames[sub] == "concrete_1")
                foreach (int index in _model.Mesh.GetTriangles(sub)) floor = Mathf.Max(floor, vertices[index].y);
            if (float.IsNegativeInfinity(floor)) throw new InvalidDataException("Gas station foundation node concrete_1 missing");
            ShiftMesh(_model, Vector3.down * floor); ShiftMesh(_collision, alignment + Vector3.down * floor);
            Log.Always("[Towns] Gas station meshes aligned: collider offset " + alignment + "; foundation top offset "
                + (-floor).ToString("0.###", CultureInfo.InvariantCulture) + " m; collider " + _collision.Mesh.vertexCount + " vertices; authored scale retained (use debug scale 1).");
            var manager = ES3ReferenceMgrBase.Current;
            if (manager == null) throw new InvalidOperationException("Native material reference manager unavailable");
            var names = new[] { "concrete_1", "brick_6", "debris_garbage_0026_01_s", "metal_rusted_21", "metal_rusted_24", "metal_rusted_9", "floor2" };
            var ids = new long[] { 4156000037244653851L, 6769621802523452942L, 8270357246983211973L, 3912141191000562135L, 3797490793376262977L, 4505092729398911849L, 8376223270176389302L };
            var native = new Dictionary<string, Material>(StringComparer.Ordinal);
            for (int i = 0; i < names.Length; i++)
            {
                var material = manager.Get(ids[i], true) as Material;
                if (!UsesTexture(material, names[i]))
                {
                    // the saved reference id did not resolve (not loaded yet / another game build): any loaded material with that texture
                    material = null;
                    foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                        if (UsesTexture(m, names[i])) { material = m; break; }
                    if (material == null && i > 0)
                    {
                        // the texture itself (loaded, or the copy shipped next to the model: Towns/Models/<name>.png) on concrete_1's shader
                        Texture tex = null;
                        foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>()) if (t != null && t.name == names[i]) { tex = t; break; }
                        if (tex == null)
                        {
                            string png = Path.Combine(Path.Combine(Assets.ModuleDir("Towns"), "Models"), names[i] + ".png");
                            if (File.Exists(png)) { var t2 = new Texture2D(2, 2, TextureFormat.RGBA32, true); if (t2.LoadImage(File.ReadAllBytes(png))) { t2.name = names[i]; t2.wrapMode = TextureWrapMode.Repeat; tex = t2; } }
                        }
                        if (tex != null)
                        {
                            material = new Material(native["concrete_1"]) { name = names[i], mainTexture = tex };
                            Log.Always("[Towns] Gas station: " + names[i] + " built from its texture (" + tex.width + "x" + tex.height + ")");
                        }
                    }
                    if (material == null && i > 0)
                    {
                        material = native["concrete_1"];      // a cosmetic part only: concrete rather than no gas station at all
                        Log.Warn("[Towns] Gas station: native texture " + names[i] + " not found, that part uses concrete_1");
                    }
                    if (material == null) throw new InvalidDataException("Native material/texture not found: " + names[i]);
                }
                native.Add(names[i], material);
            }
            _materials = new Material[_model.Mesh.subMeshCount];
            for (int i = 0; i < _materials.Length; i++)
            {
                string key = _model.ImageNames[i] == null ? _model.MaterialNames[i] : Path.GetFileNameWithoutExtension(_model.ImageNames[i]);
                if (key == null || key.StartsWith("Material", StringComparison.Ordinal)) key = _model.NodeNames[i];
                if (key == "metalrusted21") key = "metal_rusted_21";
                else if (key == "debris_garbage_trash") key = "debris_garbage_0026_01_s";
                Material material;
                if (!native.TryGetValue(key, out material)) throw new InvalidDataException("Unmapped GLB part: " + _model.NodeNames[i] + "; texture " + key);
                _materials[i] = material;
                Log.Always("[Towns] Gas station material: " + _model.NodeNames[i] + " -> " + key);
            }
        }
        private static bool UsesTexture(Material material, string name)
        {
            // Loaded materials include internal shaders without a _MainTex texture property.
            if (material == null || Array.IndexOf(material.GetTexturePropertyNames(), "_MainTex") < 0) return false;
            var texture = material.GetTexture("_MainTex");
            return texture != null && texture.name == name;
        }
        private static void ShiftMesh(GasStation.GlbModel model, Vector3 offset)
        {
            var vertices = model.Mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] += offset;
            model.Mesh.vertices = vertices; model.Mesh.RecalculateBounds(); model.Bounds = model.Mesh.bounds;
        }
        private static GasStation.GlbModel LoadMesh(string file, bool collider)
        {
            string path = Path.Combine(Assets.ModuleDir("Towns"), "Models", file);
            if (!File.Exists(path)) throw new FileNotFoundException("Install the gas station model in Towns/Models: " + file, path);
            var model = GasStation.Glb.Load(path, collider);
            // The native signature includes mesh name/count/bounds; include all GLB bytes so edited triangles invalidate it too.
            using (var sha = SHA256.Create()) model.Mesh.name = Path.GetFileNameWithoutExtension(file) + "-" + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
            Log.Always("[Towns] Gas station mesh loaded: " + path + "; " + model.Mesh.vertexCount + " vertices, " + model.Mesh.subMeshCount + " submeshes; scale-1 size " + model.Bounds.size);
            return model;
        }
        private void Fail(string stage, Exception e)
        { _failed = true; _status = "Failed " + stage + ": " + e.Message; Log.Warn("[Towns] Gas station " + _status); }
        private void Remove(string key, bool capture)
        {
            for (int i = _stations.Count - 1; i >= 0; i--)
            {
                var s = _stations[i]; if (s.Key != key) continue;
                Destroy(s, capture); _stations.RemoveAt(i);
            }
        }
        private void Destroy(Station s, bool capture)
        {
            if (capture) CaptureStates(s);
            if (_map != null && s.MapId != 0) _map.Remove(s.MapId); if (_compass != null && s.CompassId != 0) _compass.Remove(s.CompassId);
            if (s == _test)
            {
                foreach (var actor in _actors) if (actor != null) Object.Destroy(actor);
                _actors.Clear(); _spawnPoints.Clear(); _cells.Clear(); _spawned = 0; _pointsReady = false; _test = null;
            }
            if (s.Root != null) { s.Root.SetActive(false); Object.Destroy(s.Root); }
        }
        internal void Moved(TerrainTile tile)
        {
            for (int i = _stations.Count - 1; i >= 0; i--) if (_stations[i].Tile == tile) { Destroy(_stations[i], true); _stations.RemoveAt(i); }
        }
        internal void Shift(Vector3 delta)
        {
            // Native floating origin moves the roots and NPCs. Only our not-yet-used cell snapshots need correction.
            for (int i = 0; i < _spawnPoints.Count; i++) _spawnPoints[i] += delta;
        }
        internal void Clear(bool model)
        {
            foreach (var s in _stations) Destroy(s, false);
            _stations.Clear();
            if (_map != null) _map.RemoveAll(MarkerOwner); if (_compass != null) _compass.RemoveAll(MarkerOwner);
            _map = null; _compass = null;
            if (model)
            {
                if (_model != null) { Object.Destroy(_model.Mesh); foreach (var mesh in _model.Colliders) Object.Destroy(mesh); }
                if (_collision != null) { Object.Destroy(_collision.Mesh); foreach (var mesh in _collision.Colliders) Object.Destroy(mesh); }
                _model = _collision = null; _materials = null; _raider = null; _camp = null; _failed = false;
                _status = _settings.TestGasStation.Value ? "Reload world to prepare test site" : "Off (reload after enabling)";
            }
        }
        internal void Draw(ToolLayout l)
        {
            l.Entry(_settings.DesertGasStationChance); l.Entry(_settings.GasStationParking);
            l.Entry(_settings.TestGasStation); l.Entry(_settings.GasStationScale); l.Entry(_settings.TestCamp10);
            l.Text("Desert stations: per generated 5 km block, beside a road, with native loot and Scrapyard or animal spawners. Town stations stand beside the open parking lot. The test pad (reload after changing) is near the starting camp with three melee raiders.");
            l.Value("Gas stations loaded", _stations.Count.ToString());
            l.Value("Gas station test", _status);
        }
        public void Dispose() { Clear(true); if (ReferenceEquals(Current, this)) Current = null; }
    }
}
