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
    [HarmonyPatch(typeof(FloatClamp), "DoClamp")]
    internal static class ArmorCapacityPatch
    {
        static bool Prefix(FloatClamp __instance) { return ArmorOverflow.BeforeClamp(__instance); }
    }
}
