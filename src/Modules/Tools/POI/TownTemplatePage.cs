using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Tools
{
    /// <summary>Edits reusable faction town rules through the POI-owned validated template store.</summary>
    internal sealed class TownTemplatePage
    {
        private readonly List<string> _ids = new List<string>();
        private ITownsApi _source;
        private string _selected, _allowed = "", _parking = "", _status = "";
        private Dictionary<string, object> _draft;
        private bool _dirty;
        /// <summary>Lists faction/custom templates and edits walls, town lots and outside parking without rerolling existing towns.</summary>
        internal void Draw(ToolLayout layout)
        {
            var api = ModApi.Get<ITownsApi>();
            if (api == null) { layout.Text("Enable POI to edit town templates."); return; }
            if (!ReferenceEquals(api, _source)) { _source = api; _draft = null; _selected = null; _dirty = false; }
            api.Templates(_ids);
            layout.Text("Choose Animals, Mutants, Scrapyard, Coyotes or a custom template. Saved changes apply to future town planning. Existing towns keep their plans.");
            int index = _ids.IndexOf(_selected); int next = layout.Choices("Town type", index < 0 ? 0 : index, _ids.ToArray());
            if (_ids.Count == 0) return;
            if (_draft == null || index != next)
            {
                if (_dirty) { _status = "Save or discard the current draft before choosing another type."; }
                else Load(api, _ids[Mathf.Clamp(next, 0, _ids.Count - 1)]);
            }
            if (_draft == null) { layout.Text(_status); return; }
            bool walls = Flag(_draft, "Walls", true), changedWalls = layout.Toggle("Walls", walls);
            if (walls != changedWalls) { _draft["Walls"] = changedWalls; _dirty = true; }
            bool merchant = Flag(_draft, "RequireMerchant", false), changedMerchant = layout.Toggle("Require merchant lot", merchant);
            if (merchant != changedMerchant) { _draft["RequireMerchant"] = changedMerchant; _dirty = true; }
            layout.Heading("Allowed POIs, including custom POI type names");
            layout.Text("Enter one exact native or custom POI type name per line. Empty permits the normal faction-compatible pool.");
            string allowed = GUI.TextArea(layout.Next(100), _allowed);
            if (allowed != _allowed) { _allowed = allowed; _dirty = true; }
            layout.Heading("POIs usable as outside parking lots");
            layout.Text("Enter one exact POI type per line. These lots remain outside the walls.");
            string parking = GUI.TextArea(layout.Next(80), _parking);
            if (parking != _parking) { _parking = parking; _dirty = true; }
            layout.Value("Draft", _dirty ? "Unsaved changes" : "Matches saved template");
            int action = layout.Buttons("Save template", "Discard changes");
            if (action == 0)
            {
                try
                {
                    _draft["AllowedPois"] = Names(_allowed); _draft["ExternalParkingPois"] = Names(_parking);
                    if (!api.SaveTemplate(_selected, Json.Write(_draft))) throw new InvalidOperationException("The POI provider rejected the template. Check the log; the previous file is retained.");
                    _dirty = false; Load(api, _selected); _status = "Saved " + _selected + ". Future town plans use these rules.";
                }
                catch (Exception error) { _status = "Save failed: " + error.Message; }
            }
            else if (action == 1) { _dirty = false; Load(api, _selected); }
            if (_status.Length > 0) layout.Text(_status);
        }
        /// <summary>Loads a detached provider document while preserving fields not exposed by this page.</summary>
        private void Load(ITownsApi api, string id)
        {
            string json; if (!api.TryGetTemplate(id, out json)) { _status = "Template unavailable: " + id; return; }
            _draft = Json.ParseObject(json); _selected = id;
            _allowed = Lines(_draft, "AllowedPois"); _parking = Lines(_draft, "ExternalParkingPois"); _dirty = false;
        }
        /// <summary>Reads an optional boolean from the detached template document.</summary>
        private static bool Flag(Dictionary<string, object> data, string key, bool fallback) { object value; return data.TryGetValue(key, out value) && value is bool ? (bool)value : fallback; }
        /// <summary>Converts a JSON name list to the editable text field.</summary>
        private static string Lines(Dictionary<string, object> data, string key) { object value; var rows = data.TryGetValue(key, out value) ? value as List<object> : null; if (rows == null) return ""; var lines = new List<string>(); foreach (var row in rows) if (row is string) lines.Add((string)row); return string.Join("\n", lines); }
        /// <summary>Normalizes exact type names, retaining input order and rejecting excessive authoring lists.</summary>
        private static object[] Names(string text) { var rows = new List<object>(); var seen = new HashSet<string>(StringComparer.Ordinal); foreach (var line in text.Replace("\r", "").Split('\n')) { string name = line.Trim(); if (name.Length == 0 || !seen.Add(name)) continue; if (name.Length > 256 || rows.Count >= 256) throw new FormatException("At most 256 POI names of 256 characters are supported."); rows.Add(name); } return rows.ToArray(); }
    }
}
