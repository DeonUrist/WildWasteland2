using System;
using HarmonyLib;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// <summary>Harmony patch class: FloatClamp.DoClamp prefix (armor overflow).</summary>
    [HarmonyPatch(typeof(FloatClamp), "DoClamp")]
    internal static class ArmorCapacityPatch
    {
        /// <summary>Harmony prefix on FloatClamp.DoClamp (every FloatClamp each frame): the armor clamp goes through ArmorOverflow; false skips the vanilla clamp.</summary>
        static bool Prefix(FloatClamp __instance) { return ArmorOverflow.BeforeClamp(__instance); }
    }
}
