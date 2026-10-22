using System;
using System.Collections.Generic;
using System.Globalization;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.NPC
{
    /// <summary>NpcPages part owned by Presentation: the female appearance / voice settings page.</summary>
    internal static partial class NpcPages
    {
        /// <summary>Adds "Female appearance & voices" (Women of Wasteland models, textures, accessories, voices) after "NPC population".</summary>
        static partial void AddPresentationPages(NpcModule m, List<ToolPage> pages)
        {
            pages.Add(new ConfigPage(m, ToolGroups.Npc, "Female appearance & voices", m.Config.Main, Sec("Gungirl"), Sec("Shoota"))
            { Intro = "Women of Wasteland models, textures, accessories and voice settings. Paths are relative to plugins/WildWasteland/NPC. Model, texture and voice changes require a restart." });
        }
    }
}
