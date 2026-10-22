using System.Collections.Concurrent;
using MapMagic.Products;
using MapMagic.Terrains;

namespace WildWasteland.Modules.POI
{
    /// <summary>Captures the start-chunk policy on the main thread for terrain workers.</summary>
    internal static class TownPoiSettings
    {
        private static TownSettings _settings;
        private static readonly ConcurrentDictionary<TileData, bool> SpawnPolicy = new ConcurrentDictionary<TileData, bool>();
        /// <summary>Uses the POI-owned generation settings.</summary>
        internal static void Configure(TownSettings settings) { _settings = settings; }
        /// <summary>Snapshots whether custom POIs are allowed in this tile.</summary>
        internal static void Capture(TerrainTile tile, TileData data)
        { SpawnPolicy[data] = tile.coord.x != 0 || tile.coord.z != 0 || _settings.PoiNearSpawn.Value; }
        /// <summary>Reads only captured worker data; an uncaptured first chunk stays excluded.</summary>
        internal static bool Allows(TileData data)
        {
            bool allowed;
            if (SpawnPolicy.TryGetValue(data, out allowed)) return allowed;
            var area = data.area.active;
            return UnityEngine.Mathf.RoundToInt(area.worldPos.x / area.worldSize.x) != 0
                || UnityEngine.Mathf.RoundToInt(area.worldPos.z / area.worldSize.z) != 0;
        }
        /// <summary>Releases a completed tile snapshot.</summary>
        internal static void Forget(TileData data) { bool ignored; SpawnPolicy.TryRemove(data, out ignored); }
        /// <summary>Releases world snapshots.</summary>
        internal static void Clear() { SpawnPolicy.Clear(); }
    }
}
