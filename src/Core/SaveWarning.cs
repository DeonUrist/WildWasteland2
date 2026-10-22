using System.IO;
using UnityEngine;
using CleanHud = WildWasteland.Modules.CleanHUD;

namespace WildWasteland.Core
{
    internal static class SaveWarning
    {
        static byte _phase;
        static int _seconds;
        static string _text;
        internal static void Install() { Scheduler.Gui(null, "Autosave compass countdown", Draw, Wants); }
        static void Clear() { _phase = 0; _text = null; }
        internal static void Show(byte phase, int seconds = 0) { if (phase != _phase || seconds != _seconds) Set(phase, seconds); }
        static void Set(byte phase, int seconds)
        {
            _phase = phase; _seconds = seconds;
            _text = phase == 1 ? "AUTOSAVE IN " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") : phase == 2 ? "AUTOSAVE DELAYED - WAITING FOR SAFETY" : phase == 3 ? "The game will save now" : null;
        }
        static bool Wants() { return _phase != 0; }
        static void Draw()
        {
            if (!SaveSystem.LocalWorld) { Clear(); return; }
            if (LoadingProgress.MenusBlocked || !GameRefs.InGame || UI.EditorSession.Busy || Event.current.type != EventType.Repaint) return;
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            Vector2 anchor = CleanHud.Hud.SaveWarningAnchor();
            var rect = new Rect(anchor.x - 410 * scale, anchor.y + 8 * scale, 820 * scale, 58 * scale);
            int depth = GUI.depth; GUI.depth = -100;
            var color = _phase == 3 || _phase == 1 && _seconds <= 15 ? new Color(1f, .78f, .35f) : new Color(.96f, .91f, .79f);
            CleanHud.GlyphFont.DrawGame(rect, _text, 24 * scale, color, TextAnchor.MiddleCenter);
            GUI.depth = depth;
        }
    }
}
