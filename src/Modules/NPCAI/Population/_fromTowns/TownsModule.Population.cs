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
    /// Lifecycle of the interior / native-POI population rules. Wave A (A6): entry split of Towns/TownsModule.cs (NPCAI/Population part, B07).
    /// </summary>
    public sealed partial class TownsModule
    {
        /// <summary>Installs interior and native-POI population hooks and jobs (Hooks.ProcessEvent/FsmEnable/CreateObjectEnter).</summary>
        partial void InitPopulation() { InteriorPopulation.Install(this); }
        /// <summary>Lets deferred native spawn events run before the module stops (no spawner left waiting).</summary>
        partial void ShutdownPopulation() { InteriorPopulation.ReleaseDeferred(); }
    }
}
