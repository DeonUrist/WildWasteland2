using System;
using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// NpcModule part owned by Debug (Wave A split): the NPCAI log adapter and helpers, the ShowNavigation overlay job and the debug /
    /// log switches of tools.cfg. Partial methods are called from the NPC entry where the moved lines were.
    /// </summary>
    public sealed partial class NpcModule
    {
        internal static ConfigEntry<bool> VerboseLog, NavDump, ShowNav, HighlightUnbakedMapPois, BurrowLog, DogLog, NestLog, PounceLog;
        internal static readonly LogAdapter Log = new LogAdapter();
        Job _overlay;

        /// <summary>Info line: always written when [NPC / Debug] VerboseLog is on, else through the global verbose switch.</summary>
        internal static void Verbose(string s) { if (VerboseLog != null && VerboseLog.Value) Core.Log.Always("[NPC] " + s); else Core.Log.Info("[NPC] " + s); }
        /// <summary>Warning line (always written, rate-limited by Core).</summary>
        internal static void Warn(string s) { Core.Log.Warn("[NPC] " + s); }

        /// <summary>Plugin.Log of NPCAI: info lines are written when the module's own VerboseLog or the global one is on.</summary>
        internal sealed class LogAdapter
        {
            /// <summary>Info line (module VerboseLog: always; else only with the global verbose switch).</summary>
            public void LogInfo(string s) { if (VerboseLog != null && VerboseLog.Value) Core.Log.Always("[NPC] " + s); else if (Core.Log.Verbose) Core.Log.Info("[NPC] " + s); }
            /// <summary>Warning line.</summary>
            public void LogWarning(string s) { Core.Log.Warn("[NPC] " + s); }
            /// <summary>Error line.</summary>
            public void LogError(string s) { Core.Log.Error("[NPC] " + s); }
        }

        /// <summary>Registers the world overlay Gui job (enabled only while ShowNavigation is on) and its live switch.</summary>
        partial void InitDebug()
        {
            _overlay = Scheduler.Gui(this, "Overlay", DebugOverlay.OnGUI, OverlayWanted);   // (WW perf) the job is also disabled while ShowNavigation is off
            _overlay.Enabled = ShowNav.Value;
            ShowNav.SettingChanged += OnShowNavChanged;
        }

        /// <summary>Gui predicate of the overlay job.</summary>
        static bool OverlayWanted() { return Running; }   // = DebugOverlay.OnGUI's own gate (it also prunes Nav's stale markers, so no finer test)
        /// <summary>ShowNavigation changed live: the overlay job follows it.</summary>
        void OnShowNavChanged(object s, EventArgs a) { if (_overlay != null) _overlay.Enabled = ShowNav.Value; }

        /// <summary>Unsubscribes the ShowNavigation switch (first step of Shutdown).</summary>
        partial void ShutdownDebugSwitch()
        {
            try { ShowNav.SettingChanged -= OnShowNavChanged; } catch (Exception) { }
        }

        /// <summary>Forgets the overlay job (Core removes it with the module's jobs).</summary>
        partial void ShutdownDebug()
        {
            _overlay = null;
        }

        /// <summary>tools.cfg [NPC / Debug]: VerboseLog, NavDump, ShowNavigation, HighlightUnbakedMapPois.</summary>
        static partial void BindDebug()
        {
            VerboseLog = H("Debug", "VerboseLog", false, "Detailed NPC logs (hits, detection, movement, maps) - also written when the global [Debug] VerboseLog is off.");
            NavDump = H("Debug", "NavDump", false, "Map pictures (BMP) of every camp map made, and of NPCs that find no route, in config/WildWasteland/NPC/NavDump (0.5-4 MB each; for troubleshooting). With VerboseLog also a per-second Trace line for every moving NPC and a trace picture of its trail when a chase ends or it rests (cyan = the map's route, white = walked on the map, orange = walked without the map, red = backing up / resting).");
            ShowNav = H("Debug", "ShowNavigation", false, "Draw the detection ghosts, NPC states and the structure maps' waypoints in the world.");
            HighlightUnbakedMapPois = H("Debug", "HighlightUnbakedMapPois", false,
                "Bake-collection aid: map POIs without a completed saved bake are light purple regardless of allegiance. Turn off to restore normal map colors immediately. Does not reveal fog or change compass colors.");
        }

        /// <summary>tools.cfg [NPC / Burrow] Log (last entry of its section).</summary>
        static partial void BindDebugBurrowLog()
        {
            BurrowLog = H("Burrow", "Log", false, "Log burrowing and the terrain layers found.");
        }

        /// <summary>tools.cfg [NPC / Dogs] Log (last entry of its section).</summary>
        static partial void BindDebugDogLog()
        {
            DogLog = H("Dogs", "Log", false, "Log each dog's reach and the head / neck bones found.");
        }

        /// <summary>tools.cfg [NPC / Nests] Log (last entry of its section).</summary>
        static partial void BindDebugNestLog()
        {
            NestLog = H("Nests", "Log", false, "Log seating, drops and nests freed (the nest summary and alarms are always logged).");
        }

        /// <summary>tools.cfg [NPC / Pounce] Log (last entry of its section).</summary>
        static partial void BindDebugPounceLog()
        {
            PounceLog = H("Pounce", "Log", false, "Log every zigzag, leap, bite and miss.");
        }
    }
}
