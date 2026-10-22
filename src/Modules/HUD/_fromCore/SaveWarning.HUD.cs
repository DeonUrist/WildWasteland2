using WildWasteland.Core.UI;
using UnityEngine;
using CleanHud = WildWasteland.Modules.CleanHUD;

namespace WildWasteland.Core
{
    /// <summary>
    /// HUD part of <see cref="SaveWarning"/> (Wave A split): draws the autosave countdown under the CleanHUD compass with the
    /// glyph font. B10 takes this over (Saves presentation events).
    /// </summary>
    internal static partial class SaveWarning
    {
        /// <summary>Registers the predicate-gated GUI job (only while a phase is shown).</summary>
        static partial void InstallHud(IModule owner) { Scheduler.Gui(owner, "Autosave compass countdown", Draw, Wants); }

        /// <summary>Repaint-only countdown text under the compass (hidden in menus, loading, editors).</summary>
        static void Draw()
        {
            if (!SaveSystem.LocalWorld) { Clear(); return; }
            if (LoadingProgress.MenusBlocked || !GameRefs.InGame || UI.EditorSession.Busy || Event.current.type != EventType.Repaint) return;
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            Vector2 anchor = CleanHud.Hud.SaveWarningAnchor();
            var rect = new Rect(anchor.x - 410 * scale, anchor.y + 8 * scale, 820 * scale, 58 * scale);
            int depth = GUI.depth; GUI.depth = -100;
            var color = _phase == 3 || _phase == 1 && _seconds <= 15 ? new Color(1f, .78f, .35f) : new Color(.96f, .91f, .79f);
            WildWasteland.Core.UI.GlyphFont.DrawGame(rect, _text, 24 * scale, color, TextAnchor.MiddleCenter);
            GUI.depth = depth;
        }
    }
}
