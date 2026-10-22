using WildWasteland.Core.UI;
using System;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.CleanHUD
{
    /// <summary>
    /// Apocasaver presentation part serving the HUD: the standalone's autosave Begin/End drive the WW loading screen, its save-name
    /// popup is drawn in the WW menu style, and <see cref="SaveActive"/> ORs its coordinator state into the HUD's "saving" check.
    /// Installed by the HUD with its own loading screen (the screen instance is HUD state). Since the session takeover
    /// (<see cref="Compatibility.Apocasaver.ApocasaverSession"/>) disables the standalone runner, these hooks only fire when that
    /// takeover failed (question Q1 in reports/B17.md).
    /// </summary>
    internal static class ApocasaverLoading
    {
        private static LoadingScreen _screen; // refs-ok: REFACTOR-REQUEST B17-10
        private static bool _ready;
        private static Func<bool> _saveActive;
        private static IModule _owner;
        /// <summary>True while WW serializes a save or the standalone's coordinator is active.</summary>
        internal static bool SaveActive { get { return Core.SinglePlayerSaving.Serializing || _saveActive != null && _saveActive(); } }
        private static Action _confirmName, _cancelName;
        private static GUIStyle _namePlate, _nameField;

        /// <summary>Resolves the standalone's coordinator/presentation/naming types and patches them under <paramref name="owner"/> (the HUD).</summary>
        internal static void Install(IModule owner, LoadingScreen screen) // refs-ok: REFACTOR-REQUEST B17-10
        {
            _screen = screen; _ready = false; _owner = owner;
            var coordinator = AccessTools.TypeByName("Apocasaver.SaveCoordinator");
            var active = coordinator != null ? AccessTools.PropertyGetter(coordinator, "Active") : null;
            _saveActive = active != null ? (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), active) : null;
            InstallNaming(owner);
            var type = AccessTools.TypeByName("Apocasaver.AutosavePresentation");
            if (type == null) return;
            bool begin = Core.Patches.Patch(owner, AccessTools.Method(type, "Begin"), postfix: AccessTools.Method(typeof(ApocasaverLoading), "AfterBegin"));
            bool end = Core.Patches.Patch(owner, AccessTools.Method(type, "End"), postfix: AccessTools.Method(typeof(ApocasaverLoading), "AfterEnd"));
            _ready = begin && end;
            if (_ready) Log.Main(owner, "Apocasaver autosave loading screen hooks ready");
        }
        /// <summary>Forgets the screen, delegates and styles (the owner's shutdown removes the patches).</summary>
        internal static void Clear() { _owner = null; _screen = null; _ready = false; _saveActive = null; _confirmName = _cancelName = null; _namePlate = _nameField = null; }
        /// <summary>Patches the standalone's SaveNaming.OnGUI when its private popup fields exist.</summary>
        private static void InstallNaming(IModule owner)
        {
            var type = AccessTools.TypeByName("Apocasaver.SaveNaming");
            if (type == null) return;
            try
            {
                foreach (string field in new[] { "_open", "_text", "_openedAt", "_focusPending" })
                    if (AccessTools.Field(type, field) == null) return;
                var confirm = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(type, "Confirm"));
                var cancel = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(type, "Cancel"));
                if (!Core.Patches.Patch(owner, AccessTools.Method(type, "OnGUI"), prefix: AccessTools.Method(typeof(ApocasaverLoading), "BeforeNameGui"))) return;
                _confirmName = confirm; _cancelName = cancel;
                Log.Main(owner, "Apocasaver save-name window ready; Clean menus off restores the original popup");
            }
            catch (Exception e) { Log.Warn(owner, "Apocasaver save-name window hooks: " + e.Message); }
        }
        /// <summary>Prefix of SaveNaming.OnGUI (every GUI event while the popup is open): draws the WW-styled popup and skips the original.</summary>
        private static bool BeforeNameGui(bool ____open, ref string ____text, float ____openedAt, ref bool ____focusPending)
        {
            if (!CompatibilityHost.CleanHud.Running || !CleanHudModule.MenuEnabled.Value || _confirmName == null || !____open) return true; // refs-ok: REFACTOR-REQUEST B17-10
            var matrix = GUI.matrix; var color = GUI.color; var background = GUI.backgroundColor; int depth = GUI.depth;
            try
            {
                if (_namePlate == null)
                {
                    _namePlate = new GUIStyle(GUIStyle.none) { border = new RectOffset(110, 110, 110, 110) };
                    _namePlate.normal.background = Assets.Tex("panel_menu"); // refs-ok: REFACTOR-REQUEST B17-10
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
                GUI.color = AccentTheme.Resolve(DialogueWidgets.Gold); // refs-ok: REFACTOR-REQUEST B17-10 GUI.DrawTexture(new Rect(-340, -40, 680, 60), Texture2D.whiteTexture); GUI.color = Color.white;
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
            catch (Exception e) { Log.Warn(_owner, "Apocasaver save-name window: " + e.Message); return true; }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.backgroundColor = background; GUI.depth = depth; }
        }
        /// <summary>Invisible button with a glyph-font caption (gold on hover).</summary>
        private static bool NameButton(Rect rect, string text)
        {
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            bool hover = rect.Contains(Event.current.mousePosition);
            NameText(rect, text, 34, hover ? DialogueWidgets.Gold : Color.white); // refs-ok: REFACTOR-REQUEST B17-10
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
        /// <summary>Postfix of AutosavePresentation.Begin: shows the WW saving screen.</summary>
        private static void AfterBegin()
        {
            if (_screen == null || !_ready) return;
            try { _screen.Begin(true, true); } catch (Exception e) { Log.Warn(_owner, "Apocasaver autosave screen: " + e.Message); }
        }
        /// <summary>Postfix of AutosavePresentation.End: ends the WW saving screen.</summary>
        private static void AfterEnd()
        {
            if (_screen == null || !_ready) return;
            try { _screen.EndSaving(); } catch (Exception e) { Log.Warn(_owner, "Apocasaver autosave screen end: " + e.Message); }
        }
    }
}
