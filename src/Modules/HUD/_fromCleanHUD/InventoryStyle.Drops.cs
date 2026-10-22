using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;
namespace WildWasteland.Modules.CleanHUD
{
    internal static partial class InventoryStyle
    {
        static readonly List<MenuButton> Buttons = new List<MenuButton>();
        static RectTransform _inventory, _dropDialog, _dropBar, _dropFill;
        static GlyphText _dropTitle, _dropAmount;
        static bool _dragging;
        static int _dropKind = -1, _dropQuantity, _dropMax;
        static bool ModalButton(MenuButton button)
        { return _dropDialog == null || !_dropDialog.gameObject.activeSelf || button.transform.IsChildOf(_dropDialog); }
        static void RefreshExtra()
        {
            if (_dropKind < 0) return;
            var count = InventorySupplies.Count(_dropKind);
            if (count == null || count.Value < 1 || !InventorySupplies.Ready) HideDrop();
            else if (_dropMax != count.Value) { _dropMax = count.Value; SetDropQuantity(_dropQuantity); }
        }
        static void ClearExtra()
        { Buttons.Clear(); _inventory = _dropDialog = _dropBar = _dropFill = null; _dropTitle = _dropAmount = null; _dragging = false; _dropKind = -1; }
        static MenuButton Hit(Transform parent, float x, float y, float w, float h, Action action)
        {
            var image = Rect(parent, "hit", x, y, w, h).gameObject.AddAccent<Image>(); image.color = Color.clear;
            var button = image.gameObject.AddComponent<MenuButton>(); button.transition = Selectable.Transition.None;
            button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.OnVisual = over => image.color = over ? new Color(.886f, .749f, .424f, .14f) : Color.clear;
            button.onClick.AddListener(() => action()); Buttons.Add(button); return button;
        }

        static MenuButton ActionButton(Transform parent, string text, float x, float y, float w, float h, Action action)
        {
            var button = Hit(parent, x, y, w, h, action);
            Label(button.transform, text, 0, 0, w, h, 23, DialogueWidgets.Gold, TextAnchor.MiddleCenter); return button;
        }

        static void BuildDrop()
        {
            _dropDialog = Rect(_view, "drop quantity", 0, 0, 1920, 1080);
            var shade = _dropDialog.gameObject.AddAccent<Image>(); shade.color = new Color(0, 0, 0, .8f);
            var panel = Panel(_dropDialog, "quantity", 570, 350, 780, 340, "");
            _dropTitle = Label(panel, "", 30, 20, 720, 44, 28, DialogueWidgets.Gold, TextAnchor.MiddleCenter);
            _dropAmount = Label(panel, "", 30, 78, 720, 40, 26, White, TextAnchor.MiddleCenter);
            _dropBar = Rect(panel, "quantity slider", 120, 146, 540, 36);
            var track = _dropBar.gameObject.AddAccent<Image>(); track.color = new Color(.2f, .2f, .18f);
            _dropFill = Rect(_dropBar, "fill", 0, 0, 540, 36);
            var fill = _dropFill.gameObject.AddAccent<Image>(); fill.color = DialogueWidgets.Gold; fill.raycastTarget = false;
            var drag = _dropBar.gameObject.AddComponent<BarDrag>(); drag.OnValue = SetDropFraction;
            ActionButton(panel, "-", 40, 138, 64, 50, () => SetDropQuantity(_dropQuantity - 1));
            ActionButton(panel, "+", 676, 138, 64, 50, () => SetDropQuantity(_dropQuantity < _dropMax ? _dropQuantity + 1 : _dropMax));
            ActionButton(panel, "CANCEL", 80, 242, 260, 52, HideDrop);
            ActionButton(panel, "DROP", 440, 242, 260, 52, ConfirmDrop);
            _dropDialog.gameObject.SetActive(false);
        }

        static void ShowDrop(int kind)
        {
            if (!Session.Open || !Application.isFocused || !InventorySupplies.Ready) return;
            var count = InventorySupplies.Count(kind); if (count == null || count.Value < 1) return;
            _dropKind = kind; _dropMax = count.Value; _dropTitle.Set("DROP " + (kind == 12 ? "FIRST AID" : kind == 11 ? "BANDAGES" : kind == 10 ? "NUTS" : InventorySupplies.Keys[kind].ToUpperInvariant()));
            _dropDialog.gameObject.SetActive(true); SetDropQuantity(1);
        }

        static void HideDrop() { if (_dropDialog != null) _dropDialog.gameObject.SetActive(false); _dropKind = -1; _dragging = false; }

        static void SetDropFraction(float fraction)
        { SetDropQuantity((int)Math.Min(_dropMax, Math.Round(1d + (_dropMax - 1d) * Mathf.Clamp01(fraction)))); }

        static void SetDropQuantity(int quantity)
        {
            _dropQuantity = Mathf.Clamp(quantity, 1, Mathf.Max(1, _dropMax));
            _dropAmount.Set(_dropQuantity.ToString(CultureInfo.InvariantCulture) + " / " + _dropMax.ToString(CultureInfo.InvariantCulture));
            _dropFill.sizeDelta = new Vector2(_dropMax <= 1 ? 540 : 12 + 528f * (_dropQuantity - 1f) / (_dropMax - 1f), 36);
        }

        static void ConfirmDrop()
        {
            if (_dropKind < 0 || !Session.Open || !Application.isFocused) return;
            if (InventorySupplies.Drop(_dropKind, _dropQuantity)) { HideDrop(); Refresh(); }
            else _dropAmount.Set("UNABLE TO DROP - SUPPLIES RETAINED");
        }

        static void DropPointer(Camera camera)
        {
            if (_dropKind < 0) { _dragging = false; return; }
            if (Input.GetMouseButtonDown(0)) _dragging = RectTransformUtility.RectangleContainsScreenPoint(_dropBar, Input.mousePosition, camera);
            if (_dragging && Input.GetMouseButton(0))
            {
                Vector2 point;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_dropBar, Input.mousePosition, camera, out point))
                    SetDropFraction((point.x - _dropBar.rect.xMin) / _dropBar.rect.width);
            }
            if (Input.GetMouseButtonUp(0)) _dragging = false;
        }
    }
}
