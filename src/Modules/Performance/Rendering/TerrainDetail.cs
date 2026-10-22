using System;
using MapMagic.Core;
using MapMagic.Terrains;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// EN4 (research C §2 Tier 2.7, §3). The MapMagic object's terrainSettings (scene values pixelError 1, baseMapDist 1000,
    /// detailDistance 100, detailDensity 1: Sources data/hierarchy/scene1_game/MapMagic.txt:L2) are copied onto every terrain
    /// by TerrainSettings.ApplySettings (code/MapMagic/MapMagic.Terrains/TerrainSettings.cs: heightmapPixelError, basemapDistance
    /// = showBaseMap ? baseMapDist : int.MaxValue, detailObjectDistance, detailObjectDensity). We edit the shared settings (new
    /// tiles inherit them), push them to existing tiles with MapMagicObject.ApplyTerrainSettings() (no regeneration,
    /// MapMagicObject.cs:L195-209) and set the four terrain properties on every applied tile (PerformanceModule.TileAppliedEvent).
    /// No FSM touches terrainSettings; WW POI only adds graph generators (src/Modules/POI), no conflict.
    /// </summary>
    public sealed class TerrainDetail : Feature
    {
        public override string PlanId { get { return "EN4"; } }
        public override string Key { get { return "Terrain detail"; } }
        public override string Title { get { return "Terrain pixel error, base map and grass distance"; } }
        public override string Category { get { return Categories.Rendering; } }
        public override int Order { get { return 5; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Coarser far terrain mesh, earlier low-res base map, grass drawn less far and slightly thinner."; } }

        Knob _pixel, _baseMap, _detailDist, _density;
        public override void Bind(ModuleConfig cfg)
        {
            _pixel = NewKnob(cfg, "Pixel error", "Terrain heightmap pixel error.", 8, 5, 3, 1);
            _baseMap = NewKnob(cfg, "Base map distance", "Distance (m) where the terrain switches to the base map.", 350, 600, 1000, 1000);
            _detailDist = NewKnob(cfg, "Grass distance", "Detail (grass) draw distance (m).", 50, 70, 100, 100);
            _density = NewKnob(cfg, "Grass density", "Detail density 0..1.", 0.6f, 0.8f, 1f, 1f);
        }

        MapMagicObject _mm;
        int _oPixel, _oBase; float _oDist, _oDensity;
        bool _applied;
        public static long Tiles;

        protected override void OnEnable(FeatureMode mode)
        {
            PerformanceModule.TileAppliedEvent -= OnTile;
            PerformanceModule.TileAppliedEvent += OnTile;
            Find();
            Apply(true);
        }

        protected override void OnDisable()
        {
            PerformanceModule.TileAppliedEvent -= OnTile;
            if (_applied && _mm != null)
            {
                var s = _mm.terrainSettings;
                s.pixelError = _oPixel; s.baseMapDist = _oBase; s.detailDistance = _oDist; s.detailDensity = _oDensity;
                try { _mm.ApplyTerrainSettings(); } catch (Exception e) { Log.Warn("[Performance] EN4 restore: " + e.Message); }
            }
            _applied = false; _mm = null;
        }

        public override void OnSettingsChanged() { Apply(true); }
        public override void OnSceneLoaded() { _applied = false; _mm = null; Find(); Apply(false); }
        public override void OnWorldReady() { if (_mm == null) Find(); Apply(true); }

        void Find()
        {
            if (_mm != null) return;
            var all = Resources.FindObjectsOfTypeAll<MapMagicObject>();
            for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].gameObject.scene.IsValid()) { Use(all[i]); break; }
        }

        void Use(MapMagicObject mm)
        {
            if (ReferenceEquals(_mm, mm)) return;
            _mm = mm; _applied = false;
        }

        void Apply(bool pushToTiles)
        {
            if (_mm == null) return;
            var s = _mm.terrainSettings;
            if (s == null) return;
            if (!_applied) { _oPixel = s.pixelError; _oBase = s.baseMapDist; _oDist = s.detailDistance; _oDensity = s.detailDensity; _applied = true; }
            s.pixelError = Mathf.Max(1, _pixel.Int);
            s.baseMapDist = Mathf.Max(0, _baseMap.Int);
            s.detailDistance = _detailDist.Value;
            s.detailDensity = Mathf.Clamp01(_density.Value);
            if (pushToTiles) { try { _mm.ApplyTerrainSettings(); } catch (Exception e) { Log.Warn("[Performance] EN4 apply: " + e.Message); } }
        }

        void OnTile(TerrainTile tile)
        {
            if (tile == null) return;
            if (_mm == null && tile.mapMagic != null) { Use(tile.mapMagic); Apply(false); }
            if (!_applied || _mm == null) return;
            var s = _mm.terrainSettings;
            Tiles++;
            if (tile.main != null) Set(tile.main.terrain, s);
            if (tile.draft != null) Set(tile.draft.terrain, s);
        }

        static void Set(Terrain t, TerrainSettings s)
        {
            if (t == null) return;
            t.heightmapPixelError = s.pixelError;
            t.basemapDistance = s.showBaseMap ? s.baseMapDist : int.MaxValue;
            t.detailObjectDistance = s.detailDistance;
            t.detailObjectDensity = s.detailDensity;
        }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("MapMagic", _mm == null ? "not found" : "pixel error " + _mm.terrainSettings.pixelError + ", base map " + _mm.terrainSettings.baseMapDist + " m, grass " + _mm.terrainSettings.detailDistance.ToString("0") + " m x" + _mm.terrainSettings.detailDensity.ToString("0.0") + ", tiles " + Tiles);
        }
    }
}
