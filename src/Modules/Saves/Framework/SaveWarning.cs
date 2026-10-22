using WildWasteland.Core;
using UI = WildWasteland.Core.UI;
using Events = WildWasteland.Core.Events;

namespace WildWasteland.Modules.Saves
{
    internal static class SaveWarning
    {
        static byte _phase;
        static int _seconds;
        static string _text;
        /// <summary>Sets the countdown phase (0 none, 1 countdown, 2 delayed, 3 saving now); text is rebuilt only on change.</summary>
        internal static void Show(byte phase, int seconds = 0) { if (phase != _phase || seconds != _seconds) Set(phase, seconds); }
        /// <summary>Stores phase/seconds and builds the display text once.</summary>
        static void Set(byte phase, int seconds)
        {
            _phase = phase; _seconds = seconds;
            _text = phase == 1 ? "AUTOSAVE IN " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") : phase == 2 ? "AUTOSAVE DELAYED - WAITING FOR SAFETY" : phase == 3 ? "The game will save now" : null;
            var presentation = new Events.SavePresentation
            {
                Phase = phase == 0 ? Events.SavePresentationPhase.End : Events.SavePresentationPhase.Progress,
                Operation = SavePhase.Idle, Fraction = 0, Text = _text
            };
            ModEvents.Publish(ref presentation);
        }
    }
}
