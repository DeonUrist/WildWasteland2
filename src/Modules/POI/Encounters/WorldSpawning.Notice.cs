using System;
using System.Collections.Generic;
using ES3Internal;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    /// <summary>Natural War Rig map notice data (rig markers placed by the POI module, notice timer); POI part of WorldSpawning (Wave A split of Patrols WarRig/WorldSpawning.cs).</summary>
    internal static partial class WorldSpawning
    {
        internal static readonly List<GameObject> NaturalRigs = new List<GameObject>();
        internal static float NaturalNoticeUntil;
    }
}
