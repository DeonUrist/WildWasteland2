using System;
using System.Text;
using UnityEngine;
using WildWasteland.Core.UI;
using CleanHud = WildWasteland.Modules.CleanHUD;

namespace WildWasteland.Core
{
    /// <summary>
    /// HUD part of <see cref="ReplacementGuard"/> (Wave A split): the title-menu notice listing replaced standalone mods and
    /// recommended mods, drawn through Ledger with the CleanHUD glyph font/accent theme. B10 takes this over.
    /// </summary>
    internal static partial class ReplacementGuard
    {
        static GUIContent _message;
        static readonly GUIContent Heading = new GUIContent("RESTART REQUIRED");
        static Vector2 _scroll;
        static readonly Action<Rect, string, float, Color, TextAnchor> NoticeText = CleanHud.GlyphFont.Draw, NoticeWrap = CleanHud.GlyphFont.DrawWrapped;
        static readonly Func<string, float, float, float> NoticeHeight = CleanHud.GlyphFont.WrappedHeight;

        /// <summary>Registers the title-notice GUI job (Prepare, after the replaced mods were queued).</summary>
        static partial void InstallNotice() { Scheduler.Gui(null, "Core.Replacement notice", Draw, WantsNotice); }

        /// <summary>Rebuilds the restart notice text from the replaced-mod list (Prepare and late conflicts).</summary>
        static partial void RebuildNotice()
        {
            if (_names.Count == 0) return;
            var text = new StringBuilder();
            text.Append(_names.Count).Append(_names.Count == 1 ? " standalone mod should be removed" : " standalone mods should be removed");
            text.Append(" from your active mods because Wild Wasteland replaces them:\n\n");
            for (int i = 0; i < _names.Count; i++) text.Append("- ").Append(_names[i]).Append('\n');
            if (_failed < _names.Count) text.Append("\nRestart the game. Apocasetter will disable the queued mods. Their files and configs are kept.");
            if (_failed != 0) text.Append("\n\nDisable the mods marked 'disable manually' before restarting. Automatic disabling was unavailable for them; see LogOutput.log for details.");
            _message = new GUIContent(text.ToString());
        }

        /// <summary>GUI predicate: title scene, something to show, no menu modal or loading screen.</summary>
        static bool WantsNotice() { return _title && (_message != null || _showRecommended.Value && _missing.Count > 0) && !MenuModalOpen && !LoadingProgress.ScreenVisible; }

        /// <summary>Draws the notice panel (restart warning + recommended mods with Nexus links) at 1920x1080 scale.</summary>
        static void Draw()
        {
            if (LoadingProgress.ScreenVisible || MenuModalOpen || ToolWindow.IsOpen || EditorSession.Busy || ApocasetterBridge.InputBlocked) return;
            bool recommendations = _showRecommended.Value && _missing.Count > 0;
            if (_message == null && !recommendations) return;
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            float width = 810f, inset = 30f;
            float warningHeight = _message != null ? NoticeHeight(_message.text, 26f, width - inset * 2 - 30f) + 111f : 0;
            float contentHeight = warningHeight + (recommendations ? 141f + 54f * _missing.Count : 0);
            float height = Mathf.Min(contentHeight, Screen.height / scale - 540f);
            var panel = new Rect(Screen.width / scale - width - inset - 40f, Mathf.Max(inset + 40f, (Screen.height / scale - height) * .5f), width, height);
            int depth = GUI.depth; Color color = GUI.color; var matrix = GUI.matrix;
            var accent = CleanHud.CleanHudModule.Running ? CleanHud.AccentTheme.Resolve(CleanHud.DialogueWidgets.Gold) : CleanHud.DialogueWidgets.Gold;
            var border = new Color(214 / 255f, 203 / 255f, 176 / 255f, .55f);
            if (CleanHud.CleanHudModule.Running) border = CleanHud.AccentTheme.Resolve(border);
            var skin = Ledger.BeginSettings(accent, border, CleanHud.Assets.Tex("panel_menu"), CleanHud.Assets.VGradient(.75f), NoticeText, NoticeWrap, NoticeHeight);
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
                    for (int n = 0; n < _missing.Count; n++)
                    {
                        int i = _missing[n];
                        var row = new Rect(0, warningHeight + 105f + 54f * n, rowWidth, 54f);
                        if (Ledger.Button(row, Recommended[i, 1], gameFont: true)) Application.OpenURL("https://www.nexusmods.com/apocalypter/mods/" + Recommended[i, 2]);
                    }
                }
                GUI.EndScrollView();
            }
            finally { Ledger.EndSettings(); GUI.skin = skin; GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; }
        }
    }
}
