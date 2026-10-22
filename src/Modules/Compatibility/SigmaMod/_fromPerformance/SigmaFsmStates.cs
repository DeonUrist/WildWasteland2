using BepInEx.Bootstrap;
using HarmonyLib;
using HutongGames.PlayMaker;
using WildWasteland.Core;

namespace WildWasteland.Modules.Performance
{
    internal static class SigmaFsmStates
    {
        static bool _reported;

        internal static void Install(PerformanceModule module)
        {
            _reported = false;
            if (!module.EnsurePatched(AccessTools.PropertyGetter(typeof(PlayMakerFSM), "FsmStates"), null,
                AccessTools.Method(typeof(SigmaFsmStates), "Post"), null, null)) return;
            Log.Always("[Performance] SigmaMod FSM guard installed (active while SigmaMod is loaded)");
        }

        static void Post(PlayMakerFSM __instance, FsmState[] __result)
        {
            if (__result == null || !Chainloader.PluginInfos.ContainsKey("com.burner.apocalypter.sigmamod")) return;
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Initialized) return;
            // SigmaMod scans inactive FSMs before Awake. Bind their states through PlayMaker's own API;
            // leave InitData, action loading and lifecycle callbacks to their existing callers.
            fsm.InitStates();
            if (_reported) return;
            _reported = true;
            Log.Always("[Performance] SigmaMod FSM guard: prepared state owners before early action reads");
        }
    }
}
