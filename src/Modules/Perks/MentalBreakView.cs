using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Perks
{
    /// <summary>Required choice modal; uses the same input lease as the inventory and works with HUD disabled.</summary>
    internal sealed class MentalBreakView
    {
        private readonly EditorSession _session = new EditorSession();
        private readonly RectTransform[] _choices = new RectTransform[4];
        private readonly UnityEngine.UI.Image[] _cards = new UnityEngine.UI.Image[4];
        private GameObject _root;
        private int _pressed = -1;
        internal void Init(IModule owner)
        {
            Scheduler.EveryFrame(owner, "Perks.Mental break choice", Tick, Phase.LateUpdateLast);
            Hooks.GetButtonDownUpdate.Prefix(owner, ButtonDown);
            Hooks.GetButtonDo.Prefix(owner, ButtonHeld);
            Hooks.GetButtonUpUpdate.Prefix(owner, ButtonUp);
            Hooks.GetMouseButtonDownUpdate.Prefix(owner, MouseDown);
            Hooks.MouseLookDo.Prefix(owner, Look);
        }
        private bool ButtonDown(GetButtonDown action) { return !_session.Open; }
        private bool ButtonHeld(GetButton action) { return !_session.Open; }
        private bool ButtonUp(GetButtonUp action) { return !_session.Open; }
        private bool MouseDown(GetMouseButtonDown action) { return !_session.Open; }
        private bool Look(MouseLook action) { return !_session.Open; }
        private void Tick()
        {
            var mental = ModApi.Get<IMentalBreak>();
            if (mental == null || !mental.Pending || !GameRefs.InPlay) { Close(); return; }
            if (!_session.Open)
            {
                if (!_session.Acquire()) return;
                ModApi.Get<IPlayerPerks>()?.PrepareInsanityChoices();
                if (_root == null) Build();
                _root.SetActive(true); _pressed = -1;
            }
            _session.Maintain();
            if (!Application.isFocused) { _pressed = -1; return; }
            int hover = -1;
            for (int i = 0; i < _choices.Length; i++)
            {
                bool over = RectTransformUtility.RectangleContainsScreenPoint(_choices[i], Input.mousePosition, null);
                _cards[i].color = over ? new Color(.22f, .20f, .14f, 1) : new Color(.075f, .075f, .06f, 1);
                if (over) hover = i;
            }
            if (Input.GetMouseButtonDown(0)) _pressed = hover;
            if (Input.GetMouseButtonUp(0))
            {
                int pressed = _pressed; _pressed = -1;
                if (pressed >= 0 && pressed == hover && mental.Choose(pressed)) Close();
            }
        }
        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
        }
        private static void Label(Transform parent, string text, float x, float y, float w, float h, float size, Color color)
        {
            var label = Rect(parent, "label", x, y, w, h).gameObject.AddAccent<GlyphText>();
            label.Size = size; label.color = color; label.Align = TextAnchor.MiddleCenter; label.raycastTarget = false; label.Set(text);
        }
        private void Build()
        {
            _root = new GameObject("Perks.MentalBreak", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            _root.transform.SetParent(Runner.Host.transform, false);
            var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32500;
            var scaler = _root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var area = Rect(_root.transform, "modal", 0, 0, 1920, 1080);
            area.anchorMin = area.anchorMax = area.pivot = new Vector2(.5f, .5f); area.anchoredPosition = Vector2.zero;
            var shade = area.gameObject.AddComponent<UnityEngine.UI.Image>(); shade.color = new Color(0, 0, 0, .94f);
            var accent = ModApi.Get<IUiTheme>()?.Accent ?? ThemePalette.Gold;
            Label(area, "MENTAL BREAK", 360, 192, 1200, 72, 50, accent);
            Label(area, "Choose an insanity perk or persevere", 360, 274, 1200, 46, 27, Color.white);
            var perks = ModApi.Get<IPlayerPerks>();
            for (int i = 0; i < _choices.Length; i++)
            {
                var perk = perks != null ? perks.InsanityChoice(i) : new PlayerPerk();
                _choices[i] = Rect(area, "Cannibal choice " + (i + 1), 224 + i * 374, 350, 350, 430);
                _cards[i] = _choices[i].gameObject.AddComponent<UnityEngine.UI.Image>(); _cards[i].color = new Color(.075f, .075f, .06f);
                var line = Rect(_choices[i], "accent", 0, 0, 350, 4).gameObject.AddComponent<UnityEngine.UI.Image>(); line.color = perk.Insanity ? new Color(.85f, .16f, .12f, 1) : accent;
                var icon = Rect(_choices[i], "Cannibal", 103, 35, 144, 144).gameObject.AddComponent<UnityEngine.UI.RawImage>();
                icon.texture = perk.Icon; icon.enabled = perk.Icon != null; icon.raycastTarget = false;
                float titleY = 194;
                foreach (var text in GlyphFont.Wrap((perk.Name ?? "").ToUpperInvariant(), 310, 26))
                { Label(_choices[i], text, 20, titleY, 310, 31, 26, accent); titleY += 31; }
                float descriptionY = 267;
                foreach (var text in GlyphFont.Wrap(perk.Hint ?? "", 300, 19))
                { Label(_choices[i], text, 25, descriptionY, 300, 24, 19, Color.white); descriptionY += 24; }
            }
            Label(area, "Insanity: 0 after a perk   |   35 after Persevere", 360, 816, 1200, 42, 23, Color.white);
        }
        private void Close()
        {
            if (_root != null) { _root.SetActive(false); Object.Destroy(_root); _root = null; }
            _pressed = -1; if (_session.Open) _session.Close();
        }
        internal void Clear()
        {
            Close(); if (ReferenceEquals(EditorSession.Owner, _session)) _session.Close(true);
            if (_root != null) Object.Destroy(_root); _root = null;
        }
    }
}
