using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WildWasteland.Core.UI
{
    // Measured from the selected Salvage ledger prototype, including its game chrome.
    public static class Ledger
    {
        public const float Width = 1028, Height = 755;
        private static readonly Color[] Palette = { C("1B1610"), C("282119"), C("332C21"), C("17130D"), C("EEE4CF"), C("B8AD99"), C("514835"), C("E4CB69"), C("3B3321"), C("F09578") };
        private static readonly Color[] CleanPalette = { C("10100F"), C("171715"), C("20201D"), C("111110"), C("F3EFE4"), new Color(243 / 255f, 239 / 255f, 228 / 255f, .5f), C("FF6152") };
        private static bool _settings, _skinSettings;
        private static Color _settingsAccent, _settingsBorder, _skinAccent;
        private static GUIStyle _settingsPlate, _settingsHiddenInput;
        private static Texture2D _settingsGradient;
        private static Action<Rect, string, float, Color, TextAnchor> _settingsText, _settingsWrap;
        private static Func<string, float, float, float> _settingsHeight;
        internal static bool SettingsActive { get { return _settings; } }
        public static Color Background { get { return _settings ? CleanPalette[0] : Palette[0]; } }
        public static Color Surface { get { return _settings ? CleanPalette[1] : Palette[1]; } }
        public static Color Raised { get { return _settings ? CleanPalette[2] : Palette[2]; } }
        public static Color Field { get { return _settings ? CleanPalette[3] : Palette[3]; } }
        public static Color Text { get { return _settings ? CleanPalette[4] : Palette[4]; } }
        public static Color Muted { get { return _settings ? CleanPalette[5] : Palette[5]; } }
        public static Color Line { get { return _settings ? _settingsBorder : Palette[6]; } }
        public static Color Accent { get { return _settings ? _settingsAccent : Palette[7]; } }
        public static Color Selected { get { return _settings ? Color.Lerp(CleanPalette[1], _settingsAccent, .16f) : Palette[8]; } }
        public static Color Danger { get { return _settings ? CleanPalette[6] : Palette[9]; } }
        public static GUIStyle Body, Small, Heading, Brand, GameButton, DialogTitle, Input, Scrollbar;
        private static GUIStyle CloseText, InputRight;
        private static GUISkin _skin;
        private static Font _body, _bold, _game;
        private static readonly Dictionary<string, Texture2D> Icons = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, string> Numbers = new Dictionary<string, string>();
        private static readonly Dictionary<string, Texture2D> Boxes = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, float> ButtonOffsets = new Dictionary<string, float>();
        public static Color C(string hex) { return Skin.Hex(hex); }
        // the game's menu font when Apocasetter / the MODS button did not provide one: a scan of every loaded Font, so it is cached and
        // retried at most once a second (Begin runs on every IMGUI event while a window is open)
        private static Font _scannedFont; private static float _nextFontScan;
        private static Font ScannedGameFont()
        {
            if (_scannedFont == null && Time.realtimeSinceStartup >= _nextFontScan)
            {
                _nextFontScan = Time.realtimeSinceStartup + 1f;
                _scannedFont = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(f => f.name == "Helveticrap");
            }
            return _scannedFont;
        }
        internal static GUISkin BeginSettings(Color accent, Color border, Texture2D plate, Texture2D gradient, Action<Rect, string, float, Color, TextAnchor> text,
            Action<Rect, string, float, Color, TextAnchor> wrap, Func<string, float, float, float> height)
        {
            _settingsAccent = accent; _settingsBorder = border; _settingsText = text; _settingsWrap = wrap; _settingsHeight = height; _settingsGradient = gradient;
            if (_settingsPlate == null) _settingsPlate = new GUIStyle(GUIStyle.none) { border = new RectOffset(110, 110, 110, 110) };
            _settingsPlate.normal.background = plate;
            return Begin(true);
        }
        internal static void EndSettings() { _settings = false; _settingsText = _settingsWrap = null; _settingsHeight = null; }
        public static GUISkin Begin() { return Begin(false); }
        private static GUISkin Begin(bool settings)
        {
            _settings = settings;
            var old = GUI.skin; Skin.Ensure(1);
            var font = Skin.GameFont ?? ScannedGameFont();
            if (_skin == null || (_game != font && font != null) || _skinSettings != settings || (settings && _skinAccent != _settingsAccent))
            {
                if (_skin != null) UnityEngine.Object.Destroy(_skin);
                _skinSettings = settings; _skinAccent = _settingsAccent;
                if (_body == null) _body = Font.CreateDynamicFontFromOSFont("Segoe UI", 14);
                if (_bold == null) _bold = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Semibold", "Segoe UI" }, 14);
                _game = font;
                Body = Style(_body, settings ? 26 : 14); Small = Style(_body, settings ? 20 : 12); Heading = Style(_bold, settings ? 30 : 14); Heading.fontStyle = FontStyle.Bold;
                DialogTitle = Style(_bold, settings ? 40 : 18); DialogTitle.fontStyle = FontStyle.Bold; Brand = Style(_game ?? _bold, settings ? 76 : 26); GameButton = Style(_game ?? _bold, settings ? 26 : 15); CloseText = Style(_game ?? _bold, settings ? 34 : 13);
                string field = settings ? "111110" : "17130D", line = settings ? ColorUtility.ToHtmlStringRGBA(Line) : "514835", accent = settings ? ColorUtility.ToHtmlStringRGBA(Accent) : "E4CB69";
                Input = Style(_body, settings ? 26 : 14); Input.padding = new RectOffset(9, 9, settings ? 3 : 6, settings ? 3 : 6); Input.border = new RectOffset(1, 1, 1, 1);
                Input.normal.background = Box(field, line); Input.hover.background = Input.normal.background;
                Input.focused.background = Box(field, accent); Input.active.background = Input.focused.background;
                InputRight = new GUIStyle(Input) { alignment = TextAnchor.MiddleRight };
                if (settings)
                {
                    _settingsHiddenInput = new GUIStyle(Input);
                    foreach (var state in new[] { _settingsHiddenInput.normal, _settingsHiddenInput.hover, _settingsHiddenInput.active, _settingsHiddenInput.focused })
                    { state.textColor = Color.clear; state.background = null; }
                }
                _skin = UnityEngine.Object.Instantiate(old); _skin.label = Body; _skin.textField = Input;
                Scrollbar = new GUIStyle(old.verticalScrollbar) { fixedWidth = 7, padding = new RectOffset(), margin = new RectOffset(), border = new RectOffset() };
                Scrollbar.normal.background = Box(field, field);
                _skin.verticalScrollbar = Scrollbar;
                var thumb = new GUIStyle(old.verticalScrollbarThumb) { fixedWidth = 7, padding = new RectOffset(), margin = new RectOffset(), border = new RectOffset(), fixedHeight = 0 };
                thumb.normal.background = thumb.hover.background = thumb.active.background = Box(settings ? accent : "514835", settings ? accent : "514835");
                _skin.verticalScrollbarThumb = thumb;
                _skin.verticalScrollbarUpButton = _skin.verticalScrollbarDownButton = new GUIStyle(GUIStyle.none) { fixedHeight = 0, fixedWidth = 0 };
            }
            GUI.skin = _skin; return old;
        }
        private static GUIStyle Style(Font font, int size)
        {
            var s = new GUIStyle(GUIStyle.none) { font = font, fontSize = size, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = Text; return s;
        }
        private static Texture2D Box(string fill, string edge)
        {
            string key = fill + edge; Texture2D texture; if (Boxes.TryGetValue(key, out texture)) return texture;
            texture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) texture.SetPixel(x, y, x == 0 || y == 0 || x == 7 || y == 7 ? C(edge) : C(fill));
            texture.Apply(); Boxes[key] = texture; return texture;
        }
        public static void Fill(Rect r, Color color) { Skin.Fill(r, color); }
        public static void Panel(Rect r, Color? fill = null, Color? border = null)
        {
            Fill(r, fill ?? Surface); var edge = border ?? Line; float stroke = _settings ? 1.5f : 1;
            Fill(new Rect(r.x, r.y, r.width, stroke), edge); Fill(new Rect(r.x, r.yMax - stroke, r.width, stroke), edge);
            Fill(new Rect(r.x, r.y, stroke, r.height), edge); Fill(new Rect(r.xMax - stroke, r.y, stroke, r.height), edge);
        }
        public static void Label(Rect r, string text, GUIStyle font = null, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var style = font ?? Body; var previous = style.alignment; style.alignment = align;
            if (_settings && _settingsText != null)
            { var draw = style.wordWrap ? _settingsWrap : _settingsText; draw(r, text, style.fontSize, color ?? Text, align); style.alignment = previous; return; }
            Skin.Label(r, text, style, color ?? Text); style.alignment = previous;
        }
        internal static float TextHeight(string text, GUIStyle style, float width)
        { return _settings ? _settingsHeight(text, style.fontSize, width) : style.CalcHeight(new GUIContent(text), width); }

        internal static void RowHighlight(Rect rect, bool selected = false)
        {
            if (!_settings || !GUI.enabled || (!selected && !rect.Contains(Event.current.mousePosition))) return;
            Fill(new Rect(rect.x, rect.center.y - 12, 7, 24), Accent);
        }

        internal static bool Choice(Rect rect, string text, bool selected)
        {
            bool hit = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Label(rect, text, GameButton, !GUI.enabled ? Muted : selected || hover ? Accent : Text, TextAnchor.MiddleCenter);
            if (selected) Fill(new Rect(rect.x + 14, rect.yMax - 7, rect.width - 28, 2), Accent);
            return hit;
        }

        internal static bool KeyButton(Rect rect, string text)
        {
            if (!_settings) return Button(rect, text);
            bool hit = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Panel(new Rect(rect.x, rect.y + 7, rect.width, rect.height - 14), Color.clear, hover ? Accent : Line);
            Label(rect, text, GameButton, !GUI.enabled ? Muted : hover ? Accent : Text, TextAnchor.MiddleCenter);
            return hit;
        }

        internal static string TextField(Rect rect, string key, string text, bool right = false)
        {
            if (!_settings) return GUI.TextField(rect, text, right ? InputRight : Input);
            string name = "WW.Settings/" + key; GUI.SetNextControlName(name);
            bool focused = GUI.GetNameOfFocusedControl() == name;
            if (!focused && GUI.enabled && rect.Contains(Event.current.mousePosition)) Panel(rect, Color.clear, Accent);
            string next = GUI.TextField(rect, text, focused ? (right ? InputRight : Input) : _settingsHiddenInput);
            if (!focused) Label(new Rect(rect.x + 10, rect.y, rect.width - 20, rect.height), next, Body, GUI.enabled ? Text : Muted, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
            return next;
        }

        internal static float Slider(Rect rect, float value, float min, float max)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, rect); var e = Event.current;
            bool down = e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition);
            if (GUI.enabled && (down || GUIUtility.hotControl == id && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp)))
            {
                if (down) GUIUtility.hotControl = id;
                value = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width));
                if (e.type == EventType.MouseUp) GUIUtility.hotControl = 0;
                e.Use();
            }
            Panel(rect, new Color(0, 0, 0, .25f), Line);
            float width = (rect.width - 3) * Mathf.InverseLerp(min, max, value);
            if (width > 0)
            {
                var previous = GUI.color; GUI.color = Accent;
                GUI.DrawTexture(new Rect(rect.x + 1.5f, rect.y + 1.5f, width, rect.height - 3), _settingsGradient); GUI.color = previous;
            }
            return value;
        }
        public static bool Button(Rect r, string text, bool enabled = true, bool primary = false, bool gameFont = false, Color? color = null, bool transparent = false)
        {
            bool previous = GUI.enabled; GUI.enabled = previous && enabled;
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (_settings)
            {
                bool arrow = text == "<" || text == ">";
                if (!arrow) RowHighlight(r, primary);
                var caption = gameFont ? new Rect(r.x + 22, r.y, r.width - 22, r.height) : r;
                Label(caption, text, gameFont ? CloseText : GameButton, color ?? (!GUI.enabled ? Muted : primary || hover || arrow ? Accent : Text), gameFont ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                GUI.enabled = previous; return hit;
            }
            var fill = primary ? Accent : hover ? Selected : transparent ? Color.clear : Raised; var border = primary || hover ? Accent : Line;
            if (!enabled) { fill = Color.Lerp(Background, fill, .4f); border = Color.Lerp(Background, border, .4f); }
            Panel(r, fill, border);
            var ink = color ?? (primary ? Background : Text);
            if (text.StartsWith("+  ", StringComparison.Ordinal))
            {
                string caption = text.Substring(3); var font = caption.StartsWith("New ", StringComparison.Ordinal) ? Small : Body;
                float length = font.CalcSize(new GUIContent(caption)).x, left = r.center.x - (length + 23) / 2;
                IconImage(new Rect(left, r.center.y - 8, 16, 16), "plus", ink);
                ButtonLabel(new Rect(left + 23, r.y, length + 1, r.height), caption, font, ink, TextAnchor.MiddleLeft);
            }
            else if (gameFont) Label(r, text, CloseText, ink, TextAnchor.MiddleCenter);
            else ButtonLabel(r, text, Body, ink, TextAnchor.MiddleCenter);
            GUI.enabled = previous; return hit;
        }
        public static void ButtonLabel(Rect r, string text, GUIStyle style, Color ink, TextAnchor alignment)
        {
            if (_settings) { Label(r, text, style, ink, alignment); return; }
            // Centre the visible glyphs, rather than Segoe UI's asymmetric ascent/descent line box.
            string key = style.font.GetInstanceID() + "/" + style.fontSize + "/" + text; float shift;
            if (!ButtonOffsets.TryGetValue(key, out shift))
            {
                var font = style.font; font.RequestCharactersInTexture(text, style.fontSize, style.fontStyle);
                int min = int.MaxValue, max = int.MinValue;
                foreach (char c in text) { if (char.IsWhiteSpace(c)) continue; CharacterInfo info; if (!font.GetCharacterInfo(c, out info, style.fontSize, style.fontStyle)) continue; min = Mathf.Min(min, info.minY); max = Mathf.Max(max, info.maxY); }
                float scale = font.fontSize > 0 ? style.fontSize / (float)font.fontSize : 1;
                shift = min <= max ? font.lineHeight * scale / 2 - (font.ascent * scale - (min + max) / 2f) : 0;
                ButtonOffsets[key] = shift;
            }
            r.y += shift; Label(r, text, style, ink, alignment);
        }
        public static void IconImage(Rect r, string name, Color color)
        {
            Texture2D icon;
            if (!Icons.TryGetValue(name, out icon))
            {
                string path = Path.Combine(Path.GetDirectoryName(Skin.Dir), "ledger", name + ".png");
                if (!File.Exists(path)) return;
                icon = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                if (!ImageConversion.LoadImage(icon, File.ReadAllBytes(path), false)) return; Icons[name] = icon;
            }
            var previous = GUI.color; GUI.color = color; GUI.DrawTexture(r, icon); GUI.color = previous;
        }
        public static bool Icon(Rect r, string name, bool enabled = true, Color? color = null)
        {
            bool previous = GUI.enabled; GUI.enabled = previous && enabled;
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (GUI.enabled && r.Contains(Event.current.mousePosition)) Panel(r, Selected, Accent);
            var tint = enabled ? color ?? Text : new Color(Muted.r, Muted.g, Muted.b, .45f);
            IconImage(new Rect(r.center.x - 8, r.center.y - 8, 16, 16), name, tint);
            GUI.enabled = previous; return hit;
        }
        // a small inline text button ("rename", "E"): Small font, muted, accent on hover, boxed when `boxed`; vertically centred in r
        public static bool TextButton(Rect r, string text, bool boxed = false, bool enabled = true)
        {
            bool previous = GUI.enabled; GUI.enabled = previous && enabled;
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (boxed) Panel(r, hover ? Selected : Field, hover ? Accent : Line);
            var ink = !enabled ? new Color(Muted.r, Muted.g, Muted.b, .45f) : hover ? Accent : Muted;
            ButtonLabel(r, text, Small, ink, TextAnchor.MiddleCenter);
            GUI.enabled = previous; return hit;
        }
        public static float TextWidth(string text, GUIStyle style) { return style.CalcSize(new GUIContent(text)).x; }

        // two stacked triangles (up over down) in one column; returns -1 (up), +1 (down) or 0
        public static int Reorder(Rect column, bool canUp, bool canDown)
        {
            float half = column.height / 2;
            var up = new Rect(column.x, column.y, column.width, half); var down = new Rect(column.x, column.y + half, column.width, half);
            int result = 0;
            if (TriangleButton(up, true, canUp)) result = -1;
            if (TriangleButton(down, false, canDown)) result = 1;
            return result;
        }
        private static bool TriangleButton(Rect r, bool up, bool enabled)
        {
            bool previous = GUI.enabled; GUI.enabled = previous && enabled;
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none);
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            var tint = !enabled ? new Color(Muted.r, Muted.g, Muted.b, .35f) : hover ? Accent : Text;
            var old = GUI.color; GUI.color = tint; GUI.DrawTexture(new Rect(r.center.x - 6, r.center.y - 4, 12, 8), Triangle(up)); GUI.color = old;
            GUI.enabled = previous; return hit;
        }
        private static Texture2D _triUp, _triDown;
        private static Texture2D Triangle(bool up)
        {
            var t = up ? _triUp : _triDown; if (t != null) return t;
            const int W = 12, H = 8;
            t = new Texture2D(W, H, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < H; y++)
            {
                // row 0 is the bottom of the texture; the up triangle is widest at the bottom
                float rowFromBase = up ? y : H - 1 - y, halfWidth = (W / 2f) * (1f - rowFromBase / H);
                for (int x = 0; x < W; x++) { float d = Mathf.Abs(x + .5f - W / 2f); t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(halfWidth - d + .5f))); }
            }
            t.Apply(); if (up) _triUp = t; else _triDown = t; return t;
        }
        public static bool Dropdown(Rect r, string text)
        {
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none); Panel(r, Field, r.Contains(Event.current.mousePosition) && GUI.enabled ? Accent : Line);
            ButtonLabel(new Rect(r.x + 12, r.y, r.width - 40, r.height), text, Body, Text, TextAnchor.MiddleLeft);
            IconImage(new Rect(r.xMax - 27, r.center.y - 8, 16, 16), "chevron-down", Text); return hit;
        }
        public static void Rule(float x, float y, float width) { Fill(new Rect(x, y, width, 1), Line); }
        public static void Window(Rect r)
        {
            if (_settings)
            {
                if (_settingsPlate.normal.background != null) GUI.Box(new Rect(r.x - 40, r.y - 40, r.width + 80, r.height + 80), GUIContent.none, _settingsPlate);
                else Panel(r, Background, Accent);
                return;
            }
            Skin.RustBack(r); Fill(r, new Color(16 / 255f, 13 / 255f, 9 / 255f, .5f));
            // Stretch each edge independently exactly like the prototype's native game frame.
            if (Skin.RebarH != null)
            {
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 6), Skin.RebarH);
                GUI.DrawTexture(new Rect(r.x, r.yMax - 6, r.width, 6), Skin.RebarH);
                GUI.DrawTexture(new Rect(r.x, r.y, 6, r.height), Skin.RebarV);
                GUI.DrawTexture(new Rect(r.xMax - 6, r.y, 6, r.height), Skin.RebarV);
            }
            if (Skin.Rivet != null) foreach (var at in new[] { new Vector2(r.x + 10, r.y + 10), new Vector2(r.xMax - 18, r.y + 10), new Vector2(r.x + 10, r.yMax - 18), new Vector2(r.xMax - 18, r.yMax - 18) }) GUI.DrawTexture(new Rect(at.x, at.y, 8, 8), Skin.Rivet);
        }
        public static void Header(float width) { Header(width, "APOCAPATROL"); }
        public static void Header(float width, string title)
        {
            Fill(new Rect(6, 6, width - 12, 74), new Color(16 / 255f, 13 / 255f, 9 / 255f, .65f));
            Label(new Rect(26, 24, 600, 38), title, Brand, Accent);
            Rule(6, 79, width - 12);
        }
        public static bool Toggle(Rect r, bool value, string label)
        {
            if (_settings)
            {
                bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
                bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
                RowHighlight(r);
                float x = r.x + r.width * ToolLayout.LabelWidth;
                Panel(new Rect(x, r.center.y - 14, 28, 28), Color.clear, Accent);
                if (value) Fill(new Rect(x + 6, r.center.y - 8, 16, 16), Accent);
                Label(new Rect(r.x + 22, r.y, x - r.x - 36, r.height), label, Body, !GUI.enabled ? Muted : hover ? Accent : Text);
                Label(new Rect(x + 44, r.y, 120, r.height), value ? "ON" : "OFF", GameButton, GUI.enabled ? Text : Muted);
                return clicked ? !value : value;
            }
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none); Panel(new Rect(r.x, r.center.y - 8, 16, 16), value ? Accent : Field);
            if (value) IconImage(new Rect(r.x + 1, r.center.y - 7, 14, 14), "check", Background);
            Label(new Rect(r.x + 24, r.y, r.width - 24, r.height), label); return hit ? !value : value;
        }
        public static float Number(Rect r, string key, float value, float min, float max, Action changed, bool right = false)
        {
            string text; if (!Numbers.TryGetValue(key, out text)) text = value.ToString("0.####", CultureInfo.InvariantCulture);
            var next = TextField(r, key, text, right); if (next == text) return value; Numbers[key] = next;
            float parsed; if ((!float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && !float.TryParse(next, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)) || float.IsNaN(parsed) || float.IsInfinity(parsed)) return value;
            var number = Mathf.Clamp(parsed, min, max); if (number != parsed) Numbers[key] = number.ToString("0.####", CultureInfo.InvariantCulture);
            if (number != value) changed(); return number;
        }
        public static void ClearNumbers() { Numbers.Clear(); }
        public static string Quantity(float chance)
        {
            int n = (int)Math.Floor(chance / 100f); float rest = chance % 100;
            return n + " guaranteed" + (rest > 0 ? " + " + rest.ToString("0.##", CultureInfo.InvariantCulture) + "% next" : "");
        }
        public static Vector2 Scroll(Rect r, Vector2 scroll, float height, Action<float> draw)
        {
            bool overflow = height > r.height; float width = r.width - (overflow ? 10 : 0);
            scroll = GUI.BeginScrollView(r, scroll, new Rect(0, 0, width, Mathf.Max(height, r.height)), false, overflow, GUIStyle.none, Scrollbar);
            draw(width); GUI.EndScrollView(); return scroll;
        }
    }

    // One popup implementation for spawning configuration and template creation.
    public sealed class LedgerDropdown
    {
        public bool IsOpen { get { return _options != null; } }
        private Rect _anchor, _box;
        private string _selected;
        private List<KeyValuePair<string, string>> _options;
        private Action<string> _setter;
        private Action _pending;
        private Vector2 _scroll;
        public void Open(Rect anchor, List<KeyValuePair<string, string>> options, string selected, Action<string> setter, float bottom)
        {
            var first = options.Where(o => string.IsNullOrEmpty(o.Key) || string.Equals(o.Value, "None", StringComparison.OrdinalIgnoreCase) || o.Value.StartsWith("Convoy cargo", StringComparison.OrdinalIgnoreCase)).ToList();
            options = first.Concat(options.Except(first).OrderBy(o => o.Value, StringComparer.OrdinalIgnoreCase)).ToList();
            _pending = () =>
            {
                _anchor = anchor; _options = options; _selected = selected; _setter = setter; _scroll = Vector2.zero;
                float height = Mathf.Min(196, 8 + options.Count * 34), y = anchor.yMax + 4;
                if (y + height > bottom) y = anchor.y - height - 4;
                _box = new Rect(anchor.x, Mathf.Max(10, y), anchor.width, height);
            };
        }
        // Called first thing in the window: while the list is open every mouse event belongs to it. IMGUI gives a click to the first control
        // drawn under the mouse, and the list is drawn last (on top) - so a click on one of its rows used to land on whatever row of the
        // dialog lay beneath it (2.3.2). The rows are hit-tested here and the event is used up; Draw() only paints.
        public void Before()
        {
            if (!IsOpen) return;
            var e = Event.current;
            bool inside = _box.Contains(e.mousePosition);
            if (e.type == EventType.MouseDown)
            {
                if (inside)
                {
                    if(e.button==0)SelectAt(e.mousePosition);
                }
                else _pending = Close;
                e.Use();
            }
            else if (e.type == EventType.MouseUp || e.type == EventType.MouseDrag) { if (inside || _pending != null) e.Use(); }
            else if (e.type == EventType.ScrollWheel && inside)
            {
                float view = _box.height - 8, content = _options.Count * 34;
                _scroll.y = Mathf.Clamp(_scroll.y + e.delta.y * 20f, 0f, Mathf.Max(0f, content - view));
                e.Use();
            }
        }
        public void Draw()
        {
            if (!IsOpen) return;
            Ledger.Panel(_box, Ledger.Surface, Ledger.Accent);
            Ledger.Scroll(new Rect(_box.x + 4, _box.y + 4, _box.width - 8, _box.height - 8), _scroll, _options.Count * 34, width =>
            {
                var mouse = Event.current.mousePosition;
                for (int i = 0; i < _options.Count; i++)
                {
                    var option = _options[i]; var row = new Rect(0, i * 34, width, 34); bool selected = option.Key == _selected;
                    if (selected || row.Contains(mouse)) Ledger.Fill(row, Ledger.Selected);
                    Ledger.ButtonLabel(new Rect(9, row.y, width - 18, 34), option.Value, Ledger.Body, selected ? Ledger.Accent : Ledger.Text, TextAnchor.MiddleLeft);
                }
            });
        }
        public bool SelectAt(Vector2 mouse)
        {
            if(!IsOpen||!_box.Contains(mouse))return false;
            int i=Mathf.FloorToInt((mouse.y-(_box.y+4)+_scroll.y)/34f);if(i<0||i>=_options.Count)return false;
            string key=_options[i].Key;var setter=_setter;_pending=()=>{setter(key);Close();};return true;
        }
        public void Flush() { if (_pending != null) { var action = _pending; _pending = null; action(); } }
        public void Close() { _options = null; _pending = null; }
    }
}
