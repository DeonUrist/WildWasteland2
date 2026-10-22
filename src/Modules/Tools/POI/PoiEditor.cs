using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Tools
{
    // A paused authoring session. Drafts are visual-only; saving publishes reusable POI-local placement data.
    internal sealed class PoiEditor
    {
        private IPoiAuthoring _runtime;
        private PoiEditorCatalog _catalog;
        private IModule _owner;
        private readonly List<PoiAuthoringSite> _sites = new List<PoiAuthoringSite>();
        private readonly List<string> _profiles = new List<string>();
        private BepInEx.Configuration.ConfigEntry<Key> _key;
        private readonly EditorSession _session = new EditorSession();
        private readonly List<GameObject> _visuals = new List<GameObject>();
        private readonly List<PoiAuthoringAsset> _filtered = new List<PoiAuthoringAsset>();
        private readonly Stack<EditorLayout> _undo = new Stack<EditorLayout>();
        private readonly Action _beforeNumber;
        private PoiAuthoringSite _site;
        private EditorLayout _draft;
        private GameObject _preview, _ghost;
        private EditorProp _placing;
        private PoiAuthoringAsset _asset;
        private Camera _camera;
        private Transform _cameraParent;
        private Vector3 _cameraLocal, _cameraPosition, _view;
        private Quaternion _cameraRotation;
        private float _yaw, _pitch, _grid = .1f, _height;
        private string _search = "", _status = "", _filterText;
        private int _selected = -1, _built, _previewFrame = -1, _filterCount = -1;
        private Vector2 _catalogScroll, _placedScroll;
        private bool _moving, _dirty, _validHit;
        private bool _filterComplete;
        private readonly HashSet<string> _expanded = new HashSet<string>(StringComparer.Ordinal);
        private bool _profilesOpen;
        private Vector2 _profileScroll;
        private LineRenderer _outline;
        private Material _outlineMaterial;
        private readonly Vector3[] _lines = new Vector3[16];
        internal bool Opened { get { return _site != null; } }
        private bool Ready { get { return _draft != null && _built >= _draft.Props.Count; } }
        /// <summary>Binds numeric edits to the draft undo capture callback.</summary>
        internal PoiEditor() { _beforeNumber = RecordUndo; }
        /// <summary>Registers owned input, camera, preview work and drawing with the module.</summary>
        internal void Install(IModule owner, BepInEx.Configuration.ConfigEntry<Key> key)
        {
            _owner = owner; _key = key;
            Hotkeys.Register(owner, key, Toggle, CanToggle, true);
            Scheduler.EveryFrame(owner, "POI dresser input", Tick);
            Scheduler.EveryFrame(owner, "POI dresser camera", CameraTick, Phase.LateUpdateLast);
            Scheduler.Gui(owner, "POI dresser", Draw, WantsGui);
        }
        /// <summary>Reports whether the dedicated layout editor is open.</summary>
        private bool WantsGui() { return Opened; }
        /// <summary>Allows closing an open editor or opening during authorized developer gameplay.</summary>
        private bool CanToggle() { return Opened || GameRefs.InPlay && Plugin.EnableDebug.Value && Plugin.DeveloperMode.Value; }
        /// <summary>Opens or closes the dedicated layout authoring session.</summary>
        internal void Toggle() { if (Opened) Close(); else Open(); }
        /// <summary>Acquires an editor session and loads the nearest reusable POI layout.</summary>
        internal void Open()
        {
            if (Opened || !GameRefs.InPlay || !Plugin.EnableDebug.Value || !Plugin.DeveloperMode.Value) return;
            _runtime = ModApi.Get<IPoiAuthoring>(); if (_runtime == null) { _status = "Enable POI to edit its authored layouts."; return; }
            _runtime.Discover(); _runtime.Sites(_sites); _catalog = new PoiEditorCatalog(_runtime);
            var camera = GameRefs.MainCamera; if (camera == null) return;
            PoiAuthoringSite nearest = null; float distance = 200 * 200;
            foreach (var row in _sites)
            {
                if (row.Root == null || !row.Root.gameObject.activeInHierarchy || !row.Ready) continue;
                float d = (row.Root.position - camera.transform.position).sqrMagnitude;
                if (d < distance) { nearest = row; distance = d; }
            }
            if (nearest == null) { _status = "No ready custom POI within 200 m. Approach the gas station, or wait for dressing to finish."; Log.Warn(null,"[Towns] POI dresser: " + _status); return; }
            if (nearest.Error != null) { _status = "Layout error: " + nearest.Error + ". Existing file retained."; Log.Warn(null,"[Towns] POI dresser: " + _status); return; }
            if (!_session.Acquire()) return;
            try
            {
            _camera = camera; var t = camera.transform; _cameraParent = t.parent;
            _cameraLocal = t.localPosition; _cameraPosition = t.position; _cameraRotation = t.localRotation;
            _view = t.position; _yaw = t.eulerAngles.y; _pitch = t.eulerAngles.x; if (_pitch > 180) _pitch -= 360;
            _site = nearest; _draft = nearest.LayoutJson == null ? new EditorLayout { Type = nearest.Type } : EditorLayoutJson.Parse(nearest.LayoutJson, nearest.Type); _selected = -1; _dirty = false; _undo.Clear();
            _expanded.Clear(); _catalogScroll = Vector2.zero; _profilesOpen = false; _catalog.Start();
            _runtime.SetDressingVisible(nearest.Id, false);
            _runtime.RegisterSession(_owner, BuildPreview, () => Close(true));
            NewPreview(); _catalog.Start(); Ledger.ClearNumbers();
            _status = "Choose a mesh on the left to place it. SAVE writes this POI type's layout.";
            }
            catch { if (Opened) Close(true); else _session.Close(true); throw; }
        }
        /// <summary>Replaces the unsaved preview container for the current draft.</summary>
        private void NewPreview()
        {
            if (_preview != null) { _preview.SetActive(false); Object.Destroy(_preview); }
            _preview = new GameObject("POI dresser draft"); _preview.transform.SetParent(_site.Root, false);
            _visuals.Clear(); _built = 0;
        }
        /// <summary>Builds preview objects incrementally from current draft entries.</summary>
        internal bool BuildPreview()
        {
            if (!Opened || _site.Root == null || _built >= _draft.Props.Count || _previewFrame == Time.frameCount) return false;
            _previewFrame = Time.frameCount;
            var prop = _draft.Props[_built]; var asset = _catalog.Resolve(prop);
            if (asset == null) { _status = "Missing native mesh: " + prop.Mesh; Close(); return false; }
            _visuals.Add(_catalog.Create(asset, _preview.transform, prop)); _built++; return true;
        }
        /// <summary>Processes editor input and maintains preview selection and placement state.</summary>
        private void Tick()
        {
            if (!Opened) return;
            if (!ReferenceEquals(_runtime, ModApi.Get<IPoiAuthoring>()) || _site.Root == null || _camera == null || !_session.Open || SaveBus.Busy || !Application.isFocused
                || !Plugin.EnableDebug.Value || !Plugin.DeveloperMode.Value) { Close(true); return; }
            _session.Maintain();
            var mouse = Mouse.current; if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue(); bool viewport = InViewport(screen);
            if (mouse.rightButton.isPressed && viewport)
            { Vector2 delta = mouse.delta.ReadValue(); _yaw += delta.x * .14f; _pitch = Mathf.Clamp(_pitch - delta.y * .14f, -89, 89); }
            if (GUIUtility.keyboardControl == 0)   // fly at any time, not only while looking: strafe and turn together
            {
                Vector3 step = Vector3.zero;
                if (Hotkeys.Held(Key.W)) step.z++; if (Hotkeys.Held(Key.S)) step.z--;
                if (Hotkeys.Held(Key.D)) step.x++; if (Hotkeys.Held(Key.A)) step.x--;
                var rotation = Quaternion.Euler(_pitch, _yaw, 0);
                _view += rotation * step * (Hotkeys.Held(Key.LeftShift) ? 25 : 7) * Time.unscaledDeltaTime;
                if (Hotkeys.Held(Key.E)) _view.y += 7 * Time.unscaledDeltaTime;
                if (Hotkeys.Held(Key.Q)) _view.y -= 7 * Time.unscaledDeltaTime;
            }
            if (Hotkeys.Pressed(Key.Escape)) { if (_placing != null || _moving) CancelPlacement(); else Close(); return; }
            if (Ready && GUIUtility.keyboardControl == 0)
            {
                if (Hotkeys.Pressed(Key.Delete)) Delete();
                if (Hotkeys.Held(Key.LeftCtrl) && Hotkeys.Pressed(Key.Z)) Undo();
            }
            if (Ready && viewport && (_placing != null || _moving))
            {
                CameraTick();
                var ray = _camera.ScreenPointToRay(screen); RaycastHit hit;
                _validHit = Physics.Raycast(ray, out hit, 300, Core.Layers.NavSolid, QueryTriggerInteraction.Ignore);
                if (_validHit)
                {
                    var p = _moving ? _draft.Props[_selected] : _placing;
                    p.Position = _site.Root.InverseTransformPoint(hit.point);
                    if (_grid > 0) { p.Position.x = Mathf.Round(p.Position.x / _grid) * _grid; p.Position.z = Mathf.Round(p.Position.z / _grid) * _grid; }
                    p.Position.y += _height;
                    if (_moving) { PoseSelected(); _dirty = true; }
                    else { PoiEditorCatalog.Pose(_ghost.transform, p); _ghost.SetActive(true); }
                }
                else if (_ghost != null) _ghost.SetActive(false);
            }
            Outline();
        }
        /// <summary>Checks that a screen point falls inside the editable scene viewport.</summary>
        private static bool InViewport(Vector2 point)
        { float x = point.x / Screen.width * 1600, y = (Screen.height - point.y) / Screen.height * 900; return x > 320 && x < 1280 && y > 62 && y < 844; }
        /// <summary>Updates the authoring camera from its owned view state.</summary>
        private void CameraTick()
        { if (Opened && _camera != null) _camera.transform.SetPositionAndRotation(_view, Quaternion.Euler(_pitch, _yaw, 0)); }
        /// <summary>Adjusts the authoring camera and preview after an origin jump.</summary>
        internal void Shift(Vector3 delta)
        {
            // Native origin shift moves POI parents. Saved/draft prop coordinates remain local, including the current ghost.
            if (!Opened) return; _view += delta; _cameraPosition += delta;
            if (_cameraParent == null) _cameraLocal += delta;
            if (_outline != null) { for (int i = 0; i < _lines.Length; i++) _lines[i] += delta; _outline.SetPositions(_lines); }
        }
        /// <summary>Refreshes available owner-provided spawn profiles for the marker kind.</summary>
        private List<string> Profiles(string kind) { _runtime.Profiles(kind, _profiles); return _profiles; }
        /// <summary>Selects a catalog asset and starts an unsaved placement preview.</summary>
        private void Choose(PoiAuthoringAsset asset)
        {
            CancelPlacement(); _asset = asset;
            _placing = new EditorProp { Asset = asset.Id, Mesh = asset.MeshName, Source = asset.Source, Collision = asset.HasCollision, Kind = asset.MarkerKind };
            if (_placing.Marker)
            {
                _placing.Id = Guid.NewGuid().ToString("N"); _placing.Collision = false;
                _placing.Scale = _placing.Kind == "Hide" ? new Vector3(3,2,3) : _placing.Kind == "Exit" ? new Vector3(1,2,.3f) : new Vector3(2,.2f,2);
                var profiles = Profiles(_placing.Kind);
                _placing.Profile = profiles.Count == 0 ? "" : profiles[0];
                if (_placing.Kind == "Items") _placing.Profile = "All loot";
                if (_placing.Kind == "CarParts") _placing.Profile = "Car parts";
                if (_placing.Kind == "Monsters") { _placing.Profile = "EnemySpawn_Wild_V1"; _placing.Chance = 85; }
            }
            _ghost = _catalog.Create(asset, _preview.transform, _placing); _ghost.SetActive(false);
            _selected = -1; Ledger.ClearNumbers();
        }
        /// <summary>Commits the current preview prop to the detached draft.</summary>
        private void Place()
        {
            if (!_validHit || _draft.Props.Count >= EditorLayout.Limit) return;
            if (_moving) { _moving = false; Ledger.ClearNumbers(); return; }
            RecordUndo(); _draft.Props.Add(_placing.Copy());
            if (_placing.Marker) _draft.Props[_draft.Props.Count - 1].Id = Guid.NewGuid().ToString("N");
            _visuals.Add(_catalog.Create(_asset, _preview.transform, _placing)); _built++;
            _dirty = true;
        }
        /// <summary>Finds the nearest selectable preview prop along the scene ray.</summary>
        private void Pick(Ray ray)
        {
            float closest = float.MaxValue; int selected = -1;
            for (int i = 0; i < _draft.Props.Count; i++)
            {
                var asset = _catalog.Resolve(_draft.Props[i]); if (asset == null) continue;
                var t = _visuals[i].transform;
                var localRay = new Ray(t.InverseTransformPoint(ray.origin), t.InverseTransformVector(ray.direction)); float hit;
                if (!asset.Box.IntersectRay(localRay, out hit)) continue;
                float worldDistance = (t.TransformPoint(localRay.GetPoint(hit)) - ray.origin).sqrMagnitude;
                if (worldDistance < closest) { closest = worldDistance; selected = i; }
            }
            _selected = selected; Ledger.ClearNumbers();
        }
        /// <summary>Stores a detached bounded undo snapshot before draft mutation.</summary>
        private void RecordUndo()
        { if (_draft == null) return; if (_undo.Count >= 32) _undo.Clear(); _undo.Push(_draft.Copy()); }
        /// <summary>Restores the preceding detached layout snapshot and rebuilds its preview.</summary>
        private void Undo()
        {
            if (!Ready || _undo.Count == 0) return;
            if (_moving) { CancelPlacement(); return; }
            CancelPlacement(); _draft = _undo.Pop(); _selected = -1; _dirty = true; NewPreview(); Ledger.ClearNumbers();
        }
        /// <summary>Removes the selected prop after capturing undo state.</summary>
        private void Delete()
        {
            if (!Ready || _selected < 0) return;
            if (_moving) { CancelPlacement(); return; }
            RecordUndo(); CancelPlacement(); Object.Destroy(_visuals[_selected]); _visuals.RemoveAt(_selected);
            _draft.Props.RemoveAt(_selected); _built--; _selected = -1; _dirty = true; Ledger.ClearNumbers();
        }
        /// <summary>Copies the selected prop into a new draft entry and preview.</summary>
        private void Duplicate()
        {
            if (!Ready || _selected < 0 || _draft.Props.Count >= EditorLayout.Limit) return;
            if (_moving) { CancelPlacement(); return; }
            RecordUndo(); var prop = _draft.Props[_selected].Copy(); prop.Position += Vector3.right * .5f;
            if (prop.Marker) prop.Id = Guid.NewGuid().ToString("N");
            var asset = _catalog.Resolve(prop); _draft.Props.Add(prop);
            _visuals.Add(_catalog.Create(asset, _preview.transform, prop)); _built++;
            _selected = _built - 1; _dirty = true; Ledger.ClearNumbers();
        }
        /// <summary>Discards the active uncommitted placement object.</summary>
        private void CancelPlacement()
        {
            _profilesOpen = false;
            if (_ghost != null) Object.Destroy(_ghost); _ghost = null; _placing = null; _asset = null;
            if (_moving && _undo.Count > 0)
            { _draft = _undo.Pop(); NewPreview(); _dirty = true; }
            _moving = false; _validHit = false;
        }
        /// <summary>Applies edited draft coordinates to the selected preview.</summary>
        private void PoseSelected()
        { if (_selected >= 0 && _selected < _visuals.Count) PoiEditorCatalog.Pose(_visuals[_selected].transform, _draft.Props[_selected]); }
        /// <summary>Submits the complete layout to its validating owner and rebuilds runtime instances.</summary>
        private void Save()
        {
            if (!Ready) return;
            try
            {
                CancelPlacement(); string error; if (!_runtime.Save(_site.Id, EditorLayoutJson.Serialize(_draft), out error)) throw new InvalidOperationException(error); _status = "Saved " + _draft.Props.Count + " props to " + _runtime.LayoutPath(_site.Type);
                Close();
            }
            catch (Exception e) { _status = "Save failed: " + e.Message; Log.Warn(null,"[Towns] POI dresser: " + _status); }
        }
        /// <summary>Releases the authoring session, camera, previews and provider state.</summary>
        internal void Close(bool immediate = false)
        {
            if (!Opened) return;
            if (_camera != null)
            {
                var t = _camera.transform;
                if (t.parent == _cameraParent) { t.localPosition = _cameraLocal; t.localRotation = _cameraRotation; }
                else t.position = _cameraPosition;
            }
            try { _runtime.SetDressingVisible(_site.Id, true); } catch (Exception e) { Log.Warn(null,"[Tools] POI preview restoration: " + e.Message); }
            try { _runtime.ReleaseSession(_owner); } catch (Exception e) { Log.Warn(null,"[Tools] POI session release: " + e.Message); }
            if (_preview != null) { _preview.SetActive(false); Object.Destroy(_preview); }
            if (_outline != null) Object.Destroy(_outline.gameObject);
            if (_outlineMaterial != null) Object.Destroy(_outlineMaterial);
            _outline = null; _outlineMaterial = null; _preview = _ghost = null; _placing = null; _asset = null;
            _site = null; _draft = null; _camera = null; _visuals.Clear(); _undo.Clear(); _moving = false;
            _session.Close(immediate); Ledger.ClearNumbers();
        }
        /// <summary>Draws the menu entry and availability of the dedicated POI editor.</summary>
        internal void DrawPage(ToolLayout l)
        {
            l.Entry(_key);
            l.Text("Dress a nearby custom POI with categorized vanilla scenery and hiding, exit or spawn markers. Layouts are shared by POI type; spawn progress belongs to each world instance. World is paused while editing.");
            if (l.Button("Open POI dresser", GameRefs.InPlay)) OpenFromPage();
            if (!string.IsNullOrEmpty(_status)) l.Text(_status);
            l.Value("Layout folder", _runtime != null ? _runtime.LayoutDirectory : "POI provider unavailable");
        }
        /// <summary>Releases the tool window before opening the exclusive layout editor.</summary>
        private void OpenFromPage() { ToolWindow.CloseThen(Open); }
        /// <summary>Rebuilds the visible catalog selection from category and search filters.</summary>
        private void Filter()
        {
            _catalog.Refresh();
            var all = _catalog.All;
            if (_filterCount == all.Count && _filterText == _search && _filterComplete == _catalog.Complete) return;
            _filterCount = all.Count; _filterText = _search; _filterComplete = _catalog.Complete; _filtered.Clear();
            foreach (var asset in all) if (_search.Length == 0 || asset.Label.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0) _filtered.Add(asset);
        }
        /// <summary>Draws the scaled layout editor, scene controls and catalog panels.</summary>
        private void Draw()
        {
            if (!Opened) return;
            var skin = Ledger.Begin(); var matrix = GUI.matrix; int depth = GUI.depth;
            try
            {
                GUI.depth = -1000; GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1600f, Screen.height / 900f, 1));
                Ledger.Panel(new Rect(0, 0, 1600, 60)); Ledger.Label(new Rect(18, 8, 900, 42), "POI DRESSER  /  " + _site.Type + (_dirty ? "  * unsaved" : ""), Ledger.DialogTitle);
                if (Ledger.Button(new Rect(1230, 12, 160, 36), "SAVE LAYOUT", Ready, true)) Save();
                if (!Opened) return;
                if (Ledger.Button(new Rect(1400, 12, 180, 36), "CLOSE / DISCARD")) { Close(); return; }
                Ledger.Panel(new Rect(0, 62, 318, 780)); Ledger.Panel(new Rect(1282, 62, 318, 780));
                _search = Ledger.TextField(new Rect(12, 74, 294, 32), "PoiDressSearch", _search);
                Ledger.Label(new Rect(12, 108, 294, 26), _catalog.Complete ? _filtered.Count + " native meshes" : "Indexing native meshes…", Ledger.Small);
                Filter();
                DrawCatalog();
                DrawInspector();
                if (!Opened) return;
                Ledger.Panel(new Rect(0, 846, 1600, 54));
                Ledger.Label(new Rect(16, 847, 1568, 24), "WASD: fly · RMB: look · Q/E: down/up · Shift: fast · LMB: place/select · wheel: yaw · Esc: cancel/close · Ctrl+Z: undo", Ledger.Small);
                Ledger.Label(new Rect(16, 871, 1568, 24), _status, Ledger.Small, Ledger.Muted);
                var e = Event.current;
                if (Ready && new Rect(320, 62, 960, 782).Contains(e.mousePosition))
                {
                    if (e.type == EventType.ScrollWheel && (_placing != null || _moving))
                    {
                        var p = _moving ? _draft.Props[_selected] : _placing;
                        p.Rotation.y = Mathf.Repeat(p.Rotation.y + (e.delta.y > 0 ? 15 : -15), 360); PoseSelected(); e.Use();
                    }
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        GUIUtility.keyboardControl = 0;
                        if (_placing != null || _moving) Place();
                        else if (Mouse.current != null) Pick(_camera.ScreenPointToRay(Mouse.current.position.ReadValue()));
                        e.Use();
                    }
                }
            }
            finally { GUI.skin = skin; GUI.matrix = matrix; GUI.depth = depth; }
        }
        /// <summary>Draws position, rotation, scale and marker settings for the selected prop.</summary>
        private void DrawInspector()
        {
            float x = 1294;
            Ledger.Label(new Rect(x, 74, 294, 28), "Placed props: " + _draft.Props.Count + " / " + EditorLayout.Limit);
            if (Ledger.Button(new Rect(x, 108, 142, 30), "SELECT", Ready)) CancelPlacement();
            if (Ledger.Button(new Rect(x + 150, 108, 142, 30), "UNDO", Ready && _undo.Count > 0)) Undo();
            if (!Ready) { Ledger.Label(new Rect(x, 144, 292, 30), "Building preview " + _built + "/" + _draft.Props.Count); return; }
            EditorProp prop = _placing ?? (_selected >= 0 ? _draft.Props[_selected] : null);
            if (prop != null)
            {
                Ledger.Label(new Rect(x, 144, 292, 40), prop.Marker ? prop.Mesh + " / authoring area" : prop.Mesh, Ledger.Small);
                Vector3 oldPosition = prop.Position, oldRotation = prop.Rotation, oldScale = prop.Scale;
                prop.Position = VectorFields(x, 190, "Position (POI local)", "Pos", prop.Position, -500, 500);
                prop.Rotation = VectorFields(x, 258, "Rotation (degrees)", "Rot", prop.Rotation, -3600, 3600);
                prop.Scale = VectorFields(x, 326, prop.Marker ? "Area size (POI local metres)" : "Scale multiplier", "Scale", prop.Scale, .01f, 20);
                bool collision = !prop.Marker && GUI.Toggle(new Rect(x, 396, 290, 28), prop.Collision, "Solid collider / navigation obstacle");
                if (collision != prop.Collision) { RecordUndo(); prop.Collision = collision; _dirty = true; }
                if (oldPosition != prop.Position || oldRotation != prop.Rotation || oldScale != prop.Scale) { _dirty = true; PoseSelected(); if (_ghost != null) PoiEditorCatalog.Pose(_ghost.transform, prop); }
                if (_selected >= 0)
                {
                    if (Ledger.Button(new Rect(x, 432, 92, 32), "MOVE")) { if (!_moving) RecordUndo(); _moving = true; Ledger.ClearNumbers(); }
                    if (Ledger.Button(new Rect(x + 100, 432, 92, 32), "COPY")) Duplicate();
                    if (Ledger.Button(new Rect(x + 200, 432, 92, 32), "DELETE")) Delete();
                }
            }
            Ledger.Label(new Rect(x, 476, 134, 28), "Grid (0 = free)", Ledger.Small);
            _grid = Ledger.Number(new Rect(x + 150, 476, 140, 28), "PoiGrid", _grid, 0, 10, NoChange);
            Ledger.Label(new Rect(x, 510, 140, 28), "Surface height", Ledger.Small);
            _height = Ledger.Number(new Rect(x + 150, 510, 140, 28), "PoiHeight", _height, -20, 20, NoChange);
            bool spawn = prop != null && prop.Marker && prop.Kind != "Hide" && prop.Kind != "Exit";
            if (spawn) DrawSpawnInspector(prop, x);
            else if (prop != null && prop.Kind == "Exit") Ledger.Label(new Rect(x, 545, 292, 55), "Place at a doorway. Arrow points OUT. NPC exit routes use this waypoint.", Ledger.Small);
            else if (prop != null && prop.Kind == "Hide") Ledger.Label(new Rect(x, 545, 292, 55), "Place on a walkable floor behind cover. Native Hide points fill the area.", Ledger.Small);
            float listY = spawn ? 692 : prop != null && prop.Marker ? 608 : 550;
            bool wasEnabled = GUI.enabled; if (spawn && _profilesOpen) GUI.enabled = false;
            _placedScroll = GUI.BeginScrollView(new Rect(x, listY, 298, 830 - listY), _placedScroll, new Rect(0, 0, 274, _draft.Props.Count * 30));
            int first = Mathf.Max(0, (int)(_placedScroll.y / 30)), last = Mathf.Min(_draft.Props.Count, first + 11);
            for (int i = first; i < last; i++)
                if (Ledger.Button(new Rect(0, i * 30, 272, 28), (i + 1) + ": " + _draft.Props[i].Mesh, true, i == _selected))
                { CancelPlacement(); _selected = i; Ledger.ClearNumbers(); }
            GUI.EndScrollView();
            GUI.enabled = wasEnabled;
            if (spawn && _profilesOpen) DrawProfiles(prop, x);
        }
        /// <summary>Draws category controls and filtered static or marker asset choices.</summary>
        private void DrawCatalog()
        {
            float height = 0;
            foreach (string category in EditorPalette.Categories)
            { height += 36; if (_expanded.Contains(category)) foreach (var asset in _filtered) if (asset.Category == category) height += 46; }
            _catalogScroll = GUI.BeginScrollView(new Rect(10, 140, 300, 690), _catalogScroll, new Rect(0,0,277,height));
            float y = 0;
            foreach (string category in EditorPalette.Categories)
            {
                int count = 0; foreach (var asset in _filtered) if (asset.Category == category) count++;
                bool open = _expanded.Contains(category);
                if (Ledger.Button(new Rect(0,y,276,32), (open ? "- " : "+ ") + category + " (" + count + ")", true, open))
                { if (open) _expanded.Remove(category); else _expanded.Add(category); }
                y += 36;
                if (!open) continue;
                foreach (var asset in _filtered) if (asset.Category == category)
                {
                    if (y + 44 >= _catalogScroll.y && y <= _catalogScroll.y + 690 && Ledger.Button(new Rect(10,y,266,42), asset.Label, Ready)) Choose(asset);
                    y += 46;
                }
            }
            GUI.EndScrollView();
        }
        /// <summary>Draws chance, counts and randomized-profile controls for a spawn marker.</summary>
        private void DrawSpawnInspector(EditorProp prop, float x)
        {
            int min = prop.Minimum, max = prop.Maximum, chance = prop.Chance;
            Ledger.Label(new Rect(x,548,140,25), "Count min / max", Ledger.Small);
            min = Mathf.RoundToInt(Ledger.Number(new Rect(x+144,548,70,26), "MarkerMin", min,0,32,_beforeNumber));
            max = Mathf.RoundToInt(Ledger.Number(new Rect(x+220,548,70,26), "MarkerMax", max,min,32,_beforeNumber));
            Ledger.Label(new Rect(x,580,140,25), "Spawn chance (%)", Ledger.Small);
            chance = Mathf.RoundToInt(Ledger.Number(new Rect(x+150,580,140,26), "MarkerChance",chance,0,100,_beforeNumber));
            if (min != prop.Minimum || max != prop.Maximum || chance != prop.Chance) { prop.Minimum = min; prop.Maximum = Mathf.Max(min,max); prop.Chance = chance; _dirty = true; }
            if (Ledger.Button(new Rect(x,614,290,32), "List: " + (string.IsNullOrEmpty(prop.Profile) ? "choose..." : prop.Profile) + "  v")) _profilesOpen = !_profilesOpen;
            Ledger.Label(new Rect(x,650,290,34), "One-shot per instance; native weights. 50 m arrival queues the whole POI.", Ledger.Small);
        }
        /// <summary>Draws owner-provided weighted spawn-profile choices.</summary>
        private void DrawProfiles(EditorProp prop, float x)
        {
            var profiles = Profiles(prop.Kind);
            Ledger.Panel(new Rect(x,646,292,180));
            _profileScroll = GUI.BeginScrollView(new Rect(x+3,650,286,172),_profileScroll,new Rect(0,0,266,profiles.Count*32));
            for (int i = 0; i < profiles.Count; i++) if (Ledger.Button(new Rect(0,i*32,264,30),profiles[i] + " (" + _runtime.ProfileCount(prop.Kind, profiles[i]) + ")"))
            { RecordUndo(); prop.Profile = profiles[i]; _profilesOpen = false; _dirty = true; }
            GUI.EndScrollView();
        }
        /// <summary>Provides a non-mutating callback for controls that do not need undo capture.</summary>
        private static void NoChange() { }
        /// <summary>Draws bounded three-coordinate editing controls with undo capture.</summary>
        private Vector3 VectorFields(float x, float y, string label, string key, Vector3 value, float min, float max)
        {
            Ledger.Label(new Rect(x, y, 292, 25), label, Ledger.Small);
            for (int i = 0; i < 3; i++) value[i] = Ledger.Number(new Rect(x + i * 100, y + 28, 92, 30), "Poi" + key + i, value[i], min, max, _beforeNumber);
            return value;
        }
        /// <summary>Updates the selected preview bounds outline.</summary>
        private void Outline()
        {
            Transform t = null; PoiAuthoringAsset asset = null;
            if (_placing != null && _ghost != null && _ghost.activeSelf) { t = _ghost.transform; asset = _asset; }
            else if (_selected >= 0 && _selected < _visuals.Count) { t = _visuals[_selected].transform; asset = _catalog.Resolve(_draft.Props[_selected]); }
            if (t == null || asset == null) { if (_outline != null) _outline.enabled = false; return; }
            if (_outline == null)
            {
                var shader = Shader.Find("Sprites/Default"); if (shader == null) return;
                _outlineMaterial = new Material(shader); _outlineMaterial.color = Ledger.Accent;
                _outline = new GameObject("POI dresser selection").AddComponent<LineRenderer>();
                _outline.sharedMaterial = _outlineMaterial; _outline.positionCount = 16; _outline.widthMultiplier = .025f;
                _outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; _outline.receiveShadows = false;
            }
            var min = asset.Box.min; var max = asset.Box.max;
            SetCorner(0, min.x,min.y,min.z,t); SetCorner(1,max.x,min.y,min.z,t); SetCorner(2,max.x,min.y,max.z,t); SetCorner(3,min.x,min.y,max.z,t);
            _lines[4] = _lines[0]; SetCorner(5,min.x,max.y,min.z,t); SetCorner(6,max.x,max.y,min.z,t); _lines[7] = _lines[1];
            _lines[8] = _lines[6]; SetCorner(9,max.x,max.y,max.z,t); _lines[10] = _lines[2]; _lines[11] = _lines[9];
            SetCorner(12,min.x,max.y,max.z,t); _lines[13] = _lines[3]; _lines[14] = _lines[12]; _lines[15] = _lines[5];
            _outline.enabled = true; _outline.SetPositions(_lines);
        }
        /// <summary>Transforms one local outline corner into world space.</summary>
        private void SetCorner(int i, float x, float y, float z, Transform t) { _lines[i] = t.TransformPoint(new Vector3(x,y,z)); }
    }
}
