using WildWasteland.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Tools
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
        private Action _mutation, _afterClose;
        private int _spawnNotBefore;
        private int _spawnGeneration;
        private bool _dirty, _deleteCustom;
        private string _modal = "", _name = "", _search = "", _item = "";
        private float _itemChance = 100;
        private object _target;
        /// <summary>Reports whether this ledger owns an active authoring session.</summary>
        internal bool IsOpen { get { return _session.Open; } }
        private List<AuthoringGroup> Groups { get { return _tab == 2?PatrolEditor.Data.Merchants:_tab == 1 ? PatrolEditor.Data.Convoys : PatrolEditor.Data.Patrols; } }
        /// <summary>Finds the selected group in the active ledger tab.</summary>
        private AuthoringGroup Selected { get { return Groups.FirstOrDefault(g => g.Id == _selectedGroup); } }

        // WW: was Update (Update job). EditorSession.Tick runs in Core; the open key is a Core hotkey ([Keys] Patrol editor) -> TryOpen.
        /// <summary>Admits deferred actions after session release and handles invalid runtime state or escape input.</summary>
        internal void Tick()
        {
            if (_afterClose != null && _spawnGeneration != EditorSession.Generation) _afterClose = null;
            if (_afterClose != null && !EditorSession.Busy && Time.frameCount >= _spawnNotBefore) { var action = _afterClose; _afterClose = null; action(); }
            if (_session.Open && (PatrolEditor.Source == null || !PatrolEditor.Source.InPlay)) { Close(); return; }
            if (_session.Open) { if (Core.Hotkeys.Pressed(Key.Escape)) { if (_dropdown.IsOpen) _dropdown.Close(); else if (_modal.Length > 0) _modal = ""; else Close(); } }
        }
        // the hotkey or the F7 "Patrol editor" page; false when another editor is open or the game is not in play
        /// <summary>Acquires an exclusive editor session and loads detached current authoring data.</summary>
        internal bool TryOpen()
        {
            if (_session.Open || EditorSession.Busy || !Application.isFocused) return false;
            if (PatrolEditor.Source == null || !PatrolEditor.Source.InPlay) return false;
            if (!_session.Acquire()) return false;
            try
            {
            Skin.EnsureGameFont();
            PatrolEditor.Load(); _modal = ""; _dropdown.Close();
            _selectedGroup = _selectedTemplate = ""; _expanded.Clear();
            _leftScroll = _carScroll = _truckScroll = Vector2.zero;
            return true;
            }
            catch { Close(); throw; }
        }
        /// <summary>Drops stale group/template selections and synchronizes expanded rows.</summary>
        private void EnsureSelection()
        {
            if (Selected == null) _selectedGroup = "";
            _selectedGroup = EditorSelection.Select(_expanded, _selectedGroup, _selectedGroup.Length > 0);
            if (PatrolEditor.Find(_selectedTemplate) == null) _selectedTemplate = "";
        }
        /// <summary>Releases the authoring session and closes modal and dropdown state.</summary>
        internal void Close() { _mutation = null; _afterClose = null; _dirty = false; _session.Close(); _modal = ""; _dropdown.Close(); }
        /// <summary>Releases the authoring session and closes modal and dropdown state.</summary>
        private void OnDestroy() { _session.Close(true); }
        /// <summary>Maintains the active editor session after gameplay update.</summary>
        internal void LateTick() { _session.Maintain(); }
        /// <summary>Defers a ledger mutation until the current GUI pass finishes.</summary>
        private void Queue(Action action, bool save = true)
        {
            _mutation += () => { action(); if (save) _dirty = true; };
        }
        /// <summary>Draws the scaled ledger and then publishes queued mutations with visible failure reporting.</summary>
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
                if (_dirty) { PatrolEditor.Save(); _dirty = false; }
            }
            catch (Exception e) { _mutation = null; _dirty = false; _status = e.Message; Core.Log.Error("[Patrols] " + "Editor operation: " + e); }
        }
        /// <summary>Releases the authoring session and closes modal and dropdown state.</summary>
        private void Schedule(Action action) { Close(); _afterClose = action; _spawnNotBefore = Time.frameCount + 2; _spawnGeneration = EditorSession.Generation; }
        /// <summary>Marks the detached workspace for publication after the GUI pass.</summary>
        private void Dirty() { _dirty = true; }
    }
}
