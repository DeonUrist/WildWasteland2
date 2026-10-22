using System;
using System.Collections.Generic;
using System.Globalization;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.NPC
{
    /// <summary>NpcPages part owned by Tools: the pathfinding baked-data tools of the Debug page and the "POI pathfinding" page.</summary>
    internal static partial class NpcPages
    {
        /// <summary>"Pathfinding baked data": unbaked-POI map highlight, reset custom maps, export vanilla maps, last action status.</summary>
        static partial void DrawToolsMapData(ToolLayout l)
        {
            l.Space(); l.Heading("Pathfinding baked data");
            l.Toggle(NpcModule.HighlightUnbakedMapPois, "Highlight unbaked POIs on map (light purple)");
            if (l.Button("Reset custom pathfinding baked data", true, false, 430)) Nav.ResetCustomMaps();
            if (l.Button("Export vanilla pathfinding maps", true, false, 430)) Nav.ExportVanillaMaps();
            l.Text("Reset clears locally baked maps and rebuilds nearby custom maps. Shipped vanilla maps and vanilla exports are retained. Export collects release candidates from a clean vanilla setup.");
            if (!string.IsNullOrEmpty(Nav.MapActionStatus)) l.Text(Nav.MapActionStatus);
        }

        /// <summary>Adds the developer "POI pathfinding" page (setup, bake, editor) to the Debug group.</summary>
        static partial void AddToolsPages(NpcModule m, List<ToolPage> pages)
        {
            pages.Add(new ActionPage(m, ToolGroups.Debug, "POI pathfinding", PoiNavEditor.DrawPage) { DebugOnly = true, IsTool = true });
        }
    }
}
