using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// The game's Reload FSMs poll GetButtonDown("Reload"). We take over that button: it no longer fires on press;
    /// instead we fire the action's event on a short tap (released before HoldSeconds).
    /// (WW) Handler of Core.Hooks.GetButtonDownUpdate (prefix); same semantics: the original is skipped for every Reload poll.
    internal static class ReloadTapHook
    {
        private static readonly Dictionary<GetButtonDown, int> _lastTap = new Dictionary<GetButtonDown, int>();
        internal static void Reset() { _lastTap.Clear(); }

        internal static bool Prefix(GetButtonDown __instance)
        {
            if (!AmmoSettings.Enabled.Value) return true;
            if (__instance.buttonName == null || __instance.buttonName.Value != "Reload") return true;
            if (AmmoSettings.TapPending())
            {
                int last;
                if (!_lastTap.TryGetValue(__instance, out last) || last != AmmoSettings.TapFrame)
                {
                    _lastTap[__instance] = AmmoSettings.TapFrame;
                    if (__instance.storeResult != null) __instance.storeResult.Value = true;
                    if (__instance.Fsm != null && __instance.sendEvent != null) __instance.Fsm.Event(__instance.sendEvent);
                    return false;
                }
            }
            if (__instance.storeResult != null) __instance.storeResult.Value = false;
            return false;
        }
    }
}
