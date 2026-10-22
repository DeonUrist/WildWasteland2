using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class ApocasaverLoading
    {
        private static LoadingScreen _screen;
        private static bool _ready;
        private static Func<bool> _saveActive;
        internal static bool SaveActive { get { return Core.SinglePlayerSaving.Serializing || _saveActive != null && _saveActive(); } }
        private static Action _confirmName, _cancelName;
        private static GUIStyle _namePlate, _nameField;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(CleanHudModule owner, LoadingScreen screen)
        {
            _screen = screen; _ready = false;
            var coordinator = AccessTools.TypeByName("Apocasaver.SaveCoordinator");
            var active = coordinator != null ? AccessTools.PropertyGetter(coordinator, "Active") : null;
            _saveActive = active != null ? (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), active) : null;
            InstallNaming(owner);
            var type = AccessTools.TypeByName("Apocasaver.AutosavePresentation");
            if (type == null) return;
            bool begin = Core.Patches.Patch(owner, AccessTools.Method(type, "Begin"), postfix: AccessTools.Method(typeof(ApocasaverLoading), "AfterBegin"));
            bool end = Core.Patches.Patch(owner, AccessTools.Method(type, "End"), postfix: AccessTools.Method(typeof(ApocasaverLoading), "AfterEnd"));
            _ready = begin && end;
            if (_ready) CleanHudModule.Log("Autosave loading screen hooks ready");
        }
        internal static void Clear() { _screen = null; _ready = false; _saveActive = null; _confirmName = _cancelName = null; _namePlate = _nameField = null; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InstallNaming(CleanHudModule owner)
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
                CleanHudModule.Log("Apocasaver save-name window ready; Clean menus off restores the original popup");
            }
            catch (Exception e) { CleanHudModule.Throttled("save-name window hooks", e); }
        }
        private static bool BeforeNameGui(bool ____open, ref string ____text, float ____openedAt, ref bool ____focusPending)
        {
            if (!CleanHudModule.Running || !CleanHudModule.MenuEnabled.Value || _confirmName == null || !____open) return true;
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
            catch (Exception e) { CleanHudModule.Throttled("save-name window", e); return true; }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.backgroundColor = background; GUI.depth = depth; }
        }
        private static bool NameButton(Rect rect, string text)
        {
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            bool hover = rect.Contains(Event.current.mousePosition);
            NameText(rect, text, 34, hover ? DialogueWidgets.Gold : Color.white);
            return clicked;
        }
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
        private static void AfterBegin()
        {
            if (_screen == null || !_ready) return;
            try { _screen.Begin(true, true); } catch (Exception e) { CleanHudModule.Throttled("autosave screen", e); }
        }
        private static void AfterEnd()
        {
            if (_screen == null || !_ready) return;
            try { _screen.EndSaving(); } catch (Exception e) { CleanHudModule.Throttled("autosave screen end", e); }
        }
    }
}
