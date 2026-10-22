using System;
using HarmonyLib;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>Harmony patch class: IntClamp.DoClamp prefix (backpack ammo overflow).</summary>
    [HarmonyPatch(typeof(IntClamp), "DoClamp")]
    internal static class AmmoCapacityPatch
    {
        /// <summary>Harmony prefix on IntClamp.DoClamp (every IntClamp of every FSM each frame): the backpack ammo clamps go through BackpackOverflow; false skips the vanilla clamp.</summary>
        static bool Prefix(IntClamp __instance) { return BackpackOverflow.BeforeClamp(__instance); }
    }
}
