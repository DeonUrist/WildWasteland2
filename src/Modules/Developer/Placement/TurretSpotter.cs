using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WildWasteland.Modules.Developer
{
    // [Debug] TurretSpotEditor (2.2.0, was the separate Apocaspotter plugin): glowing markers on the human turret spots of the nearest raider
    // car (yellow = the selected spot / feet, orange = the other spots, green = its forward), the numpad moves the selected spot, 5 saves it
    // into the car's template file (riderPos / riderPos2 / riderPos3).
    internal sealed class TurretSpotter : MonoBehaviour
    {
        private const float Step = 0.05f, Range = 80f;
        private GameObject _car;
        private readonly GameObject[] _spots = new GameObject[7];
        private GameObject _nose;
        private int _slot;
        private bool _show = true;
        private string _status = ""; private float _statusUntil;
        private float _nextFind;
        private GUIStyle _style;

        /// <summary>Reads a held native keyboard key without changing cursor ownership.</summary>
        private static bool Held(Key key)
        {
            var kb = Keyboard.current; if (kb == null) return false;
            try { return kb[key].isPressed; } catch (Exception) { return false; }
        }

        // ---- F7 "Turret spot editor" page (the editor keeps running in-world after F7 closes)
        /// <summary>Returns the currently selected native vehicle.</summary>
        internal GameObject Car { get { return _car; } }
        /// <summary>Returns the currently selected turret or merchant slot.</summary>
        internal int Slot { get { return _slot; } }
        internal bool Markers { get { return _show; } set { _show = value; } }
        /// <summary>Returns the current bounded placement editor status message.</summary>
        internal string Status { get { return Time.unscaledTime < _statusUntil ? _status : ""; } }
        /// <summary>Returns the display label of the selected turret slot.</summary>
        internal string SlotName { get { return SlotLabel(_slot); } }
        internal Transform SelectedAnchor { get { return _car != null ? Core.ModApi.Get<Core.ITurretPlacement>().Anchor(_car, _slot) : null; } }
        internal string SelectedRider { get { var who = _car != null ? Core.ModApi.Get<Core.ITurretPlacement>().Rider(_car, _slot) : null; return who != null ? who.name : "(gone)"; } }
        /// <summary>Selects the next supported live turret slot.</summary>
        internal void NextSlot()
        {
            if (_car == null) return;
            for (int k = 1; k <= _spots.Length; k++) { int n = (_slot + k) % _spots.Length; if (Core.ModApi.Get<Core.ITurretPlacement>().Anchor(_car, n) != null) { _slot = n; break; } }
            Say("tweaking " + SlotLabel(_slot));
        }
        /// <summary>Publishes the selected turret anchor through its real Patrols owner.</summary>
        internal void SaveSpot() { if (_car != null) Say(Core.ModApi.Get<Core.ITurretPlacement>().SaveAnchor(_car, _slot)); }
        /// <summary>Requests a fresh nearest-vehicle selection on the next editor step.</summary>
        internal void Refind() { _nextFind = 0f; }

        // WW: was Update (Update job)
        /// <summary>Updates enabled placement selection, input and temporary native markers.</summary>
        internal void Tick()
        {
            if(Core.ModApi.Get<Core.ITurretPlacement>()==null){_car=null;Hide();return;}
            if(BusPassengerPoseEditor.Active){Hide();return;}
            if (PatrolEditorRuntime.TurretSpotEditor == null || !PatrolEditorRuntime.TurretSpotEditor.Value) { _car = null; Hide(); return; }
            if (Time.unscaledTime >= _nextFind) { _nextFind = Time.unscaledTime + 0.5f; _car = Nearest(); }
            if (_car == null) { Hide(); return; }
            if (Core.ModApi.Get<Core.ITurretPlacement>().Anchor(_car, _slot) == null) for (int i = 0; i < _spots.Length; i++) if (Core.ModApi.Get<Core.ITurretPlacement>().Anchor(_car, i) != null) { _slot = i; break; }
            var anchor = Core.ModApi.Get<Core.ITurretPlacement>().Anchor(_car, _slot);
            if (anchor == null) { Hide(); return; }
            if (_show)
            {
                for (int i = 0; i < _spots.Length; i++)
                {
                    var a = Core.ModApi.Get<Core.ITurretPlacement>().Anchor(_car, i);
                    if (a == null) { if (_spots[i] != null) _spots[i].SetActive(false); continue; }
                    Place(ref _spots[i], a.position, i == _slot ? 0.22f : 0.14f, i == _slot ? new Color(1f, 0.85f, 0.1f) : new Color(1f, 0.45f, 0.1f));
                }
                Place(ref _nose, anchor.position + anchor.forward * 0.4f + anchor.up * 0.05f, 0.1f, new Color(0.2f, 1f, 0.3f));
            }
            else Hide();

            if (Time.timeScale <= 0f) return;
            bool shift = Held(Key.LeftShift) || Held(Key.RightShift);
            float step = Step * (shift ? 5f : 1f);
            if (Core.Hotkeys.Pressed(Key.Numpad0)) NextSlot();
            if (Core.Hotkeys.Pressed(Key.NumpadPeriod)) { _show = !_show; Say(_show ? "markers on" : "markers off"); }
            var d = Vector3.zero;
            if (Core.Hotkeys.Pressed(Key.Numpad8)) d.z += step; if (Core.Hotkeys.Pressed(Key.Numpad2)) d.z -= step;
            if (Core.Hotkeys.Pressed(Key.Numpad6)) d.x += step; if (Core.Hotkeys.Pressed(Key.Numpad4)) d.x -= step;
            if (Core.Hotkeys.Pressed(Key.Numpad9)) d.y += step; if (Core.Hotkeys.Pressed(Key.Numpad7)) d.y -= step;
            if (d != Vector3.zero) anchor.localPosition += d;
            if (Core.Hotkeys.Pressed(Key.Numpad5)) Say(Core.ModApi.Get<Core.ITurretPlacement>().SaveAnchor(_car, _slot));
        }

        /// <summary>Finds the nearest eligible native vehicle within the existing tool radius.</summary>
        private static GameObject Nearest()
        {
            var cam = Core.GameRefs.MainCamera; if (cam == null) return null;
            GameObject best = null; float bd = Range * Range;
            foreach (var car in Core.ModApi.Get<Core.ITurretPlacement>().Cars())
            {
                if (car == null) continue;
                float d = (car.transform.position - cam.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = car; }
            }
            return best;
        }

        /// <summary>Hides temporary authoring marker objects.</summary>
        private void Hide()
        {
            foreach (var s in _spots) if (s != null) s.SetActive(false);
            if (_nose != null) _nose.SetActive(false);
        }

        /// <summary>Creates or moves a temporary authoring marker at the selected native pose.</summary>
        private static void Place(ref GameObject go, Vector3 pos, float size, Color color)
        {
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Apocapatrol.TurretMarker"; go.hideFlags = HideFlags.HideAndDontSave;
                var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
                var rd = go.GetComponent<Renderer>();
                var sh = Shader.Find("Standard") ?? (rd.sharedMaterial != null ? rd.sharedMaterial.shader : null);
                var m = sh != null ? new Material(sh) : rd.material;
                m.color = color;
                if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 2.5f); }
                rd.sharedMaterial = m; rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var light = go.AddComponent<Light>(); light.type = LightType.Point; light.range = 2.5f; light.intensity = 2.5f; light.shadows = LightShadows.None;
            }
            var mat = go.GetComponent<Renderer>().sharedMaterial;
            if (mat != null && mat.color != color) { mat.color = color; if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 2.5f); var l = go.GetComponent<Light>(); if (l != null) l.color = color; }
            else { var l = go.GetComponent<Light>(); if (l != null) l.color = color; }
            go.SetActive(true); go.transform.position = pos; go.transform.localScale = Vector3.one * size;
        }

        /// <summary>Displays a bounded placement status message and logs the result.</summary>
        private void Say(string s) { _status = s; _statusUntil = Time.unscaledTime + 6f; Core.Log.Info("[Patrols] " + "Turret spot editor: " + s); }
        /// <summary>Formats the towing-vehicle or trailer turret slot label.</summary>
        private static string SlotLabel(int slot) { return slot < 3 ? "turret " + (slot + 1) : "trailer turret " + (slot - 3 + 1); }

        /// <summary>Checks whether the current placement tool has an active visible selection.</summary>
        internal bool GuiWanted() { return Core.ModApi.Get<Core.ITurretPlacement>()!=null && PatrolEditorRuntime.TurretSpotEditor != null && PatrolEditorRuntime.TurretSpotEditor.Value && _car != null; }   // Gui's gate
        /// <summary>Draws the current keyboard placement overlay without capturing the cursor.</summary>
        internal void Gui()
        {
            if (!GuiWanted() || Event.current.type != EventType.Repaint) return;
            var anchor = Core.ModApi.Get<Core.ITurretPlacement>().Anchor(_car, _slot); if (anchor == null) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperRight, richText = false };
            var p = anchor.localPosition;
            var who = Core.ModApi.Get<Core.ITurretPlacement>().Rider(_car, _slot);
            string text = "Turret spot editor  " + _car.name + "  [" + Core.ModApi.Get<Core.ITurretPlacement>().TemplateName(_car) + "]\n"
                + "> " + SlotLabel(_slot) + (who != null ? " (" + who.name + ")" : " (gone)") + "  x " + p.x.ToString("0.000") + "  y " + p.y.ToString("0.000") + "  z " + p.z.ToString("0.000") + "\n"
                + "numpad 8/2 fwd/back  4/6 left/right  7/9 down/up  (Shift = x5)   0 next turret   5 = save   . = markers"
                + (Time.unscaledTime < _statusUntil ? "\n" + _status : "");
            var rect = new Rect(Screen.width - 960, 10, 940, 90);
            var old = GUI.color; GUI.color = Color.black; GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, _style);
            GUI.color = new Color(1f, 0.9f, 0.3f); GUI.Label(rect, text, _style); GUI.color = old;
        }

        /// <summary>Releases selected visual edit ownership and destroys owned temporary marker materials.</summary>
        private void OnDestroy() { foreach (var s in _spots) Kill(s); Kill(_nose); }
        /// <summary>Destroys one owned temporary marker and its private material.</summary>
        private static void Kill(GameObject go)
        {
            if (go == null) return;
            var rd = go.GetComponent<Renderer>(); if (rd != null && rd.sharedMaterial != null) Destroy(rd.sharedMaterial);
            Destroy(go);
        }
    }
}
