using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.HUD
{
    /// <summary>
    /// ThirdPerson part (split from PlayerBody/Camera/ThirdPerson.cs in Wave A): moves the game's HUD cursor icons (MouseCrosshair,
    /// MousePoint, MouseWrench, MouseHand) to where the third-person shot lands, and puts them back. Target: HUD crosshair (B10); it duplicates
    /// the HUD/Crosshair lookups.
    /// </summary>
    internal sealed class CursorPresentation : WildWasteland.Core.ICursorPresentation
    {
        private RectTransform _xhair;
        private bool _xhairMoved;
        private float _xhairFind;
        // the game's cursor icons on the HUD Canvas (level1: Canvas/MousePoint = the dot, MouseWrench, MouseCrosshair, MouseHand): all of them move
        private readonly string[] CursorNames = { "MouseCrosshair", "MousePoint", "MouseWrench", "MouseHand" };
        private readonly RectTransform[] _cursors = new RectTransform[4];
        private readonly Vector3[] _cursorHome = new Vector3[4];
        public void Move(Vector2 screen)
        {
            if (_xhair == null || _xhair.parent == null)
            {
                if (Time.unscaledTime < _xhairFind) return;
                _xhairFind = Time.unscaledTime + 1f;
                // (2.2.9) any of the cursor icons: with nothing drawn the crosshair is inactive (GameObject.Find skips it) and only the grab dot shows
                var go = FindCursor();
                if (go == null) return;
                _xhair = go.GetComponent<RectTransform>();
                if (_xhair == null || _xhair.parent == null) return;
                var n = new System.Collections.Generic.List<string>();
                for (int i = 0; i < CursorNames.Length; i++)
                {
                    var c = _xhair.parent.Find(CursorNames[i]) as RectTransform;
                    _cursors[i] = c;
                    if (c != null) { _cursorHome[i] = c.localPosition; n.Add(c.name); }
                }
                _xhairMoved = false;
                HudModule.Log("Crosshair: dynamic cursor icons " + string.Join(", ", n.ToArray()));
            }
            var parent = _xhair.parent as RectTransform;
            if (!ReferenceEquals(_xhairCanvasFor, _xhair) || _xhairCanvas == null) { _xhairCanvasFor = _xhair; _xhairCanvas = _xhair.GetComponentInParent<Canvas>(); }   // (WW perf) cached per crosshair
            var canvas = _xhairCanvas;
            if (parent == null || canvas == null) return;
            var root = canvas.rootCanvas;
            Camera uiCam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            Vector2 lc, lt;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), uiCam, out lc)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCam, out lt)) return;
            if (!_xhairMoved)
            {
                for (int i = 0; i < _cursors.Length; i++) if (_cursors[i] != null) _cursorHome[i] = _cursors[i].localPosition;
                _xhairMoved = true;
            }
            Vector3 delta = lt - lc;
            for (int i = 0; i < _cursors.Length; i++) if (_cursors[i] != null) _cursors[i].localPosition = _cursorHome[i] + delta;
        }

        private Canvas _xhairCanvas; private RectTransform _xhairCanvasFor;
        private GameObject FindCursor()
        {
            for (int i = 0; i < CursorNames.Length; i++) { var g = GameObject.Find(CursorNames[i]); if (g != null && g.transform.parent != null) return g; }
            foreach (var r in Resources.FindObjectsOfTypeAll<RectTransform>())   // all inactive: once a second until found
                if (r != null && r.gameObject.scene.IsValid() && r.parent != null && System.Array.IndexOf(CursorNames, r.name) >= 0) return r.gameObject;
            return null;
        }

        public void Restore()
        {
            if (_xhairMoved) for (int i = 0; i < _cursors.Length; i++) if (_cursors[i] != null) _cursors[i].localPosition = _cursorHome[i];
            _xhairMoved = false;
        }
    }
}
