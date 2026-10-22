using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.NPC
{
    // (WW) POI navigation editor (mouse + cursor). Every link end in view has a labelled handle: click to select, drag to move it over
    // the surfaces (a ladder's middle handle moves its rung line), mouse wheel turns the selected link. WASD / Q / E fly the camera at any
    // time (Shift = fast), the right mouse button looks around, so you can fly while dragging. The world can run while the editor is
    // open (test NPCs keep moving). Links are POI-local, so every instance of the type (also in towns) uses them; edited baked links
    // become editor links and survive rebakes.
    internal static class PoiNavEditor
    {
        enum Mode { Select, NewStart, NewEnd, SpawnNpc, SendNpcs }
        struct Handle { public int Link, Part; public Vector2 At; }   // Part 0 start, 1 end, 2 rung line
        private static readonly EditorSession _session = new EditorSession();
        private static readonly Stack<PoiNavData> _undo = new Stack<PoiNavData>();
        private static readonly List<Handle> _handles = new List<Handle>();
        private static Transform _root;
        private static string _poi, _status = "";
        private static PoiNavData _draft, _base;
        private static int _selected = -1, _dragLink = -1, _dragPart = -1, _hover = -1;
        private static Mode _mode;
        private static PoiLinkKind _newKind;
        private static Vector3 _newStart;
        private static bool _dirty;
        private static Camera _camera;
        private static Transform _cameraParent;
        private static Vector3 _cameraLocal, _cameraPosition, _view;
        private static Quaternion _cameraRotation;
        private static float _yaw, _pitch;
        private static Vector2 _scroll;
        private static readonly List<Transform> _roots = new List<Transform>();
        private static readonly Action _beforeNumber = RecordUndo;
        const float W = 1600f, H = 900f, Left = 300f, Right = 300f, Top = 52f, Bottom = 48f, HandleR = 15f;

        internal static bool Opened { get { return _root != null; } }
        internal static bool CanToggle() { return Opened || GameRefs.InPlay && Plugin.EnableDebug.Value && Plugin.DeveloperMode.Value; }
        internal static bool WantsGui() { return Opened; }
        internal static void Toggle() { if (Opened) Close(); else Open(); }

        internal static void Open()
        {
            if (Opened || !GameRefs.InPlay || !Plugin.EnableDebug.Value || !Plugin.DeveloperMode.Value) return;
            var camera = GameRefs.MainCamera; if (camera == null) return;
            var nearest = NearestRoot(camera.transform.position);
            if (nearest == null) { _status = "No POI within 200 m."; Log.Warn("[NPC] POI navigation editor: " + _status); return; }
            if (!_session.Acquire()) return;
            _camera = camera; var t = camera.transform; _cameraParent = t.parent;
            _cameraLocal = t.localPosition; _cameraPosition = t.position; _cameraRotation = t.localRotation;
            _view = t.position; _yaw = t.eulerAngles.y; _pitch = t.eulerAngles.x; if (_pitch > 180) _pitch -= 360;
            _root = nearest; _poi = PoiNavStore.TypeOf(nearest.name);
            Load();
            _status = _draft.Nodes.Count == 0 ? "Not baked yet: press REBAKE (links can still be drawn)." : "Drag a handle to move a link end · click a handle to select · wheel turns the selection.";
        }
        static Transform NearestRoot(Vector3 from)
        {
            Nav.Roots(_roots); PoiNavSetup.Roots(_roots);
            Transform nearest = null; float best = 200f * 200f;
            foreach (var r in _roots)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                float d = (r.position - from).sqrMagnitude; if (d < best) { best = d; nearest = r; }
            }
            return nearest;
        }

        static void Load()
        {
            _base = PoiNavStore.Get(_poi);
            _draft = _base != null ? _base.Copy() : new PoiNavData { Poi = _poi };
            _draft.Poi = _poi; _selected = -1; _mode = Mode.Select; _dirty = false; _undo.Clear(); _dragLink = -1; Ledger.ClearNumbers();
            PoiNavView.EditData = _draft; PoiNavView.EditRoot = _root; PoiNavView.Selected = -1;
        }

        // ------------------------------------------------------------------ per frame
        internal static void Tick()
        {
            if (!Opened) return;
            if (_root == null || _camera == null || !_session.Open || SaveBus.Busy || !Application.isFocused || !Plugin.EnableDebug.Value || !Plugin.DeveloperMode.Value)
            { Close(true); return; }
            _session.Maintain();
            if (!_dirty && PoiNavStore.Get(_poi) != _base && PoiNavSetup.Baking == null) { Load(); _status = "Rebaked: " + _draft.Nodes.Count + " nodes, " + _draft.Links.Count + " links."; }
            var mouse = Mouse.current; if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();
            float dt = Time.unscaledDeltaTime;
            if (mouse.rightButton.isPressed && InViewport(screen))
            { Vector2 delta = mouse.delta.ReadValue(); _yaw += delta.x * .14f; _pitch = Mathf.Clamp(_pitch - delta.y * .14f, -89, 89); }
            if (GUIUtility.keyboardControl == 0)
            {
                // fly at any time (not only while looking): strafe, climb and turn together
                Vector3 step = Vector3.zero;
                if (Hotkeys.Held(Key.W)) step.z++; if (Hotkeys.Held(Key.S)) step.z--;
                if (Hotkeys.Held(Key.D)) step.x++; if (Hotkeys.Held(Key.A)) step.x--;
                float speed = Hotkeys.Held(Key.LeftShift) ? 25 : 7;
                _view += Quaternion.Euler(_pitch, _yaw, 0) * step * speed * dt;
                if (Hotkeys.Held(Key.E)) _view.y += speed * dt;
                if (Hotkeys.Held(Key.Q)) _view.y -= speed * dt;
                if (Hotkeys.Pressed(Key.Delete)) Delete();
                if (Hotkeys.Held(Key.LeftCtrl) && Hotkeys.Pressed(Key.Z)) Undo();
                if (Hotkeys.Pressed(Key.P)) TogglePause();
            }
            if (Hotkeys.Pressed(Key.Escape)) { if (_mode != Mode.Select || _dragLink >= 0) { _mode = Mode.Select; _dragLink = -1; _status = "Cancelled."; } else Close(); return; }
            // dragging a handle over the surfaces
            if (_dragLink >= 0)
            {
                if (!mouse.leftButton.isPressed) { _dragLink = -1; _status = "Moved. SAVE to let NPCs use it."; }
                else
                {
                    CameraTick();
                    RaycastHit hit;
                    if (Physics.Raycast(_camera.ScreenPointToRay(screen), out hit, 400f, PoiNavBake.Mask, QueryTriggerInteraction.Ignore))
                        MovePart(_draft.Links[_dragLink], _dragPart, _root.InverseTransformPoint(hit.point));
                }
            }
            PoiNavView.Selected = _selected;
        }
        internal static void CameraTick() { if (Opened && _camera != null) _camera.transform.SetPositionAndRotation(_view, Quaternion.Euler(_pitch, _yaw, 0)); }
        static bool InViewport(Vector2 point) { Vector2 g = Gui(point); return g.x > Left && g.x < W - Right && g.y > Top && g.y < H - Bottom; }
        static Vector2 Gui(Vector2 screen) { return new Vector2(screen.x / Screen.width * W, (Screen.height - screen.y) / Screen.height * H); }
        internal static void Shift(Vector3 delta) { if (!Opened) return; _view += delta; _cameraPosition += delta; if (_cameraParent == null) _cameraLocal += delta; }
        static void TogglePause() { _session.SetPause(!_session.Paused); _status = _session.Paused ? "World paused." : "World running: test NPCs move while you edit."; }

        // ------------------------------------------------------------------ edits
        static void RecordUndo() { if (_draft == null) return; if (_undo.Count >= 64) _undo.Clear(); _undo.Push(_draft.Copy()); }
        static void Undo()
        {
            if (_undo.Count == 0) return;
            _draft = _undo.Pop(); PoiNavView.EditData = _draft; _selected = -1; _mode = Mode.Select; _dragLink = -1; _dirty = true; Ledger.ClearNumbers();
        }
        static void Changed(PoiNavLink l) { l.Manual = true; _dirty = true; }
        // every ladder link on the same rung line (all its floors) turns to the other side; they become editor links, so rebakes keep them
        static void FlipSide(PoiNavLink sel)
        {
            RecordUndo(); int n = 0;
            foreach (var o in _draft.Links)
            {
                if (o.Kind != PoiLinkKind.Ladder) continue;
                float dx = o.LineBottom.x - sel.LineBottom.x, dz = o.LineBottom.z - sel.LineBottom.z;
                if (dx * dx + dz * dz > .3f * .3f) continue;
                o.Yaw = Mathf.Repeat(o.Yaw + 180f, 360f); Changed(o); n++;
            }
            Ledger.ClearNumbers();
            _status = "Ladder flipped: " + n + " link(s) now climbed from the other side (SAVE to keep).";
        }
        static void Delete()
        {
            if (_selected < 0 || _selected >= _draft.Links.Count) return;
            RecordUndo();
            var l = _draft.Links[_selected];
            if (l.Manual) { _draft.Links.RemoveAt(_selected); _selected = -1; _status = "Editor link deleted."; }
            else { l.Off = !l.Off; _status = l.Off ? "Baked link switched off (stays off after rebakes)." : "Baked link switched on."; }
            _dirty = true; Ledger.ClearNumbers();
        }
        static void MovePart(PoiNavLink l, int part, Vector3 local)
        {
            if (part == 0) l.From = local;
            else if (part == 1) l.To = local;
            else { float gap = Mathf.Max(0f, l.LineBottom.y - l.From.y); l.LineBottom = new Vector3(local.x, l.From.y + (gap > .7f ? gap : 0f), local.z); l.LineTop = new Vector3(local.x, l.To.y, local.z); }
            if (part != 2) { l.FromSurface = l.ToSurface = -1; Shape(l, false); }   // re-attached to the nearest nodes when saved
            Changed(l); Ledger.ClearNumbers();
        }
        // derived parts: a new link faces its direction of travel; a new ladder's rung line stands half a metre in front of its foot
        static void Shape(PoiNavLink l, bool fresh)
        {
            Vector3 dir = l.To - l.From; dir.y = 0f;
            if (fresh && dir.sqrMagnitude > 1e-4f) l.Yaw = Quaternion.LookRotation(dir.normalized).eulerAngles.y;
            if (l.Kind == PoiLinkKind.Ladder)
            {
                if (fresh) { Vector3 f = Quaternion.Euler(0f, l.Yaw, 0f) * Vector3.forward * .45f; l.LineBottom = l.From + f; l.LineTop = new Vector3(l.LineBottom.x, l.To.y, l.LineBottom.z); }
                else { float gap = Mathf.Max(0f, l.LineBottom.y - l.From.y); l.LineBottom.y = l.From.y + (gap > .7f ? gap : 0f); l.LineTop.y = l.To.y; }
            }
            else { l.LineBottom = l.From; l.LineTop = l.To; }
        }

        // a click in the 3D view that is not on a handle
        static void Click(Vector2 screen)
        {
            if (_mode == Mode.Select) { _selected = -1; Ledger.ClearNumbers(); return; }
            RaycastHit hit;
            if (!Physics.Raycast(_camera.ScreenPointToRay(screen), out hit, 400f, PoiNavBake.Mask, QueryTriggerInteraction.Ignore)) { _status = "No surface under the cursor."; return; }
            Vector3 local = _root.InverseTransformPoint(hit.point);
            switch (_mode)
            {
                case Mode.SpawnNpc: _status = PoiNavSetup.SpawnTest(hit.point); _mode = Mode.Select; return;
                case Mode.SendNpcs:
                    PoiNavSetup.SendTests(hit.point); _mode = Mode.Select;
                    _status = _dirty ? "Sent - but SAVE first: NPCs use the saved data, not this draft." : "Test NPCs sent. " + (_session.Paused ? "Press P (or RUN WORLD) to watch them here." : "Watch them route.");
                    return;
                case Mode.NewStart:
                    _newStart = local; _mode = Mode.NewEnd;
                    _status = "Now click where the " + _newKind + " ends (" + (_newKind == PoiLinkKind.Ladder ? "the floor it leads to" : _newKind == PoiLinkKind.Climb ? "on top" : "below") + ").";
                    return;
                case Mode.NewEnd:
                {
                    RecordUndo();
                    var l = new PoiNavLink { Kind = _newKind, Manual = true, From = _newStart, To = local };
                    Shape(l, true);
                    _draft.Links.Add(l); _selected = _draft.Links.Count - 1; _mode = Mode.Select; _dirty = true; Ledger.ClearNumbers();
                    _status = _newKind + " added. Drag its handles to adjust, wheel turns it, SAVE when done.";
                    return;
                }
            }
        }

        static void Save()
        {
            try
            {
                _mode = Mode.Select; _dragLink = -1;
                PoiNavStore.Save(_draft);
                PoiNav.Forget(_poi); _base = PoiNavStore.Get(_poi); _draft = _base != null ? _base.Copy() : _draft; PoiNavView.EditData = _draft; _dirty = false;
                _status = "Saved. NPCs use it now.";
                Log.Always("[NPC] POI navigation saved: " + _poi + " (" + _draft.Links.Count + " links) -> " + PoiNavStore.ConfigPath(_poi));
            }
            catch (Exception e) { _status = "Save failed: " + e.Message; Log.Warn("[NPC] POI navigation editor: " + _status); }
        }

        internal static void Close(bool immediate = false)
        {
            if (!Opened) return;
            if (_camera != null)
            {
                var t = _camera.transform;
                if (t.parent == _cameraParent) { t.localPosition = _cameraLocal; t.localRotation = _cameraRotation; } else t.position = _cameraPosition;
            }
            _root = null; _draft = null; _base = null; _camera = null; _undo.Clear(); _mode = Mode.Select; _selected = -1; _dragLink = -1;
            PoiNavView.EditData = null; PoiNavView.EditRoot = null; PoiNavView.Selected = -1;
            _session.Close(immediate); Ledger.ClearNumbers();
        }

        // ------------------------------------------------------------------ the window
        static Color KindColor(PoiNavLink l)
        {
            if (l.Off) return new Color(.5f, .5f, .5f);
            return l.Kind == PoiLinkKind.Ladder ? new Color(0f, .85f, 1f) : l.Kind == PoiLinkKind.Climb ? new Color(1f, .6f, .1f) : new Color(1f, .25f, .2f);
        }
        static string KindLetter(PoiLinkKind k) { return k == PoiLinkKind.Ladder ? "L" : k == PoiLinkKind.Climb ? "C" : "D"; }

        internal static void Draw()
        {
            if (!Opened) return;
            var skin = Ledger.Begin(); var matrix = GUI.matrix; int depth = GUI.depth;
            try
            {
                GUI.depth = -1000; GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / W, Screen.height / H, 1));
                var e = Event.current;
                BuildHandles();
                bool overUi = !new Rect(Left, Top, W - Left - Right, H - Top - Bottom).Contains(e.mousePosition);
                _hover = -1;
                if (!overUi) for (int i = _handles.Count - 1; i >= 0; i--) if ((_handles[i].At - e.mousePosition).sqrMagnitude <= HandleR * HandleR * 1.6f) { _hover = i; break; }
                DrawHandles();
                DrawTop();
                if (!Opened) return;
                DrawLeft();
                DrawRight();
                Ledger.Panel(new Rect(0, H - Bottom, W, Bottom));
                Ledger.Label(new Rect(14, H - Bottom + 2, W - 28, 22), "WASD / Q E: fly (Shift fast) · RMB: look · drag a handle: move · click: select · wheel: turn · Del: delete / switch off · Ctrl+Z: undo · P: pause / run world · Esc: cancel / close", Ledger.Small);
                Ledger.Label(new Rect(14, H - Bottom + 23, W - 28, 22), (PoiNavSetup.Baking != null ? PoiNavSetup.Baking.Poi + " " + PoiNavSetup.Baking.Status + "  ·  " : "") + _status, Ledger.Small, Ledger.Muted);
                if (overUi) return;
                if (e.type == EventType.ScrollWheel && _selected >= 0)
                {
                    RecordUndo(); var l = _draft.Links[_selected];
                    l.Yaw = Mathf.Repeat(l.Yaw + (e.delta.y > 0 ? 15 : -15), 360); Changed(l); e.Use();
                }
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    GUIUtility.keyboardControl = 0;
                    if (_hover >= 0 && _mode == Mode.Select)
                    {
                        var h = _handles[_hover];
                        _selected = h.Link; RecordUndo(); _dragLink = h.Link; _dragPart = h.Part; Ledger.ClearNumbers();
                        _status = "Selected " + _draft.Links[h.Link].Kind + " " + (h.Link + 1) + " - drag to move its " + (h.Part == 0 ? "start" : h.Part == 1 ? "end" : "rung line") + ".";
                    }
                    else if (Mouse.current != null) Click(Mouse.current.position.ReadValue());
                    e.Use();
                }
            }
            finally { GUI.skin = skin; GUI.matrix = matrix; GUI.depth = depth; }
        }

        static void BuildHandles()
        {
            _handles.Clear();
            if (_draft == null || _camera == null) return;
            Vector3 eye = _camera.transform.position;
            for (int i = 0; i < _draft.Links.Count; i++)
            {
                var l = _draft.Links[i];
                if (!PoiNavView.Visible(l) && i != _selected) continue;
                Add(i, 0, l.From, eye); Add(i, 1, l.To, eye);
                if (l.Kind == PoiLinkKind.Ladder && i == _selected) Add(i, 2, (l.LineBottom + l.LineTop) * .5f, eye);
            }
        }
        static void Add(int link, int part, Vector3 local, Vector3 eye)
        {
            Vector3 w = _root.TransformPoint(local) + Vector3.up * .15f;
            if ((w - eye).sqrMagnitude > 90f * 90f) return;
            Vector3 s = _camera.WorldToScreenPoint(w); if (s.z <= .2f) return;
            _handles.Add(new Handle { Link = link, Part = part, At = Gui(new Vector2(s.x, s.y)) });
        }

        static void DrawHandles()
        {
            for (int i = 0; i < _handles.Count; i++)
            {
                var h = _handles[i]; var l = _draft.Links[h.Link];
                bool sel = h.Link == _selected, hot = i == _hover;
                float r = HandleR * (sel ? 1.25f : hot ? 1.15f : 1f);
                Color c = KindColor(l);
                var rect = new Rect(h.At.x - r, h.At.y - r, r * 2, r * 2);
                // start = filled, end = outlined, rung line = small bar
                Ledger.Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), sel ? Color.white : new Color(0f, 0f, 0f, .75f));
                if (h.Part == 0) Ledger.Fill(rect, c);
                else if (h.Part == 1) { Ledger.Fill(rect, new Color(c.r * .25f, c.g * .25f, c.b * .25f, .9f)); Ledger.Fill(new Rect(rect.x, rect.y, rect.width, 3), c); Ledger.Fill(new Rect(rect.x, rect.yMax - 3, rect.width, 3), c); Ledger.Fill(new Rect(rect.x, rect.y, 3, rect.height), c); Ledger.Fill(new Rect(rect.xMax - 3, rect.y, 3, rect.height), c); }
                else Ledger.Fill(new Rect(rect.x, h.At.y - 4, rect.width, 8), c);
                bool jump = l.Kind == PoiLinkKind.Ladder && l.LineBottom.y - l.From.y > .7f;
                string text = h.Part == 2 ? "≡" : h.Part == 0 && jump ? "J" : KindLetter(l.Kind);
                if (h.Part == 0 && jump) Ledger.Fill(new Rect(rect.x, rect.yMax + 2, rect.width, 4), new Color(1f, .9f, .1f));
                Ledger.Label(rect, text, Ledger.Small, h.Part == 0 ? Color.black : c, TextAnchor.MiddleCenter);
                if (sel || hot)
                {
                    string cap = (h.Part == 0 ? "START " : h.Part == 1 ? "END " : "RUNGS ") + l.Kind + " " + (l.To.y - l.From.y).ToString("+0.0;-0.0") + " m" + (l.Off ? " (off)" : "");
                    Ledger.Fill(new Rect(h.At.x + r + 4, h.At.y - 12, 170, 24), new Color(0f, 0f, 0f, .7f));
                    Ledger.Label(new Rect(h.At.x + r + 8, h.At.y - 12, 166, 24), cap, Ledger.Small, Color.white);
                }
            }
        }

        static void DrawTop()
        {
            Ledger.Panel(new Rect(0, 0, W, Top));
            Ledger.Label(new Rect(14, 6, 560, 40), "POI NAVIGATION  /  " + _poi + (_dirty ? "  * unsaved" : ""), Ledger.DialogTitle);
            bool baking = PoiNavSetup.Baking != null && PoiNavSetup.Baking.Root == _root;
            float x = 640;
            if (Ledger.Button(new Rect(x, 9, 150, 34), _session.Paused ? "RUN WORLD (P)" : "PAUSE WORLD (P)")) TogglePause();
            if (Ledger.Button(new Rect(x + 160, 9, 150, 34), baking ? "BAKING…" : "REBAKE", !baking && !_dirty)) { PoiNavSetup.Bake(_root); _status = "Baking " + _poi + " (keeps editor links)…"; }
            if (Ledger.Button(new Rect(x + 320, 9, 110, 34), "UNDO", _undo.Count > 0)) Undo();
            if (Ledger.Button(new Rect(x + 440, 9, 150, 34), "SAVE", _dirty, true)) Save();
            if (Ledger.Button(new Rect(x + 600, 9, 190, 34), _dirty ? "CLOSE / DISCARD" : "CLOSE")) Close();
        }

        static bool Check(float y, string label, bool value, Color? swatch = null)
        {
            if (swatch.HasValue) Ledger.Fill(new Rect(Left - 34, y + 7, 18, 18), swatch.Value);
            return Ledger.Toggle(new Rect(12, y, Left - 50, 32), value, label);
        }

        static void DrawLeft()
        {
            Ledger.Panel(new Rect(0, Top + 2, Left, H - Top - Bottom - 4));
            float y = Top + 10;
            Ledger.Label(new Rect(12, y, Left - 24, 22), _draft.Nodes.Count + " nodes · " + _draft.Surfaces + " surfaces · " + _draft.Links.Count + " links", Ledger.Small); y += 28;
            Ledger.Label(new Rect(12, y, Left - 24, 22), "SHOW", Ledger.Small, Ledger.Muted); y += 22;
            PoiNavView.ShowSurfaces = Check(y, "Surfaces (one colour each)", PoiNavView.ShowSurfaces); y += 32;
            PoiNavView.ShowLadders = Check(y, "Ladders", PoiNavView.ShowLadders, new Color(0f, .85f, 1f)); y += 32;
            PoiNavView.ShowClimbs = Check(y, "Climbs (up)", PoiNavView.ShowClimbs, new Color(1f, .6f, .1f)); y += 32;
            PoiNavView.ShowDrops = Check(y, "Drops (down)", PoiNavView.ShowDrops, new Color(1f, .25f, .2f)); y += 32;
            PoiNavView.ShowOff = Check(y, "Switched-off links", PoiNavView.ShowOff, new Color(.5f, .5f, .5f)); y += 36;
            Ledger.Label(new Rect(12, y, Left - 24, 40), "Handle: filled = START, outlined = END. L ladder, J ladder reached by a jump (yellow), C climb, D drop.", Ledger.Small, Ledger.Muted); y += 44;
            Ledger.Label(new Rect(12, y, Left - 24, 22), "ADD A LINK", Ledger.Small, Ledger.Muted); y += 22;
            float bw = (Left - 36) / 3f;
            if (Ledger.Button(new Rect(12, y, bw, 32), "LADDER", true, _mode != Mode.Select && _newKind == PoiLinkKind.Ladder)) Begin(PoiLinkKind.Ladder);
            if (Ledger.Button(new Rect(18 + bw, y, bw, 32), "CLIMB", true, _mode != Mode.Select && _newKind == PoiLinkKind.Climb)) Begin(PoiLinkKind.Climb);
            if (Ledger.Button(new Rect(24 + bw * 2, y, bw, 32), "DROP", true, _mode != Mode.Select && _newKind == PoiLinkKind.Drop)) Begin(PoiLinkKind.Drop);
            y += 44;
            Ledger.Label(new Rect(12, y, Left - 24, 22), "LINKS", Ledger.Small, Ledger.Muted); y += 22;
            float listH = H - Bottom - 150 - y;
            var vis = new List<int>();
            for (int i = 0; i < _draft.Links.Count; i++) if (PoiNavView.Visible(_draft.Links[i]) || i == _selected) vis.Add(i);
            _scroll = GUI.BeginScrollView(new Rect(10, y, Left - 20, listH), _scroll, new Rect(0, 0, Left - 44, vis.Count * 30));
            int first = Mathf.Max(0, (int)(_scroll.y / 30)), last = Mathf.Min(vis.Count, first + (int)(listH / 30) + 2);
            for (int k = first; k < last; k++)
            {
                int i = vis[k]; var l = _draft.Links[i];
                Ledger.Fill(new Rect(0, k * 30 + 6, 6, 18), KindColor(l));
                string label = (i + 1) + "  " + l.Kind + " " + (l.To.y - l.From.y).ToString("+0.0;-0.0") + " m" + (l.Manual ? " · editor" : "") + (l.Off ? " · off" : "");
                if (Ledger.Button(new Rect(10, k * 30, Left - 56, 28), label, true, i == _selected)) { _selected = i; _mode = Mode.Select; Ledger.ClearNumbers(); FocusOn(l); }
            }
            GUI.EndScrollView();
            y = H - Bottom - 140;
            Ledger.Label(new Rect(12, y, Left - 24, 22), "TEST NPCs: " + PoiNavSetup.Tests, Ledger.Small, Ledger.Muted); y += 24;
            float hw = (Left - 30) / 2f;
            if (Ledger.Button(new Rect(12, y, hw, 32), "SPAWN", true, _mode == Mode.SpawnNpc)) { _mode = Mode.SpawnNpc; _status = "Click a surface to place a test NPC (unsaved Scrapyard melee; F11 keeps it from seeing you)."; }
            if (Ledger.Button(new Rect(18 + hw, y, hw, 32), "SEND TO…", PoiNavSetup.Tests > 0, _mode == Mode.SendNpcs)) { _mode = Mode.SendNpcs; _status = "Click where the test NPCs should go (any floor, roof or walkway)."; }
            y += 38;
            if (Ledger.Button(new Rect(12, y, Left - 24, 32), "REMOVE TEST NPCs", PoiNavSetup.Tests > 0)) { PoiNavSetup.ClearTests(); _status = "Test NPCs removed."; }
        }
        static void Begin(PoiLinkKind kind)
        {
            _newKind = kind; _mode = Mode.NewStart; _selected = -1;
            _status = "Click where the " + kind + " starts (" + (kind == PoiLinkKind.Ladder ? "the floor at its foot" : kind == PoiLinkKind.Climb ? "below the ledge" : "the edge to jump from") + ").";
        }
        // fly the camera to look at a link chosen in the list
        static void FocusOn(PoiNavLink l)
        {
            Vector3 mid = _root.TransformPoint((l.From + l.To) * .5f);
            Vector3 back = Quaternion.Euler(_pitch, _yaw, 0) * Vector3.back;
            _view = mid + back * 8f + Vector3.up * 2f;
            Vector3 look = mid - _view; _yaw = Quaternion.LookRotation(look).eulerAngles.y; _pitch = Mathf.Clamp(Quaternion.LookRotation(look).eulerAngles.x, -89, 89);
            if (_pitch > 180) _pitch -= 360;
        }

        static void DrawRight()
        {
            float x = W - Right + 12, w = Right - 24;
            Ledger.Panel(new Rect(W - Right, Top + 2, Right, H - Top - Bottom - 4));
            float y = Top + 10;
            if (_selected < 0 || _selected >= _draft.Links.Count)
            {
                Ledger.Label(new Rect(x, y, w, 120), _mode == Mode.Select ? "No link selected.\n\nClick or drag a handle in the view, or pick one in the list." : "Placing… (Esc cancels)", Ledger.Small, Ledger.Muted);
                return;
            }
            var l = _draft.Links[_selected];
            Ledger.Fill(new Rect(x, y + 4, 8, 26), KindColor(l));
            Ledger.Label(new Rect(x + 16, y, w - 16, 34), (_selected + 1) + "  " + l.Kind.ToString().ToUpperInvariant() + "  " + (l.To.y - l.From.y).ToString("+0.0;-0.0") + " m", Ledger.Heading);
            y += 38;
            Ledger.Label(new Rect(x, y, w, 22), (l.Manual ? "Editor link" : "Baked link") + (l.Off ? " · switched off" : ""), Ledger.Small, Ledger.Muted); y += 30;
            Vector3 f = l.From, t = l.To; float yaw = l.Yaw;
            l.From = Fields(x, y, "Start (POI local)", "PnF", l.From); y += 66;
            l.To = Fields(x, y, "End (POI local)", "PnT", l.To); y += 66;
            Ledger.Label(new Rect(x, y, 140, 28), "Facing (deg)", Ledger.Small);
            l.Yaw = Ledger.Number(new Rect(x + 140, y, w - 140, 28), "PnYaw", l.Yaw, -720, 720, _beforeNumber); y += 40;
            if (l.Kind == PoiLinkKind.Ladder)
            {
                // a hanging ladder: how far above START its bottom rung is (over .7 m = NPCs jump up to grab it)
                float gap = l.LineBottom.y - l.From.y;
                Ledger.Label(new Rect(x, y, 140, 28), gap > .7f ? "Jump to rungs (m)" : "Rungs start (m)", Ledger.Small, gap > .7f ? new Color(1f, .9f, .1f) : (Color?)null);
                float ng = Ledger.Number(new Rect(x + 140, y, w - 140, 28), "PnGap", gap, 0f, 4f, _beforeNumber);
                if (!Mathf.Approximately(ng, gap)) { l.LineBottom.y = l.From.y + ng; Changed(l); }
                y += 40;
            }
            if (f != l.From || t != l.To || !Mathf.Approximately(yaw, l.Yaw)) { if (f != l.From || t != l.To) { l.FromSurface = l.ToSurface = -1; Shape(l, false); } Changed(l); }
            if (Ledger.Button(new Rect(x, y, w, 34), l.Manual ? "DELETE" : (l.Off ? "SWITCH ON" : "SWITCH OFF"))) Delete(); y += 42;
            if (l.Kind == PoiLinkKind.Ladder && Ledger.Button(new Rect(x, y, w, 34), "FLIP CLIMBING SIDE")) FlipSide(l); if (l.Kind == PoiLinkKind.Ladder) y += 42;
            if (Ledger.Button(new Rect(x, y, w, 34), "LOOK AT IT")) FocusOn(l); y += 50;
            Ledger.Label(new Rect(x, y, w, 160), l.Kind == PoiLinkKind.Ladder
                ? "Ladder: START = where NPCs stand at its foot, END = the floor it leads to (NPCs use it both ways). Select it to get the RUNGS handle (the line they climb). Facing points at the ladder."
                : l.Kind == PoiLinkKind.Climb ? "Climb: START below the ledge, END on top. Facing points at the wall."
                : "Drop: START at the edge, END where they land.", Ledger.Small, Ledger.Muted);
        }
        static Vector3 Fields(float x, float y, string label, string key, Vector3 v)
        {
            Ledger.Label(new Rect(x, y, 276, 24), label, Ledger.Small);
            for (int i = 0; i < 3; i++) v[i] = Ledger.Number(new Rect(x + i * 92, y + 26, 86, 30), key + i, v[i], -500, 500, _beforeNumber);
            return v;
        }

        // ------------------------------------------------------------------ F3 Debug page
        internal static void DrawPage(ToolLayout l)
        {
            l.Heading("POI pathfinding");
            l.Toggle(PoiNav.Enabled);
            l.Toggle(PoiNav.Setup);
            l.Toggle(PoiNav.Display);
            l.Value("Setup", PoiNavSetup.Active ? PoiNavSetup.Status : "off");
            if (PoiNavSetup.Active && l.Button("Bake all setup POIs", PoiNavSetup.Queued == 0)) PoiNavSetup.BakeAll();
            if (l.Button("Bake the nearest POI", GameRefs.InPlay && PoiNavSetup.Queued == 0)) BakeNearest();
            l.Value("Baking", PoiNavSetup.Baking != null ? PoiNavSetup.Baking.Poi + ": " + PoiNavSetup.Baking.Status + " (" + PoiNavSetup.Queued + " queued)" : "idle (" + PoiNavSetup.Baked + " baked, " + PoiNavSetup.Failed + " failed)");
            l.Value("NPCs on links now", PoiNavTraverse.Count.ToString());
            if (l.Button("Remove test NPCs (" + PoiNavSetup.Tests + ")", PoiNavSetup.Tests > 0)) PoiNavSetup.ClearTests();
            l.Entry(PoiNav.EditorKey);
            if (l.Button("Open POI navigation editor", GameRefs.InPlay)) ToolWindow.CloseThen(Open);
            l.Entry(PoiNav.BakeBudgetMs);
            l.Entry(PoiNav.BakeCell);
            l.Entry(PoiNav.LadderSpeed);
            l.Value("Files", PoiNavStore.ConfigDir);
            if (!string.IsNullOrEmpty(_status)) l.Text(_status);
        }
        static void BakeNearest()
        {
            var cam = GameRefs.MainCamera; if (cam == null) return;
            var nearest = NearestRoot(cam.transform.position);
            if (nearest == null) { _status = "No POI within 200 m."; return; }
            PoiNavSetup.Bake(nearest); _status = "Baking " + nearest.name;
        }
    }
}
