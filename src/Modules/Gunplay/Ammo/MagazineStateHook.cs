using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// (WW) Handlers of Core.Hooks.EnterState (prefix + postfix, never skip).
    internal static class MagazineStateHook
    {
        internal static bool Prefix(Fsm fsm, FsmState state)
        {
            if (state != null && fsm != null) try { PerGunAmmo.BeforeState(fsm, state); } catch (Exception e) { PerGunAmmo.Fail(e); }
            return true;
        }
        internal static void Postfix(Fsm fsm, FsmState state)
        {
            if (state != null && fsm != null) try { PerGunAmmo.AfterState(fsm, state); } catch (Exception e) { PerGunAmmo.Fail(e); }
        }
    }
}
