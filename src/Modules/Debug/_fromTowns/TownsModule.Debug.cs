using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MapMagic.Terrains;
using Den.Tools;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Towns diagnostics: Vistas starting-terrain seam trace and the (currently unused) status block Draw(ToolLayout).
    /// Wave A (A6): entry split of Towns/TownsModule.cs (Debug part, B03: trace at level 2 or delete).
    /// </summary>
    public sealed partial class TownsModule
    {
        private string _startingTerrainState;
        /// <summary>Vistas only: logs (on change) hole counts and height steps at the edges of the starting tile when a tile next to the origin is applied.</summary>
        partial void TraceStartingTerrain(TerrainTile applied, string source)
        {
            if (!VistasWorldGen.Present || applied == null || applied.mapMagic == null || Mathf.Abs(applied.coord.x) + Mathf.Abs(applied.coord.z) > 1) return;
            try
            {
                var map = applied.mapMagic; var start = map.tiles[new Coord(0, 0)];
                var terrain = start == null ? null : start.ActiveTerrain;
                if (terrain == null || terrain.terrainData == null) return;
                var data = terrain.terrainData; int res = data.holesResolution;
                string state = "seed=" + map.graph.random.Seed + "; active=" + (start.main != null && terrain == start.main.terrain ? "main" : "draft")
                    + "; size=" + data.size.ToString("F1") + "; heightRes=" + data.heightmapResolution + "; globalsHeight=" + map.globals.height
                    + "; terrainWrites=off; tests=" + _settings.TestGasStation.Value + "/" + _settings.TestCamp10.Value + "/" + Services.PoiNavSetup;
                int[] dx = { -1, 1, 0, 0 }, dz = { 0, 0, -1, 1 };
                for (int side = 0; side < 4; side++)
                {
                    var holes = data.GetHoles(side == 1 ? res - 1 : 0, side == 3 ? res - 1 : 0, side < 2 ? 1 : res, side < 2 ? res : 1);
                    int missing = 0; foreach (bool solid in holes) if (!solid) missing++;
                    state += "; edge" + side + " holes=" + missing + "/" + res;
                    var neighbour = map.tiles[new Coord(dx[side], dz[side])]; var other = neighbour == null ? null : neighbour.ActiveTerrain;
                    if (other == null || other.terrainData == null) { state += " neighbour=absent"; continue; }
                    var next = other.terrainData; float step = 0;
                    for (int s = 0; s <= 8; s++)
                    {
                        float u = side == 0 ? 0 : side == 1 ? 1 : s / 8f, v = side == 2 ? 0 : side == 3 ? 1 : s / 8f;
                        var p = terrain.transform.position + new Vector3(u * data.size.x, 0, v * data.size.z);
                        float a = data.GetInterpolatedHeight(u, v) + terrain.transform.position.y;
                        float b = next.GetInterpolatedHeight((p.x - other.transform.position.x) / next.size.x, (p.z - other.transform.position.z) / next.size.z) + other.transform.position.y;
                        step = Mathf.Max(step, Mathf.Abs(a - b));
                    }
                    state += " neighbourHeight=" + next.size.y.ToString("F1", CultureInfo.InvariantCulture) + " maxStep=" + step.ToString("F1", CultureInfo.InvariantCulture) + "m";
                }
                if (state == _startingTerrainState) return;
                _startingTerrainState = state; Log.Always("[Towns] Starting terrain (" + source + "): " + state);
            }
            catch (Exception e) { Log.Warn("[Towns] Starting terrain trace failed: " + e.Message); }
        }
        internal void Draw(ToolLayout layout)
        {
            layout.Value("Towns", _sites.Count.ToString()); layout.Value("Saved chunk decisions", _records.Count.ToString());
            layout.Value("Generation", _blocked ? "BLOCKED - see [Towns] log" : "ready");
            foreach (var live in _sites) layout.Value(live.Site.Plan.Key,
                live.Site.Plan.Condition + ", " + live.Site.NextLot + "/" + live.Site.Plan.LotCount + " lots, " + (live.Site.Navigation != null && live.Site.Navigation.Ready ? "connected" : "waiting for bakes"));
        }
        /// <summary>Forgets the last starting-terrain trace so the next applied tile logs again.</summary>
        partial void ClearDebug() { _startingTerrainState = null; }
    }
}
