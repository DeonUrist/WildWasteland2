using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// (WW) Handler of Core.Hooks.ActivateGameObjectEnter (prefix, never skips).
    internal static class MagazineDrawHook
    {
        /// <summary>Core.Hooks.ActivateGameObjectEnter prefix (every ActivateGameObject.OnEnter): a WeaponInHand activation binds the drawn model to its item's magazine. Never skips.</summary>
        internal static bool Prefix(ActivateGameObject __instance)
        {
            var fsm = __instance.Fsm;
            if (fsm == null) return true;
            string n = fsm.Name;
            if (n == null || n.Length != 12 || n != "WeaponInHand" || !__instance.activate.Value) return true;
            try { PerGunAmmo.Draw(fsm, fsm.GetOwnerDefaultTarget(__instance.gameObject)); }
            catch (Exception e) { PerGunAmmo.Fail(e); }
            return true;
        }
    }
}
