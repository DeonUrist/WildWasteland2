using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Logging;

namespace WildWasteland.Core
{
    /// <summary>
    /// One log for the whole mod (README 5.1). <see cref="Level"/> 0 = off, 1 = main actions, 2 = deep traces; set from
    /// [Debug] VerboseLevel (bound by Core until the Debug module takes it over). Callers on hot paths check <see cref="L1"/> /
    /// <see cref="L2"/> first or use the generic overloads, which format only when the level is on. Every line is prefixed
    /// "[ModuleId] " by the facade. Warnings and errors are always written and rate-limited per message (reset on scene load).
    /// Errors no longer touch saves: save participants report failures through <see cref="SaveBus.ReportFailure(IModule,string)"/>.
    /// </summary>
    public static class Log
    {
        /// <summary>BepInEx sink (the plugin's logger), set in Plugin.Awake.</summary>
        public static ManualLogSource Source;

        /// <summary>0 = off, 1 = main actions, 2 = deep. Set by Core from [Debug] VerboseLevel (Debug module in Wave B).</summary>
        public static int Level;

        /// <summary>True when level-1 (main action) lines are written.</summary>
        public static bool L1 { get { return Level >= 1; } }

        /// <summary>True when level-2 (deep trace) lines are written.</summary>
        public static bool L2 { get { return Level >= 2; } }

        /// <summary>Old global verbose switch: true while <see cref="Level"/> is 1 or higher. Setting true raises the level to 1, false sets 0.</summary>
        [Obsolete("refactor: use Log.L1 / Log.Level")]
        public static bool Verbose
        {
            get { return Level >= 1; }
            set { Level = value ? Math.Max(Level, 1) : 0; }
        }

        static readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        static readonly Dictionary<IModule, string> _prefixes = new Dictionary<IModule, string>();
        const int RepeatLimit = 5;

        // ------------------------------------------------------------------ level 1

        /// <summary>Level-1 line (module start/stop, state changes, group spawns, saves done...), prefixed "[ModuleId] ".</summary>
        public static void Main(IModule owner, string msg) { if (Level >= 1) Write(LogLevel.Info, Prefix(owner) + msg); }

        /// <summary>Level-1 line formatted with <see cref="CultureInfo.InvariantCulture"/> only when level 1 is on (no string work otherwise).</summary>
        public static void Main<A>(IModule owner, string format, A a) { if (Level >= 1) Write(LogLevel.Info, Prefix(owner) + string.Format(CultureInfo.InvariantCulture, format, a)); }

        /// <summary>Two-argument form of <see cref="Main{A}"/>.</summary>
        public static void Main<A, B>(IModule owner, string format, A a, B b) { if (Level >= 1) Write(LogLevel.Info, Prefix(owner) + string.Format(CultureInfo.InvariantCulture, format, a, b)); }

        /// <summary>Three-argument form of <see cref="Main{A}"/>.</summary>
        public static void Main<A, B, C>(IModule owner, string format, A a, B b, C c) { if (Level >= 1) Write(LogLevel.Info, Prefix(owner) + string.Format(CultureInfo.InvariantCulture, format, a, b, c)); }

        // ------------------------------------------------------------------ level 2

        /// <summary>Level-2 line (per-decision traces, per-frame diagnostics), prefixed "[ModuleId] ".</summary>
        public static void Deep(IModule owner, string msg) { if (Level >= 2) Write(LogLevel.Info, Prefix(owner) + msg); }

        /// <summary>Level-2 line formatted only when level 2 is on.</summary>
        public static void Deep<A>(IModule owner, string format, A a) { if (Level >= 2) Write(LogLevel.Info, Prefix(owner) + string.Format(CultureInfo.InvariantCulture, format, a)); }

        /// <summary>Two-argument form of <see cref="Deep{A}"/>.</summary>
        public static void Deep<A, B>(IModule owner, string format, A a, B b) { if (Level >= 2) Write(LogLevel.Info, Prefix(owner) + string.Format(CultureInfo.InvariantCulture, format, a, b)); }

        /// <summary>Three-argument form of <see cref="Deep{A}"/>.</summary>
        public static void Deep<A, B, C>(IModule owner, string format, A a, B b, C c) { if (Level >= 2) Write(LogLevel.Info, Prefix(owner) + string.Format(CultureInfo.InvariantCulture, format, a, b, c)); }

        // ------------------------------------------------------------------ always

        /// <summary>Always written (startup summary, module state changes): prefixed "[ModuleId] ".</summary>
        public static void Always(IModule owner, string msg) { Write(LogLevel.Info, Prefix(owner) + msg); }

        /// <summary>Always written, rate-limited to 5 repeats per message until the next scene load.</summary>
        public static void Warn(IModule owner, string msg) { var line = Prefix(owner) + msg; if (Allow(line)) Write(LogLevel.Warning, line); }

        /// <summary>Always written (with the exception when given), rate-limited to 5 repeats per message. Has no side effect on saves.</summary>
        public static void Error(IModule owner, string msg, Exception e = null)
        {
            var line = Prefix(owner) + msg;
            if (Allow(line)) Write(LogLevel.Error, e != null ? line + ": " + e : line);
        }

        // ------------------------------------------------------------------ old API (callers prefix "[Id] " themselves)

        /// <summary>Old level-1 info line; the caller already prefixed it.</summary>
        [Obsolete("refactor: use Log.Main(owner, ...) behind Log.L1")]
        public static void Info(string msg) { if (Level >= 1) Write(LogLevel.Info, msg); }

        /// <summary>Old always-written line; the caller already prefixed it.</summary>
        [Obsolete("refactor: use Log.Always(owner, msg)")]
        public static void Always(string msg) { Write(LogLevel.Info, msg); }

        /// <summary>Old rate-limited warning; the caller already prefixed it.</summary>
        [Obsolete("refactor: use Log.Warn(owner, msg)")]
        public static void Warn(string msg) { if (Allow(msg)) Write(LogLevel.Warning, msg); }

        /// <summary>Old rate-limited error; the caller already prefixed it. No longer fails a running save (README 9).</summary>
        [Obsolete("refactor: use Log.Error(owner, msg)")]
        public static void Error(string msg) { if (Allow(msg)) Write(LogLevel.Error, msg); }

        /// <summary>Old rate-limited error with exception; the caller already prefixed it. No longer fails a running save (README 9).</summary>
        [Obsolete("refactor: use Log.Error(owner, msg, e)")]
        public static void Error(string msg, Exception e) { if (Allow(msg)) Write(LogLevel.Error, msg + ": " + e); }

        // ------------------------------------------------------------------ internals

        /// <summary>Writes one line to the BepInEx sink when it exists.</summary>
        static void Write(LogLevel level, string line) { var s = Source; if (s != null) s.Log(level, line); }

        /// <summary>"[Id] " for a module, "[Core] " for null; cached per module so prefixes are built once.</summary>
        static string Prefix(IModule owner)
        {
            if (owner == null) return "[Core] ";
            string p;
            if (!_prefixes.TryGetValue(owner, out p)) { p = "[" + owner.Id + "] "; _prefixes[owner] = p; }
            return p;
        }

        /// <summary>Repeat limiter: true for the first 5 occurrences of a message; logs one suppression note at the limit.</summary>
        static bool Allow(string msg)
        {
            int n;
            _counts.TryGetValue(msg, out n);
            if (n >= RepeatLimit) return false;
            _counts[msg] = n + 1;
            if (n + 1 == RepeatLimit) Write(LogLevel.Warning, "(further repeats of the previous message are suppressed)");
            return true;
        }

        /// <summary>Called on scene load so a message may appear again in a new session.</summary>
        public static void ResetRateLimits() { _counts.Clear(); }
    }
}
