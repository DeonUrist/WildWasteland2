using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Tools
{
    /// <summary>Edits reusable town rules through the POI-owned validated template store.</summary>
    internal sealed class TownTemplatePage
    {
        private readonly List<string> _ids = new List<string>(), _pois = new List<string>();
        private readonly HashSet<string> _allowed = new HashSet<string>(StringComparer.Ordinal), _parking = new HashSet<string>(StringComparer.Ordinal);
        private static readonly string[] Factions = { "Animals", "Mutants", "Scrapyard", "Coyotes" };
        private ITownsApi _source;
        private string _selected, _newName = "", _status = "", _search = "";
        private Dictionary<string, object> _draft;
        private bool _dirty, _allowedOpen, _parkingOpen;
        private Vector2 _allowedScroll, _parkingScroll;

        internal void Draw(ToolLayout layout)
        {
            var api = ModApi.Get<ITownsApi>();
            if (api == null) { layout.Text("Enable POI to edit town templates."); return; }
            if (!ReferenceEquals(api, _source)) { _source = api; _draft = null; _selected = null; _dirty = false; }
            api.Templates(_ids);
            layout.Text("Keep the four defaults or add your own town types. Changes affect future towns; saved towns retain their plans.");
            int index = _ids.IndexOf(_selected);
            if (_ids.Count > 0)
            {
                int next = layout.Choices("Town type", index < 0 ? 0 : index, _ids.ToArray());
                if (_draft == null || index != next)
                { if (_dirty) _status = "Save or discard this draft before choosing another type."; else Load(api, _ids[Mathf.Clamp(next, 0, _ids.Count - 1)]); }
            }
            layout.Text("New type name (letters, numbers, _ or -):");
            _newName = Ledger.TextField(layout.Next(), "NewTownType", _newName);
            if (layout.Button("Add town type", _draft != null && !_dirty && _newName.Trim().Length > 0))
            {
                string id = _newName.Trim();
                if (_ids.Contains(id)) _status = "A town type with that name already exists.";
                else
                {
                    var data = Json.ParseObject(Json.Write(_draft)); data["Id"] = id; data["DisplayName"] = id;
                    if (api.SaveTemplate(id, Json.Write(data))) { Load(api, id); _newName = ""; _status = "Created " + id + ". Set its faction and POIs below."; }
                    else _status = "Could not create type. Use at most 64 letters, numbers, _ or -.";
                }
            }
            if (_draft == null) { layout.Text(_status); return; }
            object faction; string current = _draft.TryGetValue("Faction", out faction) ? faction as string : "Animals";
            int selected = Mathf.Max(0, Array.IndexOf(Factions, current));
            int changed = layout.Choices("Faction", selected, Factions);
            if (changed != selected) { _draft["Faction"] = Factions[changed]; _dirty = true; }
            EditFlag(layout, "Walls", "Walls", true); EditFlag(layout, "RequireMerchant", "Require merchant lot", false);
            if (layout.Button((_allowedOpen ? "Hide" : "Select") + " in-town POIs (" + _allowed.Count + ")")) { _allowedOpen = !_allowedOpen; RefreshPois(); }
            if (_allowedOpen) Picker(layout, _allowed, ref _allowedScroll, false);
            if (layout.Button((_parkingOpen ? "Hide" : "Select") + " parking POIs (" + _parking.Count + ")")) { _parkingOpen = !_parkingOpen; RefreshPois(); }
            if (_parkingOpen) Picker(layout, _parking, ref _parkingScroll, true);
            layout.Value("Draft", _dirty ? "Unsaved changes" : "Matches saved template");
            int action = layout.Buttons("Save template", "Discard changes");
            if (action == 0)
            {
                _draft["AllowedPois"] = Names(_allowed); _draft["ExternalParkingPois"] = Names(_parking);
                if (api.SaveTemplate(_selected, Json.Write(_draft))) { _dirty = false; Load(api, _selected); _status = "Saved " + _selected + ". Future town plans use these rules."; }
                else _status = "Save rejected. Parking allows at most 29 POIs, with at most 32 total lots. Previous file retained; see the log.";
            }
            else if (action == 1) { _dirty = false; Load(api, _selected); }
            if (_status.Length > 0) layout.Text(_status);
        }
        private void EditFlag(ToolLayout layout, string key, string label, bool fallback)
        { object value; bool old = _draft.TryGetValue(key, out value) && value is bool ? (bool)value : fallback; bool next = layout.Toggle(label, old); if (old != next) { _draft[key] = next; _dirty = true; } }
        private void RefreshPois()
        {
            _pois.Clear(); ModApi.Get<IPoiAuthoring>()?.Types(_pois);
            foreach (string name in _allowed) if (!_pois.Contains(name)) _pois.Add(name);
            foreach (string name in _parking) if (!_pois.Contains(name)) _pois.Add(name);
            _pois.Sort(StringComparer.Ordinal);
        }
        private void Picker(ToolLayout layout, HashSet<string> selected, ref Vector2 scroll, bool parking)
        {
            layout.Text(parking ? "Checked POIs are companion lots outside the walls." : "Check this town's POIs. No checks uses the normal pool. Native POIs need a navigation bake; custom POIs bake when saved.");
            _search = Ledger.TextField(layout.Next(), parking ? "ParkingPoiSearch" : "TownPoiSearch", _search);
            var rows = _pois.FindAll(p => _search.Length == 0 || p.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
            Rect area = layout.Next(240); scroll = GUI.BeginScrollView(area, scroll, new Rect(0, 0, area.width - 24, rows.Count * 38));
            for (int i = 0; i < rows.Count; i++)
            {
                string name = rows[i]; bool old = selected.Contains(name);
                bool next = GUI.Toggle(new Rect(4, i * 38, area.width - 32, 36), old, name);
                if (old != next) { if (next) selected.Add(name); else selected.Remove(name); _dirty = true; }
            }
            GUI.EndScrollView();
        }
        private void Load(ITownsApi api, string id)
        {
            string json; if (!api.TryGetTemplate(id, out json)) { _status = "Template unavailable: " + id; return; }
            _draft = Json.ParseObject(json); _selected = id; ReadNames("AllowedPois", _allowed); ReadNames("ExternalParkingPois", _parking); _dirty = false; RefreshPois();
        }
        private void ReadNames(string key, HashSet<string> into)
        { into.Clear(); object value; var rows = _draft.TryGetValue(key, out value) ? value as List<object> : null; if (rows != null) foreach (var row in rows) if (row is string) into.Add((string)row); }
        private static object[] Names(HashSet<string> names)
        { var rows = new List<string>(names); rows.Sort(StringComparer.Ordinal); return rows.ConvertAll<object>(s => s).ToArray(); }
    }
}
