
namespace WildWasteland.Core
{
    internal static partial class SaveWarning
    {
        static byte _phase;
        static int _seconds;
        static string _text;
        // Wave A split (refactor): the countdown drawing (GUI job, CleanHUD compass anchor and glyph font) lives in
        // src/Modules/HUD/_fromCore/SaveWarning.HUD.cs; the phase/text state stays here.
        /// <summary>HUD part: registers the countdown GUI job.</summary>
        static partial void InstallHud();
        /// <summary>Registers the countdown drawing (HUD part).</summary>
        internal static void Install() { InstallHud(); }
        /// <summary>Hides the countdown (no local world).</summary>
        static void Clear() { _phase = 0; _text = null; }
        /// <summary>Sets the countdown phase (0 none, 1 countdown, 2 delayed, 3 saving now); text is rebuilt only on change.</summary>
        internal static void Show(byte phase, int seconds = 0) { if (phase != _phase || seconds != _seconds) Set(phase, seconds); }
        /// <summary>Stores phase/seconds and builds the display text once.</summary>
        static void Set(byte phase, int seconds)
        {
            _phase = phase; _seconds = seconds;
            _text = phase == 1 ? "AUTOSAVE IN " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") : phase == 2 ? "AUTOSAVE DELAYED - WAITING FOR SAFETY" : phase == 3 ? "The game will save now" : null;
        }
        /// <summary>GUI predicate: true while a phase is shown.</summary>
        static bool Wants() { return _phase != 0; }
    }
}
