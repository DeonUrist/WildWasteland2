using System;
using WildWasteland.Core;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>
    /// Log facade of the Gunplay parts (README 5.1): every line goes through Core <see cref="Log"/> with the module as owner, so the
    /// "[Gunplay] " prefix is added by the facade. Callers guard string building with <c>Log.L1</c> / <c>Log.L2</c>.
    /// </summary>
    internal static class GunplayLog
    {
        /// <summary>Level-1 line (start/stop, state changes, settings-relevant decisions).</summary>
        internal static void Main(string msg) { Log.Main(GunplayModule.Instance, msg); }
        /// <summary>Level-2 line (per shot / per hit / per decision traces).</summary>
        internal static void Deep(string msg) { Log.Deep(GunplayModule.Instance, msg); }
        /// <summary>Warning (always, rate-limited).</summary>
        internal static void Warn(string msg) { Log.Warn(GunplayModule.Instance, msg); }
        /// <summary>Error with exception (always, rate-limited).</summary>
        internal static void Error(string msg, Exception e) { Log.Error(GunplayModule.Instance, msg, e); }
    }
}
