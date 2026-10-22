using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
{
    // Native MapCheatUI: a 900 px RawImage frame behind the 850 px RawImage_Map and the "press (M) to close" Text below it.
    // With Clean HUD on, the frame and text are hidden (components kept) and a CleanHUD plate and glyph hint take their place.
    internal static class MapFrame
    {
        private static RectTransform _map;
        private static Graphic _frame; private static Text _text;
        private static bool _frameOn, _textOn, _styled;
        private static GameObject _plate;
        private static GlyphText _hint;

        internal static void Sync(RectTransform map)
        {
            if (map != _map) { Restore(); _map = map; }
            if (_map == null || _map.parent == null) return;
            bool want = CleanHudModule.Running && CleanHudModule.Enabled.Value;
            if (want == _styled) return;
            if (!want) { Restore(); _map = map; return; }
            var root = (RectTransform)_map.parent;
            _frame = root.GetComponent<RawImage>();
            var native = root.Find("MapCheatText"); _text = native != null ? native.GetComponent<Text>() : null;
            if (_frame != null) { _frameOn = _frame.enabled; _frame.enabled = false; }
            if (_text != null) { _textOn = _text.enabled; _text.enabled = false; }
            if (_plate == null)
            {
                var plate = DialogueWidgets.Rect(root, "CleanHUD.MapPlate", 0, 0, 0, 0);
                plate.anchorMin = _map.anchorMin; plate.anchorMax = _map.anchorMax; plate.pivot = _map.pivot;
                plate.anchoredPosition = _map.anchoredPosition; plate.sizeDelta = _map.sizeDelta + new Vector2(80, 80);
                plate.SetSiblingIndex(0);
                var image = plate.gameObject.AddAccent<Image>(); image.sprite = Menu.PlateSprite(); image.type = Image.Type.Sliced; image.raycastTarget = false;
                _plate = plate.gameObject;
                var hint = DialogueWidgets.Rect(root, "CleanHUD.MapHint", 0, 0, 0, 0);
                hint.anchorMin = hint.anchorMax = hint.pivot = new Vector2(.5f, .5f);
                hint.anchoredPosition = new Vector2(_map.anchoredPosition.x, _map.anchoredPosition.y - _map.sizeDelta.y * .5f - 72f); hint.sizeDelta = new Vector2(1000, 40);
                _hint = hint.gameObject.AddAccent<GlyphText>(); _hint.Size = 26; _hint.color = DialogueWidgets.Gold; _hint.raycastTarget = false; _hint.Align = TextAnchor.MiddleCenter;
            }
            _hint.Set(_text != null && !string.IsNullOrEmpty(_text.text) ? _text.text.ToUpperInvariant() : "");
            _plate.SetActive(true); _hint.gameObject.SetActive(true);
            _styled = true;
        }

        internal static void Restore()
        {
            if (_styled)
            {
                if (_frame != null) _frame.enabled = _frameOn;
                if (_text != null) _text.enabled = _textOn;
            }
            if (_plate != null) Object.Destroy(_plate);
            if (_hint != null) Object.Destroy(_hint.gameObject);
            _plate = null; _hint = null; _frame = _text = null; _styled = false; _map = null;
        }
    }
}
