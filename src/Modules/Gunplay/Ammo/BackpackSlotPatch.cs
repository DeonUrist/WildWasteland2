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
    [HarmonyPatch(typeof(FsmState), "OnEnter")]
    internal static class BackpackSlotPatch
    {
        static void Postfix(FsmState __instance) { BackpackOverflow.OnSlotEnter(__instance); }
    }
}
