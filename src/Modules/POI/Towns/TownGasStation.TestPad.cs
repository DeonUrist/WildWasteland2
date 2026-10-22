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
    /// <summary>
    /// Developer test pads near the starting camp: the gas-station test (three melee raiders after its ground-floor bake) and the
    /// stripped Camp_10 test, plus the gas-station debug page. Wave A (A6): partial split of Towns/TownGasStation.cs (Tools part, B16).
    /// </summary>
    internal sealed partial class TownGasStation : IGasStationAuthoring
    {
        /// <summary>Returns the latest test-placement status.</summary>
        public string Status=>_status;
        /// <summary>Counts the currently materialized gas stations.</summary>
        public int LoadedCount=>_stations.Count;
        /// <summary>Returns the live developer test-pad root.</summary>
        public Transform TestRoot=>Root;
        /// <summary>Copies the module-owned authoring controls for Tools to render.</summary>
        public void Settings(List<BepInEx.Configuration.ConfigEntryBase> into){into.Clear();into.Add(_settings.DesertGasStationChance);into.Add(_settings.GasStationParking);into.Add(_settings.TestGasStation);into.Add(_settings.GasStationScale);into.Add(_settings.TestCamp10);}
        // developer test pad
        private readonly List<Vector3> _cells = new List<Vector3>(256);
        private readonly List<Vector3> _spawnPoints = new List<Vector3>(3);
        private readonly List<GameObject> _actors = new List<GameObject>(3);
        private Station _test;
        private int _spawned; private bool _pointsReady;
        private GameObject _raider;
        private Transform _camp;
        internal Transform Root { get { return _test != null && _test.Root != null ? _test.Root.transform : null; } }
        /// <summary>Locates the native starting camp inside the generated tile.</summary>
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
        /// <summary>Chooses a clear test pad closer to the starting camp than other authored locations.</summary>
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
        // the Camp_10 test pad: the native camp, stripped of its scripts (no spawners, merchant or other logic), on the level pad
        /// <summary>Places static test-camp geometry with native spawning logic removed.</summary>
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
                if (WorldGeneration.Present) { var p = go.transform.position; p.y = tile.main.terrain.SampleHeight(p) + tile.main.terrain.transform.position.y; go.transform.position = p; }
                go.transform.rotation = tile.mapMagic.transform.rotation;
                go.SetActive(true);
                Physics.SyncTransforms(); TownBakeBridge.Discover(go);
                _stations.Add(new Station { Key = request.Key, Root = go, Tile = tile, Request = request, Label = "Camp_10 test" });
                Log.Main(null, "[POI] Camp_10 test placed on a level pad at " + request.Pad.WorldCenter + " (geometry only): stand near it until its navigation is baked.");
            }
            catch (Exception e) { Log.Warn("[POI] Camp_10 test placement: " + e.Message); }
            finally { Object.Destroy(holder); }
        }
        /// <summary>Advances the navigation-gated developer test population.</summary>
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
                            || !Physics.Raycast(cell + Vector3.up * .08f, Vector3.down, out hit, .16f, Core.Layers.NavSolid, QueryTriggerInteraction.Ignore)
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
                    if (_spawnPoints.Count < 3) { _status = "Bake has fewer than 3 spaced ground-floor spawn cells; check scale"; Log.Warn("[POI] Gas station test: " + _status); return; }
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
            catch (Exception e) { _status = "Failed navigation/population: " + e.Message; Log.Warn("[POI] Gas station test " + _status); _spawnPoints.Clear(); _spawned = 3; }
        }
        /// <summary>Adjusts cached scene coordinates after a floating-origin jump.</summary>
        internal void Shift(Vector3 delta)
        {
            // Native floating origin moves the roots and NPCs. Only our not-yet-used cell snapshots need correction.
            for (int i = 0; i < _spawnPoints.Count; i++) _spawnPoints[i] += delta;
        }

        /// <summary>Adds the Camp_10 / gas-station test-pad requests of this tile (developer test pads near the starting camp).</summary>
        private void CaptureTests(TerrainTile tile, TileData data, TownAssets assets, ref List<Request> list)
        {
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
                if (_camp == null) { _status = "Waiting for the starting camp; reload if already playing"; return; }
                var camp = tile.mapMagic.transform.InverseTransformPoint(_camp.position);
                var area = data.area.active;
                if (camp.x < area.worldPos.x || camp.z < area.worldPos.z
                    || camp.x >= area.worldPos.x + area.worldSize.x || camp.z >= area.worldPos.z + area.worldSize.z) return;
                if (!Available) return;
                float scale = _settings.GasStationScale.Value;
                _status = "Preparing test pad near camp";
                if (list == null) list = new List<Request>();
                list.Add(new Request { Key = "test", Camp = camp, Scale = scale, Radius = Radius(scale) });
            }
        }
    }
}
