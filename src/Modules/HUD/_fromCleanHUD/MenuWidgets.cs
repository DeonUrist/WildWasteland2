using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WildWasteland.Modules.HUD
{
    // Row / tab / setting control: a Button whose look is driven by code (gold text + bar when hovered or selected).
    public sealed class MenuButton : Button
    {
        internal Action<bool> OnVisual;
        internal Action<int> OnHorizontal;
        internal bool Sticky;   // keep the highlighted look (current tab)
        private bool _hover;

        // Highlight follows the mouse only (no keyboard / selection highlight).
        public override void OnPointerEnter(PointerEventData e) { base.OnPointerEnter(e); _hover = true; Refresh(); }
        public override void OnPointerExit(PointerEventData e) { base.OnPointerExit(e); _hover = false; Refresh(); }
        protected override void OnDisable() { base.OnDisable(); _hover = false; }
        internal void Refresh() { if (OnVisual != null) OnVisual((_hover && IsInteractable()) || Sticky); }

        public override void OnMove(AxisEventData e)
        {
            if (OnHorizontal != null && (e.moveDir == MoveDirection.Left || e.moveDir == MoveDirection.Right))
            { OnHorizontal(e.moveDir == MoveDirection.Right ? 1 : -1); e.Use(); return; }
            base.OnMove(e);
        }
    }

    // Click / drag on a bar sets a 0..1 value.
    public sealed class BarDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        internal Action<float> OnValue;
        public void OnPointerDown(PointerEventData e) { Apply(e); }
        public void OnDrag(PointerEventData e) { Apply(e); }
        private void Apply(PointerEventData e)
        {
            var rt = (RectTransform)transform; Vector2 lp;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out lp)) return;
            var r = rt.rect;
            if (OnValue != null) OnValue(Mathf.Clamp01((lp.x - r.xMin) / Mathf.Max(1f, r.width)));
        }
    }

    public sealed class MenuScrollRect : ScrollRect
    {
        public override void OnBeginDrag(PointerEventData e) { }
        public override void OnDrag(PointerEventData e) { }
        public override void OnEndDrag(PointerEventData e) { }
    }

    // Arrow inside a row: handles its own down/click so the row button does not take the press.
    public sealed class ClickZone : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        internal Action OnClick;
        public void OnPointerDown(PointerEventData e) { }
        public void OnPointerClick(PointerEventData e) { if (OnClick != null) OnClick(); }
    }
}
