using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
{
    // Mirror the source UI's choices and callbacks, including Quests' AQ buttons. The source remains the dialogue model.
    internal static class TalkDialogue
    {
        private static object _plugin, _ui;
        private static Func<object, object> _getUi;
        private static Func<object, bool> _talking;
        private static Func<object, RectTransform> _panel, _sourceChat, _sourceChoices;
        private static Func<object, string> _npcName;
        private static Action<object> _close;
        private static GameObject _root;
        private static CanvasGroup _hidden;
        private static bool _ownsGroup;
        private static float _oldAlpha;
        private static bool _oldRays;
        private static GlyphText _title;
        private static RectTransform _chat, _choices;
        private static ScrollRect _chatScroll, _choiceScroll;
        private static MenuButton _closeButton;
        private static readonly DialoguePointer Pointer = new DialoguePointer();
        private static int _revision, _shownRevision = -1, _choiceHash;
        private static int _chatHash;
        private static readonly List<MenuButton> Buttons = new List<MenuButton>();
        private static readonly List<Button> Sources = new List<Button>();
        private static readonly Regex Tags = new Regex("<[^>]*>");

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(CleanHudModule owner)
        {
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.example.apocalypter.talk", out info) || ReferenceEquals(info.Instance, null)) return;
            if (info.Metadata.Version != new Version(2, 0, 0)) CleanHudModule.Warn("Apocatalker " + info.Metadata.Version + " differs from the tested 2.0.0; attempting dialogue compatibility with this version");
            _plugin = info.Instance; var p = _plugin.GetType(); var u = p.Assembly.GetType("ApocalypterTalk.DialogueUI", true);
            _getUi = Get<object>(p, "dialogueUI"); _talking = Get<bool>(p, "Talking"); _npcName = Get<string>(p, "npcName");
            _panel = Get<RectTransform>(u, "panel"); _sourceChat = Get<RectTransform>(u, "chatContent"); _sourceChoices = Get<RectTransform>(u, "promptContent");
            var o = Expression.Parameter(typeof(object), "o");
            _close = Expression.Lambda<Action<object>>(Expression.Call(Expression.Convert(o, p), AccessTools.Method(p, "Close")), o).Compile();
            foreach (var method in new[] { "ShowDialogue", "AppendChatLine", "SetChoices" })
                if (!Core.Patches.Patch(owner, AccessTools.Method(u, method), postfix: AccessTools.Method(typeof(TalkDialogue), "Changed")))
                    throw new InvalidOperationException("Dialogue hook unavailable: " + method);
            _revision = 0; _shownRevision = -1; _choiceHash = 0;
            CleanHudModule.Log("Apocatalker dialogue bridge ready; native window remains the CleanHUD-off fallback");
        }
        private static Func<object, T> Get<T>(Type type, string name)
        {
            var o = Expression.Parameter(typeof(object), "o"); var field = AccessTools.Field(type, name);
            return Expression.Lambda<Func<object, T>>(Expression.Convert(Expression.Field(field.IsStatic ? null : Expression.Convert(o, type), field), typeof(T)), o).Compile();
        }
        private static void Changed() { _revision++; }
        private static void Build()
        {
            _root = new GameObject("CleanHUD.Dialogue", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _root.hideFlags = HideFlags.HideAndDontSave; UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32001;
            var scale = _root.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1920, 1080); scale.matchWidthOrHeight = 1;
            var panel = DialogueWidgets.Rect(_root.transform, "window", 0, 0, 1200, 900);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            var plate = panel.gameObject.AddComponent<Image>(); plate.sprite = Menu.PlateSprite(); plate.type = Image.Type.Sliced;
            _title = DialogueWidgets.Text(panel, "name", 50, 35, 1100, 60, 42, DialogueWidgets.SpeakerGray, "");
            _chatScroll = DialogueWidgets.Scroll(panel, "conversation", 50, 110, 1100, 420, out _chat);
            RectTransform content; _choiceScroll = DialogueWidgets.Scroll(panel, "choices", 50, 555, 1100, 255, out content); _choices = content;
            _closeButton = DialogueWidgets.Button(panel, 900, 820, 250, 60, "CLOSE", () => _close(_plugin));
        }
        internal static void Tick()
        {
            if (_plugin == null) return;
            if (!CleanHudModule.Enabled.Value || !_talking(_plugin)) { Hide(); return; }
            var ui = _getUi(_plugin); if (ui == null || _panel(ui) == null) { Hide(); return; }
            if (!ReferenceEquals(ui, _ui)) { RestoreSource(); _ui = ui; _shownRevision = -1; _choiceHash = 0; }
            if (_root == null) Build();
            if (!_root.activeSelf) { _root.SetActive(true); _shownRevision = -1; _choiceHash = 0; }
            if (_hidden == null)
            {
                _hidden = _panel(ui).GetComponent<CanvasGroup>(); _ownsGroup = _hidden == null;
                if (_hidden == null) _hidden = _panel(ui).gameObject.AddComponent<CanvasGroup>();
                _oldAlpha = _hidden.alpha; _oldRays = _hidden.blocksRaycasts;
            }
            _hidden.alpha = 0; _hidden.blocksRaycasts = false;
            _title.Set(_npcName(_plugin));
            var chatSource = _sourceChat(ui);
            int chatHash = chatSource.childCount;
            if (chatSource.childCount > 0) chatHash ^= chatSource.GetChild(0).GetInstanceID() ^ chatSource.GetChild(chatSource.childCount - 1).GetInstanceID();
            if (_shownRevision != _revision || _chatHash != chatHash)
            {
                _chatHash = chatHash;
                _shownRevision = _revision; Clear(_chat); float y = 0;
                var source = _sourceChat(ui);
                for (int i = 0; i < source.childCount; i++)
                {
                    var text = source.GetChild(i).GetComponent<TMP_Text>();
                    if (text != null) y = Lines(_chat, Tags.Replace(text.text ?? "", ""), y, 1060, Color.white) + 14;
                }
                _chat.sizeDelta = new Vector2(1100, y); _chatScroll.verticalNormalizedPosition = 0;
            }
            var choices = _sourceChoices(ui); int hash = choices.childCount;
            for (int i = 0; i < choices.childCount; i++) hash = unchecked(hash * 31 + choices.GetChild(i).GetInstanceID());
            if (hash != _choiceHash)
            {
                Pointer.Clear();
                _choiceHash = hash; Clear(_choices); Buttons.Clear(); Sources.Clear(); float y = 0;
                for (int i = 0; i < choices.childCount; i++)
                {
                    var source = choices.GetChild(i).GetComponent<Button>(); if (source == null || !source.gameObject.activeSelf) continue;
                    var text = source.GetComponentInChildren<TMP_Text>(true); if (text == null) continue;
                    var lines = Wrap(Tags.Replace(text.text ?? "", ""), 1040, 20);
                    var button = DialogueWidgets.Button(_choices, 0, y, 1080, Math.Max(36, lines.Count * 24 + 8), "", () => { if (source != null && source.interactable) source.onClick.Invoke(); });
                    var labels = new List<GlyphText>(); float ly = 4;
                    foreach (var line in lines) { labels.Add(DialogueWidgets.Text(button.transform, "text", 20, ly, 1040, 24, 20, DialogueWidgets.Gold, line)); ly += 24; }
                    button.OnVisual = on => { foreach (var label in labels) label.color = !button.interactable ? Color.gray : on ? Color.white : DialogueWidgets.Gold; };
                    Buttons.Add(button); Sources.Add(source); y += button.GetComponent<RectTransform>().sizeDelta.y + 4;
                }
                _choices.sizeDelta = new Vector2(1100, y);
            }
            Pointer.Tick(Buttons, _closeButton); DialoguePointer.Wheel(_chatScroll); DialoguePointer.Wheel(_choiceScroll);
            for (int i = 0; i < Buttons.Count; i++) { Buttons[i].interactable = Sources[i] != null && Sources[i].interactable; Buttons[i].Refresh(); }
        }
        internal static List<string> Wrap(string text, float width, float size = 26)
        {
            var lines = new List<string>(); var font = GlyphFont.Get();
            foreach (var paragraph in text.Replace("\r", "").Split('\n'))
            {
                var line = new StringBuilder();
                foreach (var word in paragraph.Split(' '))
                {
                    if (line.Length > 0 && font.Measure(line.ToString() + " " + word, size, 0) > width) { lines.Add(line.ToString()); line.Length = 0; }
                    if (line.Length > 0) line.Append(' '); line.Append(word);
                }
                lines.Add(line.ToString());
            }
            return lines;
        }
        private static float Lines(Transform parent, string text, float y, float width, Color color)
        {
            foreach (var line in Wrap(text, width)) { var label = DialogueWidgets.Text(parent, "line", 10, y, width, 32, 26, color, line); DialogueWidgets.Speaker(label, _npcName(_plugin)); y += 32; }
            return y;
        }
        private static void Clear(Transform parent)
        { for (int i = parent.childCount - 1; i >= 0; i--) { var go = parent.GetChild(i).gameObject; go.SetActive(false); UnityEngine.Object.Destroy(go); } }
        private static void RestoreSource()
        {
            if (_hidden != null) { _hidden.alpha = _oldAlpha; _hidden.blocksRaycasts = _oldRays; if (_ownsGroup) UnityEngine.Object.Destroy(_hidden); }
            _hidden = null;
        }
        internal static void Hide() { Pointer.Clear(); if (_root != null) _root.SetActive(false); RestoreSource(); }
        internal static void Dispose()
        { Hide(); if (_root != null) UnityEngine.Object.Destroy(_root); _root = null; _plugin = _ui = null; Buttons.Clear(); Sources.Clear(); }
    }
}
