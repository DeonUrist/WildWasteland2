using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    // Hit feedback for the player's own bullets: damage numbers ([HUD] FloatingDamage 0 off / 1 a list top right / 2 floating at the hit
    // point; white = damage, red with "!" = headshot, light blue = % taken off a vehicle part) and a diagonal hit marker at the screen
    // centre ([HUD] HitMarker; white, red on a headshot). Drawn with a pooled uGUI overlay from the HUD LateUpdate job;
    // nothing is drawn (and the job returns at once) while there is nothing to show.
    // WW: lifetimes/limits/merge window come from tools.cfg ([HUD / Hit feedback], defaults = 1.2.2 constants), Camera.main is
    // GameRefs.MainCamera, world points follow floating-origin jumps (Shift), Shutdown destroys its owned canvas and pooled graphics.
    internal static class HitFeedbackOverlay
    {
        /// <summary>Stores pending values for the hit feedback overlay.</summary>
        private struct Pending { public GameObject Target; public Vector3 Point; public float Damage; public bool Head, Part; public float Time; }
        /// <summary>Stores floater values for the hit feedback overlay.</summary>
        private struct Floater { public Vector3 Point; public string Text; public bool Head, Part; public float Born; }
        /// <summary>Stores line values for the hit feedback overlay.</summary>
        private struct Line { public string Text; public bool Head, Part; public float Born; }

        private static readonly List<Pending> _pending = new List<Pending>();     // this frame's hits, merged per target (pellets -> one number)
        private static readonly List<Floater> _floaters = new List<Floater>();
        private static readonly List<Line> _lines = new List<Line>();
        private static float _markerUntil, _markerHeadUntil;
        private static GameObject _canvas;
        private static readonly List<UnityEngine.UI.Text> _texts = new List<UnityEngine.UI.Text>();
        private static readonly List<UnityEngine.UI.Image> _bars = new List<UnityEngine.UI.Image>();
        private static Font _font;
        private static int _textCount;

        // called by the Guns module (Core.ModApi.Get<Core.IHitFeedback>()) for every player bullet that hits a creature (damage < 0 in the game's convention)
        /// <summary>Queues player damage and headshot feedback, merging nearby hits on one target.</summary>
        public static void PlayerHit(GameObject target, Vector3 point, float damage, bool head)
        {
            if (target == null || !HudModule.HudEnabled.Value) return;
            if (head && HudModule.HeadshotSound.Value) HeadshotAudio.Play();
            int mode = HudModule.DamageNumbers.Value;
            if (mode == 0 && !HudModule.HitMarker.Value) return;
            float now = Time.unscaledTime, merge = HudModule.MergeSeconds.Value;
            for (int i = 0; i < _pending.Count; i++)
            {
                var p = _pending[i];
                if (p.Target == target && now - p.Time < merge)      // same creature, same frame (a shotgun blast): one number
                {
                    p.Damage += damage; p.Head |= head; p.Point = point;
                    _pending[i] = p;
                    return;
                }
            }
            _pending.Add(new Pending { Target = target, Point = point, Damage = damage, Head = head, Time = now });
        }

        // a player bullet took pct % off a vehicle part's condition: a light blue number ("-1.2%")
        /// <summary>Queues condition-loss feedback, merging nearby hits on one vehicle part.</summary>
        public static void PartHit(GameObject part, Vector3 point, float pct)
        {
            if (part == null || !HudModule.HudEnabled.Value || HudModule.DamageNumbers.Value == 0) return;
            float now = Time.unscaledTime, merge = HudModule.MergeSeconds.Value;
            for (int i = 0; i < _pending.Count; i++)
            {
                var p = _pending[i];
                if (p.Part && p.Target == part && now - p.Time < merge) { p.Damage += pct; p.Point = point; _pending[i] = p; return; }
            }
            _pending.Add(new Pending { Target = part, Point = point, Damage = pct, Part = true, Time = now });
        }

        /// <summary>F7 test button: one fake hit a few metres in front of the camera, through the normal path (current settings apply).</summary>
        internal static void Test(bool head)
        {
            var cam = GameRefs.MainCamera;
            var target = GameRefs.Player != null ? GameRefs.Player : Runner.Host;
            if (target == null) return;
            Vector3 at = cam != null ? cam.transform.position + cam.transform.forward * 4f : target.transform.position;
            PlayerHit(target, at, head ? 50f : 20f, head);
        }

        // colours: white = damage, red with "!" = headshot, light blue = vehicle part condition
        /// <summary>Colours: white = damage, red with "!" = headshot, light blue = vehicle part condition.</summary>
        private static Color ColorOf(bool head, bool part, float a)
        {
            if (part) return new Color(0.55f, 0.85f, 1f, a);
            if (head) return new Color(1f, 0.2f, 0.15f, a);
            return new Color(1f, 1f, 1f, a);
        }

        // end of the frame's simulation: turn the merged hits into numbers / marker
        /// <summary>Converts merged hits into the configured display and updates the uGUI overlay.</summary>
        public static void Flush()
        {
            if (_pending.Count == 0) { Render(); return; }
            float now = Time.unscaledTime;
            int mode = HudModule.DamageNumbers.Value, maxLines = HudModule.ListLines.Value;
            float markerLife = HudModule.MarkerSeconds.Value;
            foreach (var p in _pending)
            {
                float dmg = Mathf.Abs(p.Damage);
                string text = p.Part ? (dmg >= 10f ? dmg.ToString("0") : dmg.ToString("0.#")) + "%" : (dmg >= 10f ? dmg.ToString("0") : dmg.ToString("0.#")) + (p.Head ? "!" : "");
                if (mode == 2) _floaters.Add(new Floater { Point = p.Point, Text = text, Head = p.Head, Part = p.Part, Born = now });
                else if (mode == 1)
                {
                    string name = p.Target == null ? "?" : p.Part ? p.Target.transform.root.name : p.Target.name;   // (1.2.1) a creature: its own name (Apocapatrol crews sit under the car)
                    int cut = name.IndexOf('(');
                    if (cut > 0) name = name.Substring(0, cut);
                    _lines.Add(new Line { Text = "-" + text + "  " + name, Head = p.Head, Part = p.Part, Born = now });
                    while (_lines.Count > maxLines) _lines.RemoveAt(0);
                }
                if (HudModule.HitMarker.Value && !p.Part)
                {
                    _markerUntil = now + markerLife;
                    if (p.Head) _markerHeadUntil = now + markerLife;
                }
            }
            _pending.Clear();
            Render();
        }

        /// <summary>Handles scene loaded for the hit feedback overlay.</summary>
        public static void OnSceneLoaded() { _pending.Clear(); _floaters.Clear(); _lines.Clear(); _markerUntil = 0f; _markerHeadUntil = 0f; if (_canvas != null) _canvas.SetActive(false); }

        /// <summary>FloatingOrigin jump: keep the numbers on their hit points.</summary>
        public static void Shift(Vector3 delta)
        {
            for (int i = 0; i < _floaters.Count; i++) { var f = _floaters[i]; f.Point += delta; _floaters[i] = f; }
            for (int i = 0; i < _pending.Count; i++) { var p = _pending[i]; p.Point += delta; _pending[i] = p; }
        }

        /// <summary>Destroys HUD-owned feedback graphics and releases scene-bound hit records.</summary>
        public static void Shutdown()
        {
            OnSceneLoaded();
            if (_canvas != null) UnityEngine.Object.Destroy(_canvas);
            _canvas = null; _texts.Clear(); _bars.Clear(); _font = null;
        }

        /// <summary>Lazily creates a non-interactive pixel-sized overlay, matching the former feedback coordinates.</summary>
        private static void EnsureCanvas()
        {
            if (_canvas != null) return;
            _canvas = new GameObject("WW Hit Feedback", typeof(RectTransform), typeof(Canvas));
            _canvas.hideFlags = HideFlags.HideAndDontSave;
            _canvas.transform.SetParent(Runner.Host.transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Marker " + i, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(_canvas.transform, false);
                var image = go.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = false;
                var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, 0f); rect.localRotation = Quaternion.Euler(0f, 0f, -45f - 90f * i);
                _bars.Add(image);
            }
        }

        /// <summary>Obtains a reusable text graphic without adding components to any saved game object.</summary>
        private static UnityEngine.UI.Text TextGraphic()
        {
            if (_textCount == _texts.Count)
            {
                var go = new GameObject("Damage " + _texts.Count, typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Shadow));
                go.transform.SetParent(_canvas.transform, false);
                var text = go.GetComponent<UnityEngine.UI.Text>(); text.font = _font; text.fontStyle = FontStyle.Bold;
                text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
                var shadow = go.GetComponent<UnityEngine.UI.Shadow>(); shadow.effectColor = new Color(0f, 0f, 0f, .8f); shadow.effectDistance = new Vector2(1f, -1f);
                _texts.Add(text);
            }
            var result = _texts[_textCount++]; result.gameObject.SetActive(true); return result;
        }

        /// <summary>Draws active numbers and marker bars once per frame, retiring expired feedback even while hidden.</summary>
        private static void Render()
        {
            float now = Time.unscaledTime;
            for (int i = _floaters.Count - 1; i >= 0; i--) if (now - _floaters[i].Born > HudModule.NumberSeconds.Value) _floaters.RemoveAt(i);
            for (int i = _lines.Count - 1; i >= 0; i--) if (now - _lines[i].Born > HudModule.ListSeconds.Value) _lines.RemoveAt(i);
            bool wanted = _floaters.Count > 0 || _lines.Count > 0 || now < _markerUntil;
            if (!wanted) { if (_canvas != null) _canvas.SetActive(false); return; }
            EnsureCanvas(); _canvas.SetActive(true); _textCount = 0;
            var cam = GameRefs.MainCamera;
            foreach (var f in _floaters)
            {
                if (cam == null) continue;
                float age = now - f.Born, life = Mathf.Max(.01f, HudModule.NumberSeconds.Value);
                var sp = cam.WorldToScreenPoint(f.Point + Vector3.up * (.3f + age * .8f)); if (sp.z <= 0f) continue;
                float alpha = Mathf.Clamp01((life - age) / (life * .4f));
                DrawText(new Rect(sp.x - 60f, Screen.height - sp.y - 14f, 120f, 28f), f.Text, Mathf.Max(8, HudModule.DamageFontSize.Value + 4), TextAnchor.MiddleCenter, ColorOf(f.Head, f.Part, alpha));
            }
            float y = 12f;
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                var l = _lines[i]; float life = Mathf.Max(.01f, HudModule.ListSeconds.Value), age = now - l.Born;
                int size = Mathf.Max(8, HudModule.DamageFontSize.Value);
                DrawText(new Rect(Screen.width - 320f, y, 300f, size + 6f), l.Text, size, TextAnchor.UpperRight, ColorOf(l.Head, l.Part, Mathf.Clamp01((life - age) / (life * .3f))));
                y += size + 4f;
            }
            for (int i = _textCount; i < _texts.Count; i++) _texts[i].gameObject.SetActive(false);
            float markerLife = Mathf.Max(.01f, HudModule.MarkerSeconds.Value), sizeMarker = Mathf.Max(6f, HudModule.HitMarkerSize.Value);
            float gap = sizeMarker * .35f, thickness = Mathf.Max(1f, sizeMarker * .12f), a = Mathf.Clamp01((_markerUntil - now) / markerLife * 2f);
            for (int i = 0; i < _bars.Count; i++)
            {
                var image = _bars[i]; image.gameObject.SetActive(now < _markerUntil);
                image.color = now < _markerHeadUntil ? new Color(1f, .2f, .15f, a) : new Color(1f, 1f, 1f, a);
                image.rectTransform.sizeDelta = new Vector2(thickness, sizeMarker - gap);
                float radians = (45f + 90f * i) * Mathf.Deg2Rad;
                image.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * gap;
            }
        }

        /// <summary>Places one pooled label using top-left screen coordinates and the original feedback alignment.</summary>
        private static void DrawText(Rect rect, string content, int size, TextAnchor alignment, Color color)
        {
            var text = TextGraphic(); text.text = content; text.fontSize = size; text.alignment = alignment; text.color = color;
            var rt = text.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(rect.x, -rect.y); rt.sizeDelta = rect.size;
        }
    }
}
