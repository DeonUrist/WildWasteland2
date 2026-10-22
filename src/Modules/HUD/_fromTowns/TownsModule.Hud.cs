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
    /// Lifecycle of the town map labels. Wave A (A6): entry split of Towns/TownRuntime.cs (HUD part, B10).
    /// </summary>
    internal sealed partial class TownRuntime
    {
        /// <summary>Installs the town map label/icon patches on CleanHUD (LocationMarkers.Observe, QuestMap.Tick).</summary>
        partial void InitHud() { TownMarkers.Install(this); }
    }
}
