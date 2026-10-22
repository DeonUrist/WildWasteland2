using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Immersive Map coexistence (Wave A split, B17): while the standalone Immersive Map plugin is loaded the WW fog of war stands down until restart.</summary>
    internal sealed class ExternalMapPolicy : IExternalMapPolicy
    {
        private bool _warned;
        private const string SourceGuid = "local.immersivemap";

        /// <summary>True (and warns once) while the standalone Immersive Map plugin is loaded: the WW fog stands down until restart.</summary>
        public bool FogOwned
        {
            get
        {
            PluginInfo info;
            bool loaded = Chainloader.PluginInfos.TryGetValue(SourceGuid, out info) && !ReferenceEquals(info.Instance, null);
            if (loaded && !_warned) { _warned=true;Log.Warn(CompatibilityModule.Instance, "Immersive Map: " + ReplacementGuard.Status(SourceGuid) + ". Wild Wasteland fog waits until restart."); }
            return loaded;
        }
        }
    }
}
