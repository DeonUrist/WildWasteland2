using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class QuestDialogue
    {
        static GameObject _root, _window;
        static GlyphText _name, _prompt;
        static RectTransform _chat, _choices;
        static ScrollRect _chatScroll, _choiceScroll;
        static readonly List<GlyphText> Lines = new List<GlyphText>();
        static readonly List<MenuButton> Buttons = new List<MenuButton>();
        static readonly List<List<GlyphText>> Labels = new List<List<GlyphText>>();
        static readonly DialoguePointer Pointer = new DialoguePointer();
        static MenuButton _close;
        static int _revision = -1;
        static IQuestConversation _source;
        internal static bool Shown { get { return _root != null && _root.activeSelf && CleanHudModule.Running && CleanHudModule.Enabled.Value; } }
        static void Build()
        {
            _root = new GameObject("CleanHUD.QuestDialogue", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _root.hideFlags = HideFlags.HideAndDontSave; UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32001;
            var scale = _root.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1920, 1080); scale.matchWidthOrHeight = 1;
            var panel = DialogueWidgets.Rect(_root.transform, "window", 0, 0, 1200, 900); _window = panel.gameObject;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            var plate = panel.gameObject.AddComponent<Image>(); plate.sprite = Menu.PlateSprite(); plate.type = Image.Type.Sliced;
            _name = DialogueWidgets.Text(panel, "name", 50, 35, 1100, 60, 42, DialogueWidgets.SpeakerGray, "");
            _chatScroll = DialogueWidgets.Scroll(panel, "conversation", 50, 110, 1100, 420, out _chat);
            _choiceScroll = DialogueWidgets.Scroll(panel, "choices", 50, 555, 1100, 255, out _choices);
            _close = DialogueWidgets.Button(panel, 900, 820, 250, 60, "CLOSE", Close);
            _prompt = DialogueWidgets.Text(_root.transform, "prompt", 0, 0, 1000, 60, 28, Color.white, "");
            _prompt.rectTransform.anchorMin = _prompt.rectTransform.anchorMax = _prompt.rectTransform.pivot = new Vector2(.5f, .35f);
            _prompt.rectTransform.anchoredPosition = Vector2.zero; _prompt.Align = TextAnchor.MiddleCenter;
        }
        static void Close() { if (_source != null) _source.Close(); }
        internal static void Tick()
        {
            var api = Services.Quests; var source = api != null ? api.Conversation : null;
            if (!CleanHudModule.Enabled.Value || source == null || !api.Ready || (!source.Open && string.IsNullOrEmpty(source.Prompt))) { Hide(); return; }
            if (_root == null) Build();
            if (!ReferenceEquals(_source, source)) { Pointer.Clear(); _source = source; _revision = -1; }
            if (!_root.activeSelf) { _root.SetActive(true); _revision = -1; }
            _window.SetActive(source.Open); _prompt.gameObject.SetActive(!source.Open); _prompt.Set(source.Prompt ?? "");
            if (!source.Open) { Pointer.Clear(); return; }
            _name.Set(source.Name);
            if (_revision != source.Revision) { Refresh(source); _revision = source.Revision; Pointer.Clear(); }
            Pointer.Tick(Buttons, _close);
            DialoguePointer.Wheel(_chatScroll); DialoguePointer.Wheel(_choiceScroll);
            Menu.BlockCancelFrame = Time.frameCount + 2;
        }
        static void Refresh(IQuestConversation source)
        {
            float y = 0; int row = 0;
            foreach (string paragraph in source.Chat)
            {
                string text = paragraph.Replace("<b>", "").Replace("</b>", "");
                if (!text.StartsWith("You:", StringComparison.OrdinalIgnoreCase) && !text.StartsWith(source.Name + ":", StringComparison.OrdinalIgnoreCase)) text = source.Name + ": " + text;
                foreach (string line in TalkDialogue.Wrap(text, 1060))
                {
                    if (row == Lines.Count) Lines.Add(DialogueWidgets.Text(_chat, "line", 10, y, 1060, 32, 26, Color.white, ""));
                    var label = Lines[row++]; label.rectTransform.anchoredPosition = new Vector2(10, -y); label.Set(line); label.gameObject.SetActive(true); y += 32;
                    DialogueWidgets.Speaker(label, source.Name);
                }
                y += 14;
            }
            for (int i = row; i < Lines.Count; i++) Lines[i].gameObject.SetActive(false);
            _chat.sizeDelta = new Vector2(1100, y); _chatScroll.verticalNormalizedPosition = 0;
            y = 0;
            for (int i = 0; i < source.Choices.Count; i++)
            {
                if (i == Buttons.Count)
                {
                    var button = DialogueWidgets.Button(_choices, 0, y, 1080, 36, "", delegate { });
                    Buttons.Add(button); Labels.Add(new List<GlyphText> { button.GetComponentInChildren<GlyphText>(true) });
                }
                var choice = source.Choices[i]; var b = Buttons[i]; var labels = Labels[i];
                string key = choice.Key;
                b.onClick.RemoveAllListeners(); b.onClick.AddListener(delegate { if (_source == source && source.Open) source.Choose(key); });
                var lines = TalkDialogue.Wrap(choice.Text, 1040, 20); float h = Mathf.Max(36, lines.Count * 24 + 8);
                var rect = (RectTransform)b.transform; rect.anchoredPosition = new Vector2(0, -y); rect.sizeDelta = new Vector2(1080, h);
                for (int n = 0; n < lines.Count; n++)
                {
                    if (n == labels.Count) labels.Add(DialogueWidgets.Text(b.transform, "text", 20, 4 + n * 24, 1040, 24, 20, DialogueWidgets.Gold, ""));
                    var label = labels[n]; label.Size = 20; label.rectTransform.anchoredPosition = new Vector2(20, -4 - n * 24);
                    label.rectTransform.sizeDelta = new Vector2(1040, 24); label.Set(lines[n]); label.gameObject.SetActive(true);
                }
                for (int n = lines.Count; n < labels.Count; n++) labels[n].gameObject.SetActive(false);
                b.OnVisual = hover => { foreach (var label in labels) label.color = !b.interactable ? Color.gray : hover ? Color.white : DialogueWidgets.Gold; };
                b.gameObject.SetActive(true); b.Refresh(); y += h + 4;
            }
            for (int i = source.Choices.Count; i < Buttons.Count; i++) Buttons[i].gameObject.SetActive(false);
            _choices.sizeDelta = new Vector2(1100, y);
        }
        internal static void Hide() { Pointer.Clear(); if (_root != null) _root.SetActive(false); _source = null; _revision = -1; }
        internal static void Dispose()
        {
            Hide(); if (_root != null) UnityEngine.Object.Destroy(_root); _root = _window = null; Lines.Clear(); Buttons.Clear(); Labels.Clear(); _close = null;
        }
    }

    // Apocasetter's modal blocker disables EventSystem.Update and Button.Press. Dispatch only owned dialogue buttons.
    internal sealed class DialoguePointer
    {
        MenuButton _pressed, _hovered;
        internal void Clear() { if (_hovered != null && _hovered.OnVisual != null) _hovered.OnVisual(false); _pressed = _hovered = null; }
        internal void Tick(List<MenuButton> buttons, MenuButton close)
        {
            if (!ApocasetterBridge.InputBlocked || !Application.isFocused) { Clear(); return; }
            MenuButton hover = Visible(close) ? close : null;
            foreach (var b in buttons) if (Visible(b)) { hover = b; break; }
            if (_hovered != hover)
            {
                if (_hovered != null && _hovered.OnVisual != null) _hovered.OnVisual(false);
                _hovered = hover; if (hover != null && hover.OnVisual != null) hover.OnVisual(true);
            }
            if (Input.GetMouseButtonDown(0)) _pressed = hover;
            if (Input.GetMouseButtonUp(0))
            {
                var press = _pressed; _pressed = null;
                if (press != null && press == hover && press.IsInteractable()) press.onClick.Invoke();
            }
        }
        static bool Visible(MenuButton button)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable()) return false;
            if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, Input.mousePosition)) return false;
            for (var p = button.transform.parent; p != null; p = p.parent)
                if ((p.GetComponent<RectMask2D>() != null || p.GetComponent<Mask>() != null)
                    && !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)p, Input.mousePosition)) return false;
            return true;
        }
        internal static void Wheel(ScrollRect scroll)
        {
            if (!ApocasetterBridge.InputBlocked || !Application.isFocused || scroll == null || Input.mouseScrollDelta.y == 0) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, Input.mousePosition)) return;
            float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
            if (overflow > 0) scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + Input.mouseScrollDelta.y * 80 / overflow);
        }
    }
}
