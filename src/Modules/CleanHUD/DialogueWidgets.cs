using System;
using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class DialogueWidgets
    {
        internal static readonly Color Gold = new Color(.886f, .749f, .424f);
        internal static readonly Color SpeakerGray = new Color32(160, 160, 160, 255);
        internal static void Speaker(GlyphText label, string npc)
        {
            bool player = label.Text.StartsWith("You:", StringComparison.OrdinalIgnoreCase);
            label.LeadingCharacters = player ? 4 : label.Text.StartsWith(npc + ":", StringComparison.OrdinalIgnoreCase) ? npc.Length + 1 : 0;
            label.LeadingColor = player ? Gold : SpeakerGray;
            label.SetVerticesDirty();
        }
        internal static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        internal static GlyphText Text(Transform parent, string name, float x, float y, float w, float h, float size, Color color, string text)
        {
            var t = Rect(parent, name, x, y, w, h).gameObject.AddAccent<GlyphText>();
            t.Size = size; t.color = color; t.raycastTarget = false; t.Set(text); return t;
        }
        internal static MenuButton Button(Transform parent, float x, float y, float w, float h, string label, Action action)
        {
            var r = Rect(parent, "choice", x, y, w, h); var image = r.gameObject.AddAccent<Image>(); image.color = Color.clear;
            var t = Text(r, "label", 20, 0, w - 30, h, 26, Color.white, label);
            var b = r.gameObject.AddComponent<MenuButton>(); b.transition = Selectable.Transition.None; b.targetGraphic = image;
            b.OnVisual = selected => { t.color = !b.interactable ? Color.gray : selected ? Gold : Color.white; };
            b.onClick.AddListener(() => action()); b.Refresh(); return b;
        }
        internal static ScrollRect Scroll(Transform parent, string name, float x, float y, float w, float h, out RectTransform content)
        {
            var r = Rect(parent, name, x, y, w, h); var background = r.gameObject.AddAccent<Image>(); background.color = new Color(0, 0, 0, .1f);
            r.gameObject.AddComponent<RectMask2D>(); var s = r.gameObject.AddComponent<ScrollRect>();
            content = Rect(r, "content", 0, 0, w, h); s.viewport = r; s.content = content; s.horizontal = false; s.vertical = true;
            s.movementType = ScrollRect.MovementType.Clamped; s.scrollSensitivity = 40; return s;
        }
    }
}
