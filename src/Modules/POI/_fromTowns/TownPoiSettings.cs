using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections.Concurrent;
using Den.Tools;
using MapMagic.Products;
using MapMagic.Terrains;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Towns
{
    internal static class TownPoiSettings
    {
        private static TownSettings _settings;
        private static ModuleHost.Entry _poi;
        private static ConfigEntry<int> _warRig;
        private static readonly ConcurrentDictionary<TileData, bool> SpawnPolicy = new ConcurrentDictionary<TileData, bool>();
        internal static void Install(TownsModule owner, TownSettings settings)
        {
            _settings = settings;
            foreach (var entry in ModuleHost.Entries) if (entry.Module.Id == "POI") _poi = entry;
            if (_poi == null) throw new InvalidOperationException("Native POI settings are unavailable.");
            _warRig = (ConfigEntry<int>)_poi.Config.Main[new ConfigDefinition("POI / World", "Spawn chance (%)")];
            var pages = typeof(ToolWindow).Assembly.GetType("WildWasteland.Core.UI.CorePages", true);
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            if (!Patches.Patch(owner, Patches.Method(pages, "DrawWorld"), postfix: typeof(TownPoiSettings).GetMethod("Draw", flags))
                || !Patches.Patch(owner, Patches.Method(_poi.Module.GetType(), "ToolPages"), postfix: typeof(TownPoiSettings).GetMethod("Pages", flags))
                || !Patches.Patch(owner, Patches.Method(pages, "ResetSettings"), postfix: typeof(TownPoiSettings).GetMethod("ResetTown", flags)))
                throw new InvalidOperationException("Shared POI chance controls could not be registered.");
            var generator = typeof(Services).Assembly.GetType("WildWasteland.Modules.POI.PoiLocations", true);
            if (!Patches.Patch(owner, Patches.Method(generator, "Generate"), prefix: Patches.Method(typeof(TownPoiSettings), "AllowPoi")))
                throw new InvalidOperationException("Shared starting-chunk POI policy could not be registered");
        }
        internal static void Capture(TerrainTile tile, TileData data)
        { SpawnPolicy[data] = tile.coord.x != 0 || tile.coord.z != 0 || _settings.PoiNearSpawn.Value; }
        internal static void Forget(TileData data) { bool ignored; SpawnPolicy.TryRemove(data, out ignored); }
        internal static void Clear() { SpawnPolicy.Clear(); }
        private static bool AllowPoi(object __instance, TileData data, StopToken stop)
        {
            bool allowed;
            if (!SpawnPolicy.TryGetValue(data, out allowed))
                allowed = Mathf.RoundToInt(data.area.active.worldPos.x / data.area.active.worldSize.x) != 0
                    || Mathf.RoundToInt(data.area.active.worldPos.z / data.area.active.worldSize.z) != 0;
            if (allowed) return true;
            data.StoreProduct((MapMagic.Nodes.IOutlet<TransitionsList>)__instance, new TransitionsList()); return false;
        }
        private static void Pages(ref IEnumerable<ToolPage> __result)
        { __result = new ToolPage[] { new ActionPage(_poi.Module, ToolGroups.Vehicles, "Points of interest", Draw) }; }
        private static void Draw(ToolLayout l)
        {
            l.Heading("POI");
            l.Entry(_settings.PoiNearSpawn);
            Change(l, "War Rig chance (%)", _warRig);
            Change(l, "Towns chance (%)", _settings.Chance);
            l.Entry(_settings.EnsureTown);
            l.Text("Independent chance per generated 5 km block. Defaults: War Rig 2%, Towns 50%. New POI near spawn permits both in the first block around camp (off by default). Ensure selected towns retries placement and uses the flattest available site when terrain targets cannot be met. Existing towns keep their layout; blockers and real road connections are required.");
            if (l.Button("Reset POI chances (2% / 50%)")) { _warRig.Value = 2; _settings.Chance.Value = 50; }
        }
        private static void Change(ToolLayout l, string label, ConfigEntry<int> entry)
        {
            int value = Mathf.RoundToInt(l.Slider(label, "POI/" + label, entry.Value, 0, 100));
            if (value != entry.Value) entry.Value = value;
        }
        private static void ResetTown()
        { _settings.Chance.BoxedValue = _settings.Chance.DefaultValue; _settings.EnsureTown.BoxedValue = _settings.EnsureTown.DefaultValue; _settings.PoiNearSpawn.Value = false; }
    }
}
