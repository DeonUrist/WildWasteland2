using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    // Hit feedback for the player's own bullets: damage numbers ([HUD] FloatingDamage 0 off / 1 a list top right / 2 floating at the hit
    // point; white = damage, red with "!" = headshot, light blue = % taken off a vehicle part) and a diagonal hit marker at the screen
    // centre ([HUD] HitMarker; white, red on a headshot). Drawn with IMGUI from the Core runner's OnGUI (Scheduler.Gui job);
    // nothing is drawn (and the job returns at once) while there is nothing to show.
    // WW: lifetimes/limits/merge window come from tools.cfg ([HUD / Hit feedback], defaults = 1.2.2 constants), Camera.main is
    // GameRefs.MainCamera, world points follow floating-origin jumps (Shift), Shutdown destroys the white texture.
    internal static class Hud
    {
        private struct Pending { public GameObject Target; public Vector3 Point; public float Damage; public bool Head, Part; public float Time; }
        private struct Floater { public Vector3 Point; public string Text; public bool Head, Part; public float Born; }
        private struct Line { public string Text; public bool Head, Part; public float Born; }

        private static readonly List<Pending> _pending = new List<Pending>();     // this frame's hits, merged per target (pellets -> one number)
        private static readonly List<Floater> _floaters = new List<Floater>();
        private static readonly List<Line> _lines = new List<Line>();
        private static float _markerUntil, _markerHeadUntil;
        private static GUIStyle _floatStyle, _lineStyle;
        private static Texture2D _white;

        // called by the Guns module (Services.Hud) for every player bullet that hits a creature (damage < 0 in the game's convention)
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
        private static Color ColorOf(bool head, bool part, float a)
        {
            if (part) return new Color(0.55f, 0.85f, 1f, a);
            if (head) return new Color(1f, 0.2f, 0.15f, a);
            return new Color(1f, 1f, 1f, a);
        }

        // end of the frame's simulation: turn the merged hits into numbers / marker
        public static void Flush()
        {
            if (_pending.Count == 0) return;
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
        }

        public static void OnSceneLoaded() { _pending.Clear(); _floaters.Clear(); _lines.Clear(); _markerUntil = 0f; _markerHeadUntil = 0f; }

        /// <summary>FloatingOrigin jump: keep the numbers on their hit points.</summary>
        public static void Shift(Vector3 delta)
        {
            for (int i = 0; i < _floaters.Count; i++) { var f = _floaters[i]; f.Point += delta; _floaters[i] = f; }
            for (int i = 0; i < _pending.Count; i++) { var p = _pending[i]; p.Point += delta; _pending[i] = p; }
        }

        public static void Shutdown()
        {
            OnSceneLoaded();
            if (_white != null) UnityEngine.Object.Destroy(_white);
            _white = null; _floatStyle = null; _lineStyle = null;
        }

        private static void EnsureStyles()
        {
            if (_white != null && _floatStyle != null) return;
            if (_white == null)
            {
                _white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                _white.SetPixel(0, 0, Color.white); _white.Apply();
            }
            _floatStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _lineStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
        }

        // OnGUI's own early-out (it also expires the floaters/lines, so it keeps running until both lists are empty); Flush fills them in LateUpdate
        internal static bool GuiWanted() { return _floaters.Count > 0 || _lines.Count > 0 || Time.unscaledTime < _markerUntil; }
        public static void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float now = Time.unscaledTime;
            bool marker = now < _markerUntil;
            if (!marker && _floaters.Count == 0 && _lines.Count == 0) return;
            EnsureStyles();
            var cam = GameRefs.MainCamera;
            int fontSize = HudModule.DamageFontSize.Value;

            if (_floaters.Count > 0)
            {
                float floatLife = HudModule.NumberSeconds.Value;
                _floatStyle.fontSize = Mathf.Max(8, fontSize + 4);
                for (int i = _floaters.Count - 1; i >= 0; i--)
                {
                    var f = _floaters[i];
                    float age = now - f.Born;
                    if (age > floatLife) { _floaters.RemoveAt(i); continue; }
                    if (cam == null) continue;
                    Vector3 sp = cam.WorldToScreenPoint(f.Point + Vector3.up * (0.3f + age * 0.8f));
                    if (sp.z <= 0f) continue;
                    float a = age < floatLife * 0.6f ? 1f : 1f - (age - floatLife * 0.6f) / (floatLife * 0.4f);
                    var c = ColorOf(f.Head, f.Part, a);
                    DrawText(new Rect(sp.x - 60f, Screen.height - sp.y - 14f, 120f, 28f), f.Text, _floatStyle, c);
                }
            }

            if (_lines.Count > 0)
            {
                float lineLife = HudModule.ListSeconds.Value;
                _lineStyle.fontSize = Mathf.Max(8, fontSize);
                float y = 12f;
                for (int i = _lines.Count - 1; i >= 0; i--)
                {
                    var l = _lines[i];
                    float age = now - l.Born;
                    if (age > lineLife) { _lines.RemoveAt(i); continue; }
                    float a = age < lineLife * 0.7f ? 1f : 1f - (age - lineLife * 0.7f) / (lineLife * 0.3f);
                    DrawText(new Rect(Screen.width - 320f, y, 300f, _lineStyle.fontSize + 6f), l.Text, _lineStyle, ColorOf(l.Head, l.Part, a));
                    y += _lineStyle.fontSize + 4f;
                }
            }

            if (marker)
            {
                // a diagonal cross: four bars at 45 degrees around the centre, with a gap in the middle
                float markerLife = Mathf.Max(0.01f, HudModule.MarkerSeconds.Value);
                float size = Mathf.Max(6f, HudModule.HitMarkerSize.Value), gap = size * 0.35f, thick = Mathf.Max(1f, size * 0.12f);
                float a = Mathf.Clamp01((_markerUntil - now) / markerLife * 2f);
                Color c = now < _markerHeadUntil ? new Color(1f, 0.2f, 0.15f, a) : new Color(1f, 1f, 1f, a);
                Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var old = GUI.color; GUI.color = c;
                for (int k = 0; k < 4; k++)
                {
                    var m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(45f + 90f * k, centre);
                    GUI.DrawTexture(new Rect(centre.x - thick * 0.5f, centre.y - size, thick, size - gap), _white);
                    GUI.matrix = m;
                }
                GUI.color = old;
            }
        }

        private static void DrawText(Rect r, string text, GUIStyle style, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, c.a * 0.8f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, style);    // shadow
            GUI.color = c;
            GUI.Label(r, text, style);
            GUI.color = old;
        }
    }
}
