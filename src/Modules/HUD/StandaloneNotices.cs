using System;
using System.Text;
using UnityEngine;
using WildWasteland.Core.UI;
using WildWasteland.Core;
using CleanHud = WildWasteland.Modules.HUD;

namespace WildWasteland.Modules.HUD
{
    /// <summary>HUD-owned title warnings rendered from Core standalone replacement status.</summary>
    internal static class StandaloneNotices
    {
        static GUIContent _message;
        static readonly GUIContent Heading = new GUIContent("RESTART REQUIRED");
        static Vector2 _scroll;
        static readonly Action<Rect, string, float, Color, TextAnchor> NoticeText = WildWasteland.Core.UI.GlyphFont.Draw, NoticeWrap = WildWasteland.Core.UI.GlyphFont.DrawWrapped;
        static readonly Func<string, float, float, float> NoticeHeight = WildWasteland.Core.UI.GlyphFont.WrappedHeight;

        /// <summary>Registers this module's title notice renderer; module cleanup removes its job.</summary>
        internal static void Install(IModule owner) { Scheduler.Gui(owner, "HUD.Replacement notice", Draw, WantsNotice); }

        /// <summary>GUI predicate: title scene, something to show, no menu modal or loading screen.</summary>
        static bool WantsNotice() { return ReplacementGuard.TitleActive && (ReplacementGuard.NoticeText != null || ReplacementGuard.ShowRecommendations && ReplacementGuard.MissingCount > 0) && !ReplacementGuard.MenuModalOpen && !LoadingProgress.ScreenVisible; }

        /// <summary>Draws the notice panel (restart warning + recommended mods with Nexus links) at 1920x1080 scale.</summary>
        static void Draw()
        {
            if (LoadingProgress.ScreenVisible || ReplacementGuard.MenuModalOpen || ToolWindow.IsOpen || EditorSession.Busy || ApocasetterBridge.InputBlocked) return;
            string message = ReplacementGuard.NoticeText;
            if ((_message != null ? _message.text : null) != message) _message = message != null ? new GUIContent(message) : null;
            bool recommendations = ReplacementGuard.ShowRecommendations && ReplacementGuard.MissingCount > 0;
            if (_message == null && !recommendations) return;
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            float width = 810f, inset = 30f;
            float warningHeight = _message != null ? NoticeHeight(_message.text, 26f, width - inset * 2 - 30f) + 111f : 0;
            float contentHeight = warningHeight + (recommendations ? 141f + 54f * ReplacementGuard.MissingCount : 0);
            float height = Mathf.Min(contentHeight, Screen.height / scale - 540f);
            var panel = new Rect(Screen.width / scale - width - inset - 40f, Mathf.Max(inset + 40f, (Screen.height / scale - height) * .5f), width, height);
            int depth = GUI.depth; Color color = GUI.color; var matrix = GUI.matrix;
            var accent = AccentTheme.Resolve(ThemePalette.Gold);
            var border = new Color(214 / 255f, 203 / 255f, 176 / 255f, .55f);
            border = AccentTheme.Resolve(border);
            var skin = Ledger.BeginSettings(accent, border, ThemeAssets.Tex("panel_menu"), ThemeAssets.VGradient(.75f), NoticeText, NoticeWrap, NoticeHeight);
            GUI.depth = -800; GUI.color = Color.white;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            try
            {
                Ledger.Window(panel);
                var view = new Rect(panel.x + inset, panel.y + 18f, width - inset * 2, height - 36f);
                float rowWidth = view.width - 30f;
                _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0, 0, rowWidth, contentHeight - 36f));
                if (_message != null)
                {
                    Ledger.Label(new Rect(0, 3f, rowWidth, 45f), Heading.text, Ledger.Heading, Ledger.Accent);
                    NoticeWrap(new Rect(0, 63f, rowWidth, warningHeight - 96f), _message.text, 26f, Ledger.Text, TextAnchor.MiddleLeft);
                }
                if (recommendations)
                {
                    Ledger.Label(new Rect(0, warningHeight + 3f, rowWidth, 45f), "RECOMMENDED MODS", Ledger.Heading, Ledger.Accent);
                    Ledger.Label(new Rect(0, warningHeight + 54f, rowWidth, 51f), "Optional additions (not active). Click to view mod.", Ledger.Small, Ledger.Muted);
                    for (int n = 0; n < ReplacementGuard.MissingCount; n++)
                    {
                        var row = new Rect(0, warningHeight + 105f + 54f * n, rowWidth, 54f);
                        if (Ledger.Button(row, ReplacementGuard.MissingName(n), gameFont: true)) Application.OpenURL(ReplacementGuard.MissingUrl(n));
                    }
                }
                GUI.EndScrollView();
            }
            finally { Ledger.EndSettings(); GUI.skin = skin; GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; }
        }
    }
}
