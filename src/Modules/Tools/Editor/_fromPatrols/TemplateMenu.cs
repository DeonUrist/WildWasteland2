using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    internal sealed partial class TemplateMenu : MonoBehaviour
    {
        private readonly EditorSession _session = new EditorSession();
        private readonly HashSet<string> _expanded = new HashSet<string>();
        private readonly string[] _lootSelected = new string[2];
        private int _tab;
        private string _selectedGroup = "", _selectedTemplate = "", _status = "Select a patrol or convoy, then choose a template.";
        private Vector2 _leftScroll, _carScroll, _truckScroll, _lootScroll, _modalScroll;
        private Rect _rect;
        private Patrol _patrol;
        private Convoy _convoy;
        private Action _mutation, _afterClose;
        private int _spawnNotBefore;
        private int _spawnGeneration;
        private bool _dirty, _deleteCustom;
        private string _modal = "", _name = "", _search = "", _item = "";
        private float _itemChance = 100;
        private object _target;
        internal bool IsOpen { get { return _session.Open; } }
        private List<SpawnGroup> Groups { get { return _tab == 2?EditorStore.Data.Merchants:_tab == 1 ? EditorStore.Data.Convoys : EditorStore.Data.Patrols; } }
        private SpawnGroup Selected { get { return Groups.FirstOrDefault(g => g.Id == _selectedGroup); } }

        // WW: was Update (Update job). EditorSession.Tick runs in Core; the open key is a Core hotkey ([Keys] Patrol editor) -> TryOpen.
        internal void Tick()
        {
            if (_afterClose != null && _spawnGeneration != EditorSession.Generation) _afterClose = null;
            if (_afterClose != null && !EditorSession.Busy && Time.frameCount >= _spawnNotBefore) { var action = _afterClose; _afterClose = null; action(); }
            if (_session.Open) { if (PatrolsModule.Pressed(Key.Escape)) { if (_dropdown.IsOpen) _dropdown.Close(); else if (_modal.Length > 0) _modal = ""; else Close(); } }
        }
        // the hotkey or the F7 "Patrol editor" page; false when another editor is open or the game is not in play
        internal bool TryOpen()
        {
            if (_session.Open || EditorSession.Busy || !Application.isFocused) return false;
            if (_patrol == null) _patrol = GetComponent<Patrol>();
            if (_patrol == null || !_patrol.InGame()) return false;
            if (!_session.Acquire()) return false;
            PatrolsModule.EnsureGameFont();
            _convoy = GetComponent<Convoy>(); CarTemplates.Refresh(false); _modal = ""; _dropdown.Close();
            _selectedGroup = _selectedTemplate = ""; _expanded.Clear();
            _leftScroll = _carScroll = _truckScroll = Vector2.zero;
            return true;
        }
        private void EnsureSelection()
        {
            if (Selected == null) _selectedGroup = "";
            _selectedGroup = EditorSelection.Select(_expanded, _selectedGroup, _selectedGroup.Length > 0);
            if (CarTemplate.Find(_selectedTemplate) == null) _selectedTemplate = "";
        }
        private void Close() { _session.Close(); _modal = ""; _dropdown.Close(); }
        private void OnDestroy() { _session.Close(true); }
        internal void LateTick() { _session.Maintain(); }
        private void Queue(Action action, bool save = true)
        {
            _mutation += () => { action(); if (save) _dirty = true; };
        }
        internal void Gui()
        {
            if (!_session.Open) return;
            _session.Maintain(); GUI.depth = -900;
            float s = Mathf.Min((Screen.width - 32f) / Ledger.Width, (Screen.height - 32f) / Ledger.Height);
            var old = Ledger.Begin(); var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Ledger.Width * s) / 2, (Screen.height - Ledger.Height * s) / 2), Quaternion.identity, new Vector3(s, s, 1));
            _rect = new Rect(0, 0, Ledger.Width, Ledger.Height);
            try { GUI.Window(GetInstanceID(), _rect, DrawWindow, "", GUIStyle.none); }
            finally { GUI.skin = old; GUI.matrix = matrix; }
            _dropdown.Flush();
            try
            {
                if (_mutation != null) { var action = _mutation; _mutation = null; action(); }
                if (_dirty) { EditorStore.Save(); _dirty = false; }
            }
            catch (Exception e) { _mutation = null; _dirty = false; _status = e.Message; Core.Log.Error("[Patrols] " + "Editor operation: " + e); }
        }
        private void Schedule(Action action) { Close(); _afterClose = action; _spawnNotBefore = Time.frameCount + 2; _spawnGeneration = EditorSession.Generation; }
        private void Dirty() { _dirty = true; }
    }
}
