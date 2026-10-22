using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// Runtime state of the NPCAI module shared by its static subsystems (namespace kept until Wave C): the running flag every hook and
    /// job checks first, the module instance used as log/hook owner and the config data folder (config/WildWasteland/NPCAI, D13).
    /// Set by NPCAIModule.Init, cleared by Shutdown.
    /// </summary>
    internal static class NpcaiRuntime
    {
        /// <summary>The module runs (Init done, Shutdown not yet). Every hook / patch handler and every On property checks it first.</summary>
        internal static bool Running;

        /// <summary>The running NPCAI module (owner of its jobs, hooks, events and log lines); null while off.</summary>
        internal static IModule Module;

        /// <summary>config/WildWasteland/NPCAI: senses sidecars (Saves/), nest layouts, custom nav maps.</summary>
        internal static string DataDir;

        /// <summary>Level-2 trace line owned by NPCAI (callers on hot paths check Log.L2 first).</summary>
        internal static void Deep(string message) { if (Log.L2) Log.Deep(Module, message); }

        /// <summary>Level-1 line owned by NPCAI (main actions).</summary>
        internal static void Main(string message) { if (Log.L1) Log.Main(Module, message); }

        /// <summary>Warning owned by NPCAI (always written, rate-limited by Core).</summary>
        internal static void Warn(string message) { Log.Warn(Module, message); }

        /// <summary>Error owned by NPCAI (always written, rate-limited by Core).</summary>
        internal static void Error(string message, System.Exception e = null) { Log.Error(Module, message, e); }

        static readonly System.Collections.Generic.HashSet<string> _once = new System.Collections.Generic.HashSet<string>();
        /// <summary>Error logged once per key for the session (ex Quests Plugin.LogOnce in the follower code).</summary>
        internal static void ErrorOnce(string key, System.Exception e) { if (_once.Add(key)) Log.Error(Module, key + ": " + e); }
    }
}
