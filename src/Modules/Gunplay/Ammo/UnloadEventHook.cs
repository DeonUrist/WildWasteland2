using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// (WW) Handler of Core.Hooks.ProcessEvent (observer, never skips).
    internal static class UnloadEventHook
    {
        internal static bool Prefix(Fsm fsm, FsmEvent fsmEvent, FsmEventData data)
        {
            if (!AmmoRunner.Unloader.Busy || fsmEvent == null) return true;
            AmmoRunner.Unloader.OnFsmEvent(fsm, fsmEvent.Name);
            return true;
        }
    }
}
