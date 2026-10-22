using BepInEx.Bootstrap;
using HarmonyLib;
using HutongGames.PlayMaker;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Binds native FSM state owners before SigmaMod's early inactive-object action scans.</summary>
    internal static class SigmaFsmStates
    {
        static bool _reported;
        static IModule _owner;

        /// <summary>Detects SigmaMod once per bridge activation and installs the owner-scoped guard if present.</summary>
        internal static void Install(IModule module)
        {
            _reported = false;
            _owner = null;
            if (!Chainloader.PluginInfos.ContainsKey("com.burner.apocalypter.sigmamod")) return;
            if (!Patches.Patch(module, AccessTools.PropertyGetter(typeof(PlayMakerFSM), "FsmStates"),
                postfix: AccessTools.Method(typeof(SigmaFsmStates), "Post"))) return;
            _owner = module;
            if (Log.L1) Log.Main(module, "SigmaMod FSM guard installed");
        }

        /// <summary>Clears retained owner/report state when Compatibility shuts down.</summary>
        internal static void Clear() { _owner = null; _reported = false; }

        /// <summary>Initializes only state ownership, preserving native action loading and lifecycle callbacks.</summary>
        static void Post(PlayMakerFSM __instance, FsmState[] __result)
        {
            if (__result == null || _owner == null) return;
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Initialized) return;
            // SigmaMod scans inactive FSMs before Awake. Bind their states through PlayMaker's own API;
            // leave InitData, action loading and lifecycle callbacks to their existing callers.
            fsm.InitStates();
            if (_reported) return;
            _reported = true;
            if (Log.L1) Log.Main(_owner, "SigmaMod FSM guard: prepared state owners before early action reads");
        }
    }
}
