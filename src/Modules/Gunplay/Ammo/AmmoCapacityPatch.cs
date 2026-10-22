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
    [HarmonyPatch(typeof(IntClamp), "DoClamp")]
    internal static class AmmoCapacityPatch
    {
        static bool Prefix(IntClamp __instance) { return BackpackOverflow.BeforeClamp(__instance); }
    }
}
