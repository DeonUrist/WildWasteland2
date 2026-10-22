using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
{
    internal sealed partial class Menu
    {
        private Canvas _referenceCanvas;
        private static readonly string[] ReferenceNames = { "Tutorial", "Codex" };
        private Transform _referenceNative, _referencePage;
        private readonly List<Transform> _referencePages = new List<Transform>();
        private readonly List<RawImage> _referenceImages = new List<RawImage>();
        private Text[] _referenceTexts;
        private RectTransform _referenceContent;
        private MenuScrollRect _referenceScroll;
        private GlyphText _referenceTitle, _referenceCounter;
        private bool _referenceTutorial;
        private int _referenceSignature;
        private float _referenceAt, _referenceSince, _referenceHeight;

        private void ShowReference(bool tutorial, bool alreadyOpen = false)
        {
            string name = tutorial ? "Tutorial" : "Codex";
            var source = SettingsModel.FindPath(name + "_Canvas");
            var canvas = source != null ? source.GetComponent<Canvas>() : null;
            var native = source != null ? source.Find(name) : null;
            if (canvas == null || native == null) { CleanHudModule.Warn("Menu: " + name + " content unavailable"); return; }
            ResetToMain();
            if (!alreadyOpen && !Click("MainMenu_Canvas/MainMenu/" + name)) return;
            _referenceCanvas = canvas; _referenceNative = native; _referenceTutorial = tutorial;
            foreach (Transform child in native)
                if (tutorial ? child.name.StartsWith("Page_", System.StringComparison.Ordinal) : child.Find("content") != null) _referencePages.Add(child);
            HideGo(source);
            _screen = "reference"; _main.gameObject.SetActive(false);
            _sub = R(_rootRt, name, 0, 0, 1920, 1080);
            float top = tutorial ? 60 : 280, height = tutorial ? 960 : 700;
            _referenceHeight = height - 200;
            var heading = _rootRt.Find("head"); if (heading != null) heading.gameObject.SetActive(!tutorial);
            Plate(_sub, 120, top, 1680, height);
            _referenceTitle = T(_sub, "heading", 154, top + 18, 1300, 48, 38, TextAnchor.MiddleLeft, Gold, name.ToUpperInvariant());
            _referenceCounter = T(_sub, "page", 1500, top + 18, 260, 48, 26, TextAnchor.MiddleRight, Dim, "");
            var view = R(_sub, "pages", 154, top + 90, 1612, _referenceHeight);
            view.gameObject.AddComponent<RectMask2D>();
            Img(view, "hit", 0, 0, 1612, _referenceHeight, Color.clear, true);
            _referenceContent = R(view, "content", 0, 0, 1580, _referenceHeight);
            _referenceScroll = view.gameObject.AddComponent<MenuScrollRect>();
            _referenceScroll.content = _referenceContent; _referenceScroll.viewport = view;
            _referenceScroll.horizontal = false; _referenceScroll.vertical = true; _referenceScroll.inertia = false;
            _referenceScroll.movementType = ScrollRect.MovementType.Clamped; _referenceScroll.scrollSensitivity = 70;
            var track = Img(view, "track", 1594, 0, 18, _referenceHeight, new Color(Gold.r, Gold.g, Gold.b, .15f), true);
            var thumb = Img(track.transform, "thumb", 0, 0, 0, 0, Gold, true).rectTransform;
            thumb.pivot = new Vector2(.5f, .5f); thumb.sizeDelta = new Vector2(-10, 0);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop; scrollbar.handleRect = thumb; scrollbar.targetGraphic = thumb.GetComponent<Image>();
            scrollbar.transition = Selectable.Transition.None; _referenceScroll.verticalScrollbar = scrollbar;
            Row(_sub, 154, top + height - 78, 270, 60, "BACK", 30, Back);
            Row(_sub, 1080, top + height - 78, 320, 60, "PREVIOUS", 30, () => TurnReference(false));
            Row(_sub, 1450, top + height - 78, 290, 60, "NEXT", 30, () => TurnReference(true));
            _referenceAt = 0; _referenceSince = Time.unscaledTime;
            RefreshReference();
            CleanHudModule.Log("Menu: opened native " + name + " in CleanHUD (" + _referencePages.Count + " pages)");
        }

        private void TickReference()
        {
            if (_referenceCanvas != null)
            {
                if (!_referenceCanvas.enabled || !_referenceCanvas.gameObject.activeInHierarchy)
                { if (Time.unscaledTime - _referenceSince >= .3f) Back(); return; }
                if (Time.unscaledTime >= _referenceAt) { _referenceAt = Time.unscaledTime + .2f; RefreshReference(); }
                return;
            }
            if (_ctx != Ctx.Pause || _screen != "main") return;
            if (Time.unscaledTime < _referenceAt) return;
            _referenceAt = Time.unscaledTime + .2f;
            foreach (string name in ReferenceNames)
            {
                var source = SettingsModel.FindPath(name + "_Canvas");
                var canvas = source != null ? source.GetComponent<Canvas>() : null;
                if (canvas != null && canvas.enabled && canvas.gameObject.activeInHierarchy)
                { ShowReference(name == "Tutorial", true); return; }
            }
        }

        private void RefreshReference()
        {
            Transform page = null;
            foreach (var candidate in _referencePages) if (candidate != null && candidate.gameObject.activeSelf) { page = candidate; break; }
            if (page == null) return;
            bool changed = page != _referencePage;
            if (changed)
            {
                _referencePage = page; _referenceImages.Clear();
                _referenceTexts = page.GetComponentsInChildren<Text>(true);
                foreach (var image in page.GetComponentsInChildren<RawImage>(true))
                    if (image.GetComponentInParent<Button>() == null) _referenceImages.Add(image);
            }
            int signature = 17;
            unchecked
            {
                foreach (var text in _referenceTexts) if (text != null)
                    signature = signature * 31 + (text.text ?? "").GetHashCode() + (text.gameObject.activeInHierarchy ? 1 : 0);
                foreach (var image in _referenceImages) if (image != null)
                    signature = signature * 31 + (image.texture != null ? image.texture.GetInstanceID() : 0) + image.uvRect.GetHashCode();
                signature = signature * 31 + (CleanHudModule.CleanFont.Value ? 1 : 0);
            }
            if (!changed && signature == _referenceSignature) return;
            _referenceSignature = signature;
            float scroll = changed ? 1f : _referenceScroll.verticalNormalizedPosition;
            for (int i = _referenceContent.childCount - 1; i >= 0; i--)
            { var child = _referenceContent.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child); }
            var title = page.Find("codex_name_text"); var nativeTitle = title != null ? title.GetComponent<Text>() : null;
            _referenceTitle.Set(_referenceTutorial ? "TUTORIAL" : "CODEX / " + (nativeTitle != null ? nativeTitle.text : page.name));
            _referenceCounter.Set((_referencePages.IndexOf(page) + 1) + " / " + _referencePages.Count);
            float height = _referenceTutorial ? TutorialContent(page) : CodexContent(page);
            _referenceContent.sizeDelta = new Vector2(1580, Mathf.Max(_referenceHeight, height));
            _referenceScroll.vertical = height > _referenceHeight;
            _referenceScroll.verticalScrollbar.gameObject.SetActive(_referenceScroll.vertical);
            _referenceScroll.verticalNormalizedPosition = scroll;
        }

        private float TutorialContent(Transform page)
        {
            int topic = 0; float row = 0, bottom = 0;
            foreach (Transform child in page)
            {
                var title = child.GetComponent<Text>(); if (title == null || !child.gameObject.activeInHierarchy) continue;
                if (topic > 0 && topic % 2 == 0) row = bottom + 30;
                float x = topic % 2 * 804, y = row;
                y += ReferenceLines(title.text, x, y, 776, 30, Gold) + 16;
                foreach (var text in child.GetComponentsInChildren<Text>(true))
                    if (text != title && text.gameObject.activeInHierarchy) y += ReferenceLines(text.text, x, y, 776, 26, White) + 12;
                float pictureHeight = Mathf.Max(1, Mathf.Min(360, _referenceHeight - (y - row) - 40));
                if (topic < _referenceImages.Count) y += ReferenceImage(_referenceImages[topic], x, y + 14, 776, pictureHeight) + 14;
                bottom = Mathf.Max(bottom, y); topic++;
            }
            for (int i = topic; i < _referenceImages.Count; i++) bottom += ReferenceImage(_referenceImages[i], 0, bottom + 20, 1580, 500) + 20;
            return bottom + 12;
        }

        private float CodexContent(Transform page)
        {
            var content = page.Find("content"); if (content == null) return 0;
            int index = 0, rows = Mathf.Max(1, (content.childCount + 4) / 5); float bottom = 0;
            float cardHeight = Mathf.Min(236, (_referenceHeight - 12) / rows - 8);
            foreach (Transform entry in content)
            {
                var image = entry.GetComponent<RawImage>(); if (image == null) continue;
                float x = index % 5 * 320, y = index / 5 * (cardHeight + 8), captionHeight = 0;
                var texts = entry.GetComponentsInChildren<Text>(true);
                foreach (var text in texts) if (text.gameObject.activeInHierarchy) captionHeight += TalkDialogue.Wrap(text.text, 276, 23).Count * 23 * 1.3f;
                float captionY = y + cardHeight - 12 - captionHeight;
                Frame(_referenceContent, x, y, 300, cardHeight, 1, new Color(Gold.r, Gold.g, Gold.b, .4f));
                ReferenceImage(image, x + 12, y + 12, 276, Mathf.Max(1, Mathf.Min(160, captionY - y - 24)));
                foreach (var text in texts)
                    if (text.gameObject.activeInHierarchy) captionY += ReferenceLines(text.text, x + 12, captionY, 276, 23, White);
                bottom = y + cardHeight; index++;
            }
            return bottom + 12;
        }

        private float ReferenceLines(string text, float x, float y, float width, float size, Color color)
        {
            float height = 0;
            foreach (string line in TalkDialogue.Wrap(text ?? "", width, size))
            {
                T(_referenceContent, "line", x, y + height, width, size * 1.3f, size, TextAnchor.UpperLeft, color, line);
                height += size * 1.3f;
            }
            return height;
        }

        private float ReferenceImage(RawImage source, float x, float y, float width, float maxHeight)
        {
            if (source == null || source.texture == null) return 0;
            float aspect = source.texture.width / (float)Mathf.Max(1, source.texture.height);
            float height = Mathf.Min(maxHeight, width / Mathf.Max(.01f, aspect)), w = height * aspect;
            var image = R(_referenceContent, "picture", x + (width - w) / 2, y, w, height).gameObject.AddComponent<RawImage>();
            image.texture = source.texture; image.uvRect = source.uvRect; image.color = source.color; image.raycastTarget = false;
            return height;
        }

        private void TurnReference(bool next)
        {
            if (_referencePage == null) return;
            foreach (Transform child in _referencePage)
                if (child.name.StartsWith(next ? "next" : "previous", System.StringComparison.Ordinal))
                {
                    var fsm = Core.FsmUtil.Find(child.gameObject, "Continue");
                    if (fsm != null && fsm.isActiveAndEnabled) { fsm.SendEvent("Clicked"); _referenceAt = 0; }
                    return;
                }
        }

        private void CloseReference()
        {
            if (_referenceCanvas != null && _referenceCanvas.enabled && _referenceNative != null)
            {
                var back = _referenceNative.Find("Back");
                var fsm = back != null ? Core.FsmUtil.Find(back.gameObject, "Continue") : null;
                if (fsm != null && fsm.isActiveAndEnabled) fsm.SendEvent("Clicked");
            }
            var heading = _rootRt != null ? _rootRt.Find("head") : null; if (heading != null) heading.gameObject.SetActive(true);
            ForgetReference();
        }

        private void ForgetReference()
        {
            _referenceCanvas = null; _referenceNative = _referencePage = null; _referencePages.Clear(); _referenceImages.Clear();
            _referenceTexts = null; _referenceContent = null; _referenceScroll = null; _referenceTitle = _referenceCounter = null;
            _referenceAt = 0;
        }
    }
}
