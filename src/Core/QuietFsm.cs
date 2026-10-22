using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WildWasteland.Core
{
    // Apocapatrol 2.1.4 (moved from Patrols to Core so it works without the Patrols module): the game's own FSMs ask every object
    // the player looks at or touches for an "ID" FSM (GetFsmString ID on {Item}: car frame "collider" children, switch_lights, a car
    // root...). PlayMaker's ActionHelpers.GetGameObjectFsm prints a Unity warning "Could not find FSM: ID on GameObject: collider"
    // each time it is missing - hundreds per minute in the BepInEx console.
    // This prefix does the same lookup (first PlayMakerFSM with that name, else the first PlayMakerFSM like vanilla) without the warning. Off: the vanilla method runs.
    internal static class QuietFsm
    {
        private static readonly List<PlayMakerFSM> _buf = new List<PlayMakerFSM>();
        private static int _hidden;

        // On is a static bool kept in sync with the setting "Hide missing-FSM warnings" (tools.cfg, Patrols / Debug; bound by Patrols.BindConfig).
        internal static bool On;
        internal static void Install()
        {
            Patches.Patch(null, AccessTools.PropertyGetter(typeof(PlayMakerPrefs), "LogPerformanceWarnings"),
                postfix: AccessTools.Method(typeof(QuietFsm), nameof(PerformanceWarnings)));
            var target = AccessTools.Method(typeof(HutongGames.PlayMaker.ActionHelpers), "GetGameObjectFsm", new[] { typeof(GameObject), typeof(string) });
            if (target == null) { Log.Warn("[Core] QuietFsm: ActionHelpers.GetGameObjectFsm not found; missing-FSM warnings stay"); return; }
            Patches.Patch(null, target, prefix: AccessTools.Method(typeof(QuietFsm), nameof(Prefix)));
        }

        private static void PerformanceWarnings(ref bool __result) { __result = __result && Log.Verbose; }

        private static bool Prefix(GameObject __0, string __1, ref PlayMakerFSM __result)
        {
            if (!On) return true;
            if (__0 == null || string.IsNullOrEmpty(__1)) return true;   // the vanilla paths for these
            try
            {
                __0.GetComponents(_buf);
                for (int i = 0; i < _buf.Count; i++)
                    if (_buf[i] != null && _buf[i].FsmName == __1) { __result = _buf[i]; _buf.Clear(); return false; }
                // vanilla falls back to go.GetComponent<PlayMakerFSM>() (the first FSM, or null) after the warning
                __result = _buf.Count > 0 ? _buf[0] : null;
                _buf.Clear();
                if (++_hidden == 1 && Log.Verbose) Log.Info("[Core] QuietFsm: hiding \"Could not find FSM\" warnings (first: " + __1 + " on " + __0.name + ")");
                return false;
            }
            catch (Exception) { _buf.Clear(); return true; }
        }
    }
}
