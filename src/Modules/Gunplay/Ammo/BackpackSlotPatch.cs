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
    /// <summary>Harmony patch class: FsmState.OnEnter postfix (backpack slot emptied).</summary>
    [HarmonyPatch(typeof(FsmState), "OnEnter")]
    internal static class BackpackSlotPatch
    {
        /// <summary>Harmony postfix on FsmState.OnEnter (every state entry of every FSM): forwards to BackpackOverflow.OnSlotEnter (first filter: FSM name length).</summary>
        static void Postfix(FsmState __instance) { BackpackOverflow.OnSlotEnter(__instance); }
    }
}
