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

namespace WildWasteland.Modules.POI
{
    // The supplied gas station (GLB render shell + separate collider) in three roles:
    //  * a DESERT POI: [Towns / World] chance per generated 5 km block, on a flat pad beside a native road, facing it, with
    //    4 native loot spawners inside the shop and 2-3 native Scrapyard OR animal spawners (spawner progress is saved with the world);
    //  * a town PARKING LOT (TownFactory): the shell only, beside the separate open parking apron outside the walls;
    //  * the developer TEST pad near the starting camp (unsaved, three melee raiders after its bake).
    // All instances are named "TownsGasStation ..." so the one NPC bake, POI navigation and dressing layout apply to every copy.
    /// <summary>
    /// The supplied gas station (GLB shell + collider): desert roadside POI planning/placement, town parking shell, markers, saved spawner states.
    /// Wave A (A6): POI part of Towns/TownGasStation.cs; loot spawners in Loot/_fromTowns/TownGasStation.Loot.cs, test pads in Tools/_fromTowns/TownGasStation.TestPad.cs.
    /// </summary>
    internal sealed partial class TownGasStation : IDisposable
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
            /// <summary>Returns the live marker scene position.</summary>
            internal Vector3 Position() { return Root == null ? Vector3.zero : Root.transform.position; }
        }
        private const string MarkerOwner = "WildWasteland.Towns.GasStation";
        internal const string SaveKey = "WildWasteland.Towns.GasStations.v1";
        internal static TownGasStation Current;
        // Station-local layout (x = -glTF x, after the slab top is moved to y 0). The shop is x 3..11, z -11..13 (door at x 3.5, z -8);
        // the canopy lane x -4..2 runs the whole length, open at -z (the pumps stand at x -6, the bags/fence close +z beyond 11).
        internal const float LaneSpread = 5.75f;   // resident bus at +4.5 (deep), visitors at -7 (by the open end)
        private readonly TownSettings _settings;
        private readonly List<Station> _stations = new List<Station>();
        private readonly Dictionary<string, SpawnerState[]> _saved = new Dictionary<string, SpawnerState[]>(StringComparer.Ordinal);
        private GlbModel _model, _collision;
        private Material[] _materials;
        private IMapMarkers _map;
        private ICompassMarkers _compass;
        private bool _failed;
        private string _status = "Off (reload after enabling)";
        /// <summary>Initializes town gas station with the supplied owning state and immutable inputs.</summary>
        internal TownGasStation(TownSettings settings) { _settings = settings; Current = this; }
        private GameObject _catalogPrefab;
        /// <summary>Returns the inactive measured gas-station shell for the town prefab catalog.</summary>
        internal GameObject CatalogPrefab {get{if(!Available)return null;if(_catalogPrefab==null)_catalogPrefab=Shell(null,"TownsGasStation");return _catalogPrefab;}}
        /// <summary>Loads required station assets once and reports successful initialization.</summary>
        internal bool Available { get { if (_model == null && !_failed) { try { LoadModel(); } catch (Exception e) { Fail("model setup", e); } } return _model != null && _materials != null; } }

        // ------------------------------------------------------------------ requests (main thread, before the tile is generated)
        /// <summary>Captures immutable generation inputs or the current native progress.</summary>
        internal List<Request> Capture(TerrainTile tile, TileData data, string key, bool startingChunk, TownAssets assets)
        {
            if (data.isDraft || _failed) return null;
            List<Request> list = null;
            try
            {
                int chance = _settings.DesertGasStationChance.Value;
                if (chance > 0 && !(Core.ModApi.Get<Core.INavApi>()?.WorldGenerationSuspended==true) && !startingChunk && TownPlanner.Hash(key + "/gasstation") % 100 < chance && Available)
                    (list = new List<Request>()).Add(new Request { Key = key, Desert = true, Scale = 1f, Radius = Radius(1f) });
                CaptureTests(tile, data, assets, ref list);
            }
            catch (Exception e) { Fail("model setup", e); }
            return list;
        }
        /// <summary>Computes the required footprint radius from the authored model bounds.</summary>
        private float Radius(float scale)
        {
            var bounds = _model.Bounds;
            float x = Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x));
            float z = Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z));
            return Mathf.Sqrt(x * x + z * z) * scale + 2;
        }

        // Worker: only numeric snapshots and the existing terrain-apply buffers, never scene queries.
        /// <summary>Builds a numeric terrain-placement request without touching scene objects.</summary>
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
        /// <summary>Chooses an eligible roadside station site from numeric height and road inputs.</summary>
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
                if (high - low > (WorldGeneration.Present ? 1.5f : 8)) continue;
                float cost = (high - low) + distance * .02f;
                if (cost < bestCost) { bestCost = cost; best = new Vector3(p.x, (low + high) * .5f, p.z); bestRoad = road; }
            }
            if (float.IsPositiveInfinity(bestCost)) { request.Failure = "no flat roadside site"; return; }
            // the canopy's open (-x) side faces the road
            var toRoad = bestRoad - best; toRoad.y = 0; toRoad.Normalize();
            request.Yaw = Mathf.Atan2(toRoad.z, -toRoad.x) * Mathf.Rad2Deg;
            request.Pad = new TownPlan { WorldCenter = best, Radius = request.Radius, Feather = 10 };
        }
        /// <summary>Measures the horizontal length of the supplied vector.</summary>
        private static float Flat(Vector3 delta) { return Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z); }

        // ------------------------------------------------------------------ placement (main thread, tile applied)
        /// <summary>Publishes the completed numeric plan on the main thread.</summary>
        internal void Applied(List<Request> requests, TerrainTile tile, TownAssets assets, PoiDressing dressing)
        {
            if (requests == null || _failed) return;
            foreach (var request in requests)
            {
                if (request.Failure != null)
                {
                    if (!request.Desert) { _status = request.Failure; Log.Warn(null,"[POI] Gas station test: " + _status); }
                    else Log.Main(null,"[POI] desert gas station " + request.Key + " skipped: " + request.Failure);
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
                    if (WorldGeneration.Present) { var p = root.transform.position; p.y = tile.main.terrain.SampleHeight(p) + tile.main.terrain.transform.position.y + .1f; root.transform.position = p; }
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
                        Log.Main(null, "[POI] Gas station test placed at " + request.Pad.WorldCenter + "; camp distance "
                            + Flat(request.Pad.WorldCenter - request.Camp).ToString("0.0", CultureInfo.InvariantCulture) + " m; scale " + request.Scale.ToString("0.###", CultureInfo.InvariantCulture));
                    }
                    else Log.Main(null, "[POI] desert gas station " + request.Key + " at " + request.Pad.WorldCenter + ", facing its road; " + station.Spawners.Count + " native spawners.");
                }
                catch (Exception e) { Log.Warn(null,"[POI] gas station " + request.Key + " placement: " + e.Message); Remove(request.Key, false); }
            }
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


        // ------------------------------------------------------------------ saving (spawner progress of the desert stations)
        /// <summary>Snapshots live native spawner counters before streaming or saving.</summary>
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
        /// <summary>Writes the current validated state through its owning persistence transaction.</summary>
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
            SaveBus.Key(SaveKey).Write(null, slot, value);
        }
        /// <summary>Restores the current data format and rejects incompatible or corrupt input.</summary>
        internal void Load(string slot)
        {
            _saved.Clear();
            if (string.IsNullOrEmpty(slot)) return;
            var disk = new ES3Settings(slot) { location = ES3.Location.File };
            if (!File.Exists(disk.FullPath) || !ES3.KeyExists(SaveKey, disk)) return;
            foreach (string line in ES3.Load<string>(SaveKey, disk).Split('\n'))
            {
                if (line.Length == 0) continue;
                var parts = line.Split('\t');
                if (parts.Length < 2 || string.IsNullOrEmpty(parts[0]) || _saved.ContainsKey(parts[0])) throw new InvalidDataException("Invalid gas station state identity");
                var states = new SpawnerState[parts.Length - 1];
                for (int i = 1; i < parts.Length; i++)
                {
                    var v = parts[i].Split(',');
                    if (v.Length != 4) throw new InvalidDataException("Invalid gas station spawner state");
                    var state = new SpawnerState { Path = v[0], OnOff = int.Parse(v[1], CultureInfo.InvariantCulture), Number = int.Parse(v[2], CultureInfo.InvariantCulture), NumberEnd = int.Parse(v[3], CultureInfo.InvariantCulture) };
                    if (state.OnOff < 0 || state.OnOff > 1 || state.Number < 0 || state.NumberEnd < 0) throw new InvalidDataException("Invalid gas station spawner progress");
                    states[i - 1] = state;
                }
                _saved.Add(parts[0], states);
            }
        }

        /// <summary>Releases prior world records before starting a fresh world.</summary>
        internal void NewWorld() { Clear(false); _saved.Clear(); }

        // ------------------------------------------------------------------ per tick: markers, the test pad's raiders
        /// <summary>Advances the bounded runtime work admitted by its own cadence.</summary>
        internal void Tick()
        {
            if (_stations.Count == 0 || !GameRefs.InGame || SaveBus.Busy || _failed) return;
            if (_test != null && !_settings.TestGasStation.Value) Remove(_test.Key, false);
            if (_map != Core.ModApi.Get<Core.IMapMarkers>()) { if (_map != null) _map.RemoveAll(MarkerOwner); _map = Core.ModApi.Get<Core.IMapMarkers>(); foreach (var s in _stations) s.MapId = 0; }
            if (_compass != Core.ModApi.Get<Core.ICompassMarkers>()) { if (_compass != null) _compass.RemoveAll(MarkerOwner); _compass = Core.ModApi.Get<Core.ICompassMarkers>(); foreach (var s in _stations) s.CompassId = 0; }
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

        /// <summary>Loads and validates the authored render/collision meshes and native materials.</summary>
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
            Log.Main(null, "[POI] Gas station meshes aligned: collider offset " + alignment + "; foundation top offset "
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
                            string png = Path.Combine(Path.Combine(Assets.ModuleDir("POI"), "Models"), names[i] + ".png");
                            if (File.Exists(png)) { var t2 = new Texture2D(2, 2, TextureFormat.RGBA32, true); if (t2.LoadImage(File.ReadAllBytes(png))) { t2.name = names[i]; t2.wrapMode = TextureWrapMode.Repeat; tex = t2; } }
                        }
                        if (tex != null)
                        {
                            material = new Material(native["concrete_1"]) { name = names[i], mainTexture = tex };
                            Log.Main(null, "[POI] Gas station: " + names[i] + " built from its texture (" + tex.width + "x" + tex.height + ")");
                        }
                    }
                    if (material == null && i > 0)
                    {
                        material = native["concrete_1"];      // a cosmetic part only: concrete rather than no gas station at all
                        Log.Warn(null,"[POI] Gas station: native texture " + names[i] + " not found, that part uses concrete_1");
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
                Log.Main(null, "[POI] Gas station material: " + _model.NodeNames[i] + " -> " + key);
            }
        }
        /// <summary>Tests whether a native material references the requested texture.</summary>
        private static bool UsesTexture(Material material, string name)
        {
            // Loaded materials include internal shaders without a _MainTex texture property.
            if (material == null || Array.IndexOf(material.GetTexturePropertyNames(), "_MainTex") < 0) return false;
            var texture = material.GetTexture("_MainTex");
            return texture != null && texture.name == name;
        }
        /// <summary>Offsets render and collision vertices consistently in local space.</summary>
        private static void ShiftMesh(GlbModel model, Vector3 offset)
        {
            var vertices = model.Mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] += offset;
            model.Mesh.vertices = vertices; model.Mesh.RecalculateBounds(); model.Bounds = model.Mesh.bounds;
        }
        /// <summary>Loads a module-owned mesh and hashes its bytes for navigation identity.</summary>
        private static GlbModel LoadMesh(string file, bool collider)
        {
            string path = Path.Combine(Assets.ModuleDir("POI"), "Models", file);
            if (!File.Exists(path)) throw new FileNotFoundException("Install the gas station model in Towns/Models: " + file, path);
            var model = Glb.Load(path, collider);
            // The native signature includes mesh name/count/bounds; include all GLB bytes so edited triangles invalidate it too.
            using (var sha = SHA256.Create()) model.Mesh.name = Path.GetFileNameWithoutExtension(file) + "-" + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
            Log.Main(null, "[POI] Gas station mesh loaded: " + path + "; " + model.Mesh.vertexCount + " vertices, " + model.Mesh.subMeshCount + " submeshes; scale-1 size " + model.Bounds.size);
            return model;
        }
        /// <summary>Records an initialization failure without silently replacing valid world data.</summary>
        private void Fail(string stage, Exception e)
        { _failed = true; _status = "Failed " + stage + ": " + e.Message; Log.Warn(null,"[POI] Gas station " + _status); }
        /// <summary>Removes the matching registration and releases its live presentation state.</summary>
        private void Remove(string key, bool capture)
        {
            for (int i = _stations.Count - 1; i >= 0; i--)
            {
                var s = _stations[i]; if (s.Key != key) continue;
                Destroy(s, capture); _stations.RemoveAt(i);
            }
        }
        /// <summary>Releases the site population/navigation contexts before destroying its owned geometry.</summary>
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
        /// <summary>Releases live geometry belonging to a streamed terrain tile.</summary>
        internal void Moved(TerrainTile tile)
        {
            for (int i = _stations.Count - 1; i >= 0; i--) if (_stations[i].Tile == tile) { Destroy(_stations[i], true); _stations.RemoveAt(i); }
        }
        /// <summary>Releases current resources and resets the associated world or catalog state.</summary>
        internal void Clear(bool model)
        {
            foreach (var s in _stations) Destroy(s, false);
            _stations.Clear();
            if (_map != null) _map.RemoveAll(MarkerOwner); if (_compass != null) _compass.RemoveAll(MarkerOwner);
            _map = null; _compass = null;
            if (model)
            {
                if(_catalogPrefab!=null)Object.Destroy(_catalogPrefab);_catalogPrefab=null;
                if (_model != null) { Object.Destroy(_model.Mesh); foreach (var mesh in _model.Colliders) Object.Destroy(mesh); }
                if (_collision != null) { Object.Destroy(_collision.Mesh); foreach (var mesh in _collision.Colliders) Object.Destroy(mesh); }
                _model = _collision = null; _materials = null; _raider = null; _camp = null; _failed = false;
                _status = _settings.TestGasStation.Value ? "Reload world to prepare test site" : "Off (reload after enabling)";
            }
        }
        /// <summary>Releases owned resources and restores original native state.</summary>
        public void Dispose() { Clear(true); if (ReferenceEquals(Current, this)) Current = null; }
    }
}
