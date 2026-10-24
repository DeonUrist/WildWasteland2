using System;
using System.Collections.Generic;
using System.Threading;
using Den.Tools;
using Den.Tools.Matrices;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;
using WildWasteland.Core;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.POI
{
    internal sealed partial class TownRuntime
    {
        private sealed class VistaPending
        {
            internal TerrainTile Tile;
            internal TerrainData Terrain;
            internal Capture Capture;
            internal MatrixWorld Heights;
            internal int Row, Revision;
            internal bool Started, Connected;
            internal volatile bool Complete;
            internal readonly StopToken Stop = new StopToken();
            internal TownVistaClearance Clearance;
        }
        private readonly List<VistaPending> _vista = new List<VistaPending>();

        private void QueueVista(TerrainTile tile, Capture capture)
        {
            foreach (var old in _vista) if (old.Tile == tile) old.Stop.stop = true;
            _vista.RemoveAll(p => p.Tile == tile);
            _vista.Add(new VistaPending { Tile = tile, Terrain = tile.main.terrain.terrainData, Capture = capture });
            Log.Main(null, "[POI] town siting waiting for Vista roads: " + capture.Input.Key);
        }

        private void ClearVista()
        { foreach (var pending in _vista) pending.Stop.stop = true; _vista.Clear(); }

        private bool VistaStep()
        {
            if (!_running || _blocked || _vista.Count == 0 || SaveBus.Saving || (ModApi.Get<ISaveTransactionState>()?.Active ?? false)) return false;
            var p = _vista[0]; var capture = p.Capture; var tile = p.Tile;
            if (p.Stop.stop || capture.Epoch != _epoch || tile == null || tile.mapMagic == null || tile.main == null || tile.main.terrain == null
                || tile.main.terrain.terrainData != p.Terrain || ChunkKey(tile.mapMagic.graph.random.Seed, tile.coord.x, tile.coord.z) != capture.Input.Key)
            { p.Stop.stop = true; _vista.RemoveAt(0); capture.Input.Progress?.Finish("Cancelled — terrain streamed out"); return true; }
            var roads = ModApi.Get<IWorldGenRoads>();
            if (roads == null || !roads.Ready(tile.mapMagic.gameObject)) return false;
            var facts = ModApi.Get<IWorldGenFacts>();
            if (!p.Connected && p.Heights != null && p.Revision != facts.RoadRevision)
            {
                if (p.Started && !p.Complete) return false;
                p.Heights = null; p.Row = 0; p.Started = p.Complete = false; capture.Plan = null; capture.Input.Error = null;
            }
            if (p.Heights == null)
            {
                int res = p.Terrain.heightmapResolution; var area = capture.VistaData.area.active;
                p.Heights = new MatrixWorld(new CoordRect(0, 0, res, res), (Vector3)area.worldPos, p.Terrain.size);
                capture.VistaData.globals.height = p.Terrain.size.y; p.Revision = facts.RoadRevision;
                Log.Main(null, "[POI] Vista roads ready; town siting begins: " + capture.Input.Key + "; road revision " + p.Revision);
            }
            if (p.Row < p.Heights.rect.size.z)
            {
                int width = p.Heights.rect.size.x, count = Mathf.Min(16, p.Heights.rect.size.z - p.Row);
                var rows = p.Terrain.GetHeights(0, p.Row, width, count);
                Buffer.BlockCopy(rows, 0, p.Heights.arr, p.Row * width * sizeof(float), count * width * sizeof(float)); p.Row += count;
                return true;
            }
            if (!p.Started)
            {
                p.Started = true;
                var snapshot = new TownWorldRoads(null, capture.VistaData);
                ThreadPool.QueueUserWorkItem(ignored =>
                {
                    try { capture.Plan = TownPlanner.Make(capture.Input, capture.VistaData, p.Heights, capture.VistaAvoid, snapshot, p.Stop); }
                    catch (Exception e) { capture.Input.Error = "Town siting after Vista: " + e.Message; }
                    finally { p.Complete = true; }
                });
                return false;
            }
            if (!p.Complete) return false;
            if (capture.Input.Error != null || capture.Plan == null)
            {
                Log.Warn(null, "[POI] " + (capture.Input.Error ?? "Vista town planning cancelled"));
                capture.Input.Progress?.Finish("Town planning stopped"); _vista.RemoveAt(0); return true;
            }
            if (capture.Plan.HasTown && !p.Connected)
            {
                var path = capture.Plan.AccessPath; var world = new Vector3[path.Length];
                for (int i = 0; i < path.Length; i++) world[i] = capture.Plan.World(path[i]);
                // Existing saved towns keep their coordinates even when changed Vista settings leave no safe connector.
                if (world.Length < 2) Log.Warn(null, "[POI] Saved town retained without a safe Vista connector: " + capture.Input.Key);
                if (world.Length > 1 && !roads.Connect(capture.Input.Key, world))
                { capture.Input.Progress?.Finish("Vista connector unavailable"); Log.Warn(null, "[POI] Vista rejected town connector: " + capture.Input.Key); _vista.RemoveAt(0); return true; }
                p.Connected = true;
                return false; // Native Vista paints/grades the connector before foundations or buildings are placed.
            }
            if (capture.Plan.HasTown)
            {
                if (p.Clearance == null) p.Clearance = new TownVistaClearance(tile, capture.Plan, capture.Registration.Assets);
                if (p.Clearance.Step()) return true;
            }
            if (!capture.Plan.HasTown) capture.Input.Progress?.Finish(capture.Plan.Decision == "chance" ? "No town selected for this block" : capture.Plan.Decision);
            CommitTown(tile, capture); _vista.RemoveAt(0); return true;
        }
    }
}
