using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    /// <summary>Immersive Map coexistence (Wave A split, B17): while the standalone Immersive Map plugin is loaded the WW fog of war stands down until restart.</summary>
    internal static partial class MapFog
    {
        private const string SourceGuid = "local.immersivemap";

        /// <summary>True (and warns once) while the standalone Immersive Map plugin is loaded: the WW fog stands down until restart.</summary>
        private static bool ImmersiveMapLoaded()
        {
            PluginInfo info;
            bool loaded = Chainloader.PluginInfos.TryGetValue(SourceGuid, out info) && !ReferenceEquals(info.Instance, null);
            if (loaded) Log.Warn(Compatibility.CompatibilityModule.Instance, "Immersive Map: " + ReplacementGuard.Status(SourceGuid) + ". Wild Wasteland fog waits until restart.");
            return loaded;
        }
    }
}
