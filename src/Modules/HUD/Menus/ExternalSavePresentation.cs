using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.HUD
{
    /// <summary>Uses HUD-owned screens, styles and settings for external save presentation.</summary>
    internal sealed class ExternalSavePresentation : IExternalSavePresentation
    {
        private readonly LoadingScreen _screen;
        private static GUIStyle _namePlate, _nameField;
        /// <summary>Binds the current HUD screen; no third-party hooks are installed here.</summary>
        internal ExternalSavePresentation(LoadingScreen screen) { _screen=screen; }
        /// <summary>Shows autosave presentation while the external coordinator runs.</summary>
        public void Begin() { _screen.Begin(true,true); }
        /// <summary>Releases the autosave presentation.</summary>
        public void End() { _screen.EndSaving(); }
        /// <summary>Prefix of SaveNaming.OnGUI (every GUI event while the popup is open): draws the WW-styled popup and skips the original.</summary>
        public bool DrawName(ref string ____text, float ____openedAt, ref bool ____focusPending, Action _confirmName, Action _cancelName)
        {
            if (!HudModule.Running || !HudModule.MenuEnabled.Value || _confirmName == null) return true;
            var matrix = GUI.matrix; var color = GUI.color; var background = GUI.backgroundColor; int depth = GUI.depth;
            try
            {
                if (_namePlate == null)
                {
                    _namePlate = new GUIStyle(GUIStyle.none) { border = new RectOffset(110, 110, 110, 110) };
                    _namePlate.normal.background = Assets.Tex("panel_menu");
                    _nameField = new GUIStyle(GUI.skin.textField) { font = Core.ApocasetterBridge.GameFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf"), fontSize = 28,
                        alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 0, 0), border = new RectOffset() };
                    foreach (var state in new[] { _nameField.normal, _nameField.hover, _nameField.active, _nameField.focused })
                    { state.background = Texture2D.whiteTexture; state.textColor = Color.white; }
                }
                var e = Event.current;
                if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
                { e.Use(); if (Time.realtimeSinceStartup - ____openedAt > .3f) { _confirmName(); return false; } }
                float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
                GUI.matrix = Matrix4x4.TRS(new Vector3(Screen.width / 2f, Screen.height / 2f, 0), Quaternion.identity, new Vector3(scale, scale, 1));
                GUI.depth = -1000; GUI.color = new Color(0, 0, 0, .55f);
                GUI.DrawTexture(new Rect(-Screen.width / (2f * scale), -Screen.height / (2f * scale), Screen.width / scale, Screen.height / scale), Texture2D.whiteTexture);
                GUI.color = Color.white; GUI.backgroundColor = Color.white;
                GUI.Box(new Rect(-420, -170, 840, 340), "", _namePlate);
                NameText(new Rect(-340, -112, 680, 50), "SAVE NAME", 34, Color.white);
                GUI.color = AccentTheme.Resolve(DialogueWidgets.Gold); GUI.DrawTexture(new Rect(-340, -40, 680, 60), Texture2D.whiteTexture); GUI.color = Color.white;
                GUI.backgroundColor = new Color(.08f, .07f, .05f);
                GUI.SetNextControlName("apocasaver_savename");
                ____text = GUI.TextField(new Rect(-338, -38, 676, 56), ____text, 24, _nameField);
                GUI.backgroundColor = Color.white;
                bool confirm = NameButton(new Rect(-340, 54, 320, 64), "OK");
                bool cancel = NameButton(new Rect(20, 54, 320, 64), "CANCEL");
                if (____focusPending) { GUI.FocusControl("apocasaver_savename"); ____focusPending = false; }
                if (confirm) _confirmName(); else if (cancel) _cancelName();
                return false;
            }
            catch (Exception e) { Log.Warn(HudModule.Instance, "Apocasaver save-name window: " + e.Message); return true; }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.backgroundColor = background; GUI.depth = depth; }
        }
        /// <summary>Invisible button with a glyph-font caption (gold on hover).</summary>
        private static bool NameButton(Rect rect, string text)
        {
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            bool hover = rect.Contains(Event.current.mousePosition);
            NameText(rect, text, 34, hover ? DialogueWidgets.Gold : Color.white);
            return clicked;
        }
        /// <summary>Centered glyph-font text (repaint only).</summary>
        private static void NameText(Rect rect, string text, float size, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var font = GlyphFont.Get(); float scale = size / font.Size;
            float x = rect.center.x - font.Measure(text, size, 0) / 2f;
            float baseline = rect.center.y + (font.Ascent - font.Descent) * scale / 2f;
            var previous = GUI.color; GUI.color = AccentTheme.Resolve(color);
            foreach (char ch in text)
            {
                GlyphFont.G glyph;
                if (!font.Map.TryGetValue(ch, out glyph)) continue;
                if (ch != ' ' && glyph.W > 1 && glyph.H > 1)
                    GUI.DrawTextureWithTexCoords(new Rect(x + glyph.Ox * scale, baseline - glyph.Top * scale, glyph.W * scale, glyph.H * scale), font.Atlas,
                        new Rect(glyph.X / font.AtlasW, 1f - (glyph.Y + glyph.H) / font.AtlasH, glyph.W / font.AtlasW, glyph.H / font.AtlasH));
                x += glyph.Adv * scale;
            }
            GUI.color = previous;
        }
    }
}
