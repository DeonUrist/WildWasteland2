using System;
using MapMagic.Core;
using MapMagic.Terrains;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// EN4 (research C §2 Tier 2.7, §3). The MapMagic object's terrainSettings (scene values pixelError 1, baseMapDist 1000,
    /// detailDistance 100, detailDensity 1: Sources data/hierarchy/scene1_game/MapMagic.txt:L2) are copied onto every terrain
    /// by TerrainSettings.ApplySettings (code/MapMagic/MapMagic.Terrains/TerrainSettings.cs: heightmapPixelError, basemapDistance
    /// = showBaseMap ? baseMapDist : int.MaxValue, detailObjectDistance, detailObjectDensity). We edit the shared settings (new
    /// tiles inherit them), push them to existing tiles with MapMagicObject.ApplyTerrainSettings() (no regeneration,
    /// MapMagicObject.cs:L195-209) and set the four terrain properties on every applied tile (OptimizationRuntime.TileAppliedEvent).
    /// No FSM touches terrainSettings; WW POI only adds graph generators (src/Modules/POI), no conflict.
    /// </summary>
    public sealed class TerrainDetail : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "EN4"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Terrain detail"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Terrain pixel error, base map and grass distance"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.Rendering; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 5; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Coarser far terrain mesh, earlier low-res base map, grass drawn less far and slightly thinner."; } }

        Knob _pixel, _baseMap, _detailDist, _density;
        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
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

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            OptimizationRuntime.TileAppliedEvent -= OnTile;
            OptimizationRuntime.TileAppliedEvent += OnTile;
            Find();
            Apply(true);
        }

        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable()
        {
            OptimizationRuntime.TileAppliedEvent -= OnTile;
            if (_applied && _mm != null)
            {
                var s = _mm.terrainSettings;
                s.pixelError = _oPixel; s.baseMapDist = _oBase; s.detailDistance = _oDist; s.detailDensity = _oDensity;
                try { _mm.ApplyTerrainSettings(); } catch (Exception e) { Log.Warn(OptimizationRuntime.Instance, "EN4 restore: " + e.Message); }
            }
            _applied = false; _mm = null;
        }

        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged() { Apply(true); }
        /// <summary>Drops scene-bound references and refreshes this feature for the newly loaded scene.</summary>
        public override void OnSceneLoaded() { _applied = false; _mm = null; Find(); Apply(false); }
        /// <summary>Refreshes this feature after native gameplay objects have finished loading.</summary>
        public override void OnWorldReady() { if (_mm == null) Find(); Apply(true); }

        /// <summary>Resolves the active MapMagic object during feature initialization/world refresh, not per gameplay frame.</summary>
        void Find()
        {
            if (_mm != null) return;
            var all = Resources.FindObjectsOfTypeAll<MapMagicObject>();
            for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].gameObject.scene.IsValid()) { Use(all[i]); break; }
        }

        /// <summary>Captures the selected MapMagic terrain settings before applying preset quality values.</summary>
        void Use(MapMagicObject mm)
        {
            if (ReferenceEquals(_mm, mm)) return;
            _mm = mm; _applied = false;
        }

        /// <summary>Applies terrain detail overrides and optionally propagates the updated settings to loaded tiles.</summary>
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
            if (pushToTiles) { try { _mm.ApplyTerrainSettings(); } catch (Exception e) { Log.Warn(OptimizationRuntime.Instance, "EN4 apply: " + e.Message); } }
        }

        /// <summary>Applied-tile callback: copies the selected detail settings to the tile's live terrain.</summary>
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

        /// <summary>Writes only the terrain detail properties owned by this feature.</summary>
        static void Set(Terrain t, TerrainSettings s)
        {
            if (t == null) return;
            t.heightmapPixelError = s.pixelError;
            t.basemapDistance = s.showBaseMap ? s.baseMapDist : int.MaxValue;
            t.detailObjectDistance = s.detailDistance;
            t.detailObjectDensity = s.detailDensity;
        }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l)
        {
            l.Value("MapMagic", _mm == null ? "not found" : "pixel error " + _mm.terrainSettings.pixelError + ", base map " + _mm.terrainSettings.baseMapDist + " m, grass " + _mm.terrainSettings.detailDistance.ToString("0") + " m x" + _mm.terrainSettings.detailDensity.ToString("0.0") + ", tiles " + Tiles);
        }
    }
}
