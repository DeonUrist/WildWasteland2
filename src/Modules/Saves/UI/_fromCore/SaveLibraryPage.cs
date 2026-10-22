using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace WildWasteland.Core.UI
{
    internal sealed class SaveLibraryPage : ToolPage
    {
        internal static readonly SaveLibraryPage Instance = new SaveLibraryPage();
        readonly List<SaveSystem.SaveInfo> _saves = new List<SaveSystem.SaveInfo>();
        readonly List<int> _visible = new List<int>();
        readonly List<string> _backups = new List<string>();
        readonly Dictionary<string, Texture2D> _pictures = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, string[]> _backupLabels = new Dictionary<string, string[]>();
        string _name = "", _world = "", _search = "", _message, _confirmFile, _confirmKind;
        string _confirmLabel;
        int _editorGeneration = -1;
        bool _saving;
        int _kind, _page, _selected = -1;
        SaveLibraryPage() { Title = "SAVES"; }
        internal void Configure(bool saving)
        { _saving = saving; _kind = _page = 0; _search = ""; }
        public override void OnShow()
        {
            _confirmFile = _confirmKind = null;
            if (_editorGeneration != SaveBus.Generation) { _name = ""; _editorGeneration = SaveBus.Generation; }
            if (SaveSystem.LocalWorld) { _world = SaveSystem.CurrentWorldName; }
            Refresh();
        }
        internal void ReleasePictures()
        { foreach (var image in _pictures.Values) if (image != null) UnityEngine.Object.Destroy(image); _pictures.Clear(); }
        void Refresh()
        {
            SaveSystem.CatalogueVersion++; _saves.Clear(); _selected = -1; ReleasePictures();
            try { foreach (var save in SaveRuntime.Saves()) _saves.Add(SaveSystem.Info(save.File, save.Name)); _saves.Sort(Newest); _message = null; Filter(); }
            catch (Exception error) { _message = error.Message; }
        }
        static int Newest(SaveSystem.SaveInfo a, SaveSystem.SaveInfo b)
        { return File.GetLastWriteTimeUtc(SaveFiles.WorldPath(b.File)).CompareTo(File.GetLastWriteTimeUtc(SaveFiles.WorldPath(a.File))); }
        void Filter()
        {
            _visible.Clear();
            for (int i = 0; i < _saves.Count; i++)
            { var s = _saves[i]; if (s.Automatic == (_kind == 1) && (_search.Length == 0 || s.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 || s.World.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)) _visible.Add(i); }
            _page = Mathf.Clamp(_page, 0, Mathf.Max(0, (_visible.Count - 1) / 5));
        }
        Texture2D Picture(string file)
        {
            Texture2D picture = null; if (_pictures.TryGetValue(file, out picture)) return picture;
            if (_pictures.Count >= 32) ReleasePictures();
            try { string path = SaveFiles.ImagePath(file); if (File.Exists(path)) { picture = new Texture2D(2, 2, TextureFormat.RGB24, false); if (!picture.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(picture); picture = null; } } }
            catch (Exception error) { if (picture != null) UnityEngine.Object.Destroy(picture); picture = null; _message = "Preview unavailable: " + error.Message; }
            _pictures[file] = picture; return picture;
        }
        void Select(int index) { _selected = index; _name = _saves[index].Name; if (!SaveSystem.LocalWorld) _world = _saves[index].World; }
        static void Text(Rect r, string text, bool muted = false, bool heading = false)
        { Ledger.Label(r, text, heading ? Ledger.Heading : Ledger.Small, muted ? Ledger.Muted : Ledger.Text); }
        static bool Button(Rect r, string text, bool enabled = true, bool primary = false, bool danger = false)
        {
            bool before = GUI.enabled; GUI.enabled = before && enabled; bool hover = GUI.enabled && r.Contains(Event.current.mousePosition); Color accent = danger ? Ledger.Danger : Ledger.Accent;
            Ledger.Panel(r, primary ? new Color(accent.r, accent.g, accent.b, .16f) : Ledger.Field, hover || primary ? accent : Ledger.Line);
            Ledger.Label(r, text, Ledger.GameButton, !GUI.enabled ? Ledger.Muted : danger || primary || hover ? accent : Ledger.Text, TextAnchor.MiddleCenter);
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none); GUI.enabled = before; return hit;
        }
        void Tab(Rect r, string text, int kind)
        { if (Ledger.Choice(r, text, _kind == kind) && _kind != kind) { _kind = kind; _page = 0; _selected = -1; Filter(); if (kind == 2) DeletedBackups(); } }
        void Input(Rect r, string label, string key, ref string value)
        {
            Text(new Rect(r.x, r.y, r.width, 26), label, true); var field = new Rect(r.x, r.y + 30, r.width, 46); Ledger.Panel(field, Ledger.Field, Ledger.Line);
            string next = Ledger.TextField(field, key, value); value = next.Length > 64 ? next.Substring(0, 64) : next;
        }
        public override void Draw(ToolLayout l) { l.Text("Open the save library from SAVE GAME or LOAD GAME."); }
        internal void DrawLibrary()
        {
            bool enabled = GUI.enabled; GUI.enabled = enabled && _confirmFile == null;
            try
            {
                Text(new Rect(100, 68, 900, 54), _saving ? "SAVE GAME" : "LOAD GAME", false, true);
                if (Button(new Rect(1660, 78, 160, 54), "BACK")) { ToolWindow.Close(); return; }
                Tab(new Rect(100, 188, 280, 54), "MANUAL SAVES", 0); Tab(new Rect(396, 188, 240, 54), "AUTOSAVES", 1); Tab(new Rect(652, 188, 220, 54), "BACKUPS", 2);
                if (Button(new Rect(940, 188, 220, 54), SaveSystem.RecoveryError != null ? "RETRY RECOVERY" : "REFRESH")) { if (SaveSystem.RecoveryError != null) SaveSystem.RecoverFiles(); Refresh(); }
                if (_kind == 2) DrawBackups(); else DrawSaves();
                DrawDetails();
                string status = SaveSystem.RecoveryError ?? (SaveSystem.ReloadRequired ? "The last save failed. Reload before saving again; previous files are retained." : _message);
                Text(new Rect(100, 1004, 1720, 50), status ?? "Autosaves stay separate. Deleting a save keeps a recoverable backup.", true);
            }
            finally { GUI.enabled = enabled; }
            if (_confirmFile != null) DrawConfirmation();
        }
        
        internal static void SaveColumns(Rect r)
        {
            float s = r.width / 1060;
            Text(new Rect(r.x + 170 * s, r.y, 320 * s, 32), "SAVE NAME", true); Text(new Rect(r.x + 510 * s, r.y, 260 * s, 32), "WORLD", true); Text(new Rect(r.x + 780 * s, r.y, 135 * s, 32), "TRAVELLED", true);
        }
        internal bool SaveRow(Rect r, SaveSystem.SaveInfo save, bool selected)
        {
            float s = r.width / 1060;
            Ledger.Panel(r, selected ? Ledger.Raised : Ledger.Surface, selected ? Ledger.Accent : r.Contains(Event.current.mousePosition) ? Ledger.Muted : Ledger.Line);
            if (selected) Ledger.Fill(new Rect(r.x, r.y, 4, r.height), Ledger.Accent);
            var image = new Rect(r.x + 10 * s, r.y + 10, 140 * s, 76); Ledger.Fill(image, Ledger.Field); var picture = Picture(save.File);
            if (picture != null) GUI.DrawTexture(image, picture, ScaleMode.ScaleToFit); else Text(image, "NO PREVIEW", true);
            Text(new Rect(r.x + 170 * s, r.y + 10, 320 * s, 37), save.Name, false, true); Text(new Rect(r.x + 170 * s, r.y + 52, 320 * s, 30), save.Date, true);
            Text(new Rect(r.x + 510 * s, r.y + 12, 260 * s, 66), save.World); Text(new Rect(r.x + 780 * s, r.y + 12, 260 * s, 66), save.Kilometres.HasValue ? save.Distance : "—");
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }
        void DrawSaves()
        {
            Text(new Rect(100, 265, 150, 44), "SEARCH", true); var search = new Rect(250, 265, 910, 44); Ledger.Panel(search, Ledger.Field, Ledger.Line);
            string next = Ledger.TextField(search, "SaveLibrary/Search", _search); if (next != _search) { _search = next; _page = 0; _selected = -1; Filter(); }
            SaveColumns(new Rect(100, 325, 1060, 32));
            for (int row = 0; row < 5 && _page * 5 + row < _visible.Count; row++)
            {
                int index = _visible[_page * 5 + row]; var save = _saves[index]; var r = new Rect(100, 366 + row * 106, 1060, 96);
                if (SaveRow(r, save, index == _selected)) Select(index);
            }
            if (_visible.Count == 0) Text(new Rect(150, 495, 940, 100), _search.Length > 0 ? "No saves match your search." : _kind == 1 ? "No autosaves yet." : "No manual saves yet. Your next save will appear here.", true);
            int pages = Mathf.Max(1, (_visible.Count + 4) / 5);
            if (Button(new Rect(100, 920, 170, 48), "PREVIOUS", _page > 0)) _page--; Text(new Rect(305, 920, 650, 48), (_page + 1) + " / " + pages + "    ·    " + _visible.Count + " saves", true);
            if (Button(new Rect(990, 920, 170, 48), "NEXT", _page + 1 < pages)) _page++;
        }
        bool CanSave { get { return SaveSystem.LocalReady && !SaveSystem.Busy && !SaveSystem.Requested && SaveSystem.Ready && SaveSystem.RecoveryError == null && !SaveSystem.ReloadRequired; } }
        void DrawDetails()
        {
            var save = _selected >= 0 && _selected < _saves.Count ? _saves[_selected] : null; Ledger.Panel(new Rect(1200, 188, 620, 780), Ledger.Surface, Ledger.Line);
            var image = new Rect(1224, 212, 572, 288); Ledger.Fill(image, Ledger.Field); var picture = save != null ? Picture(save.File) : null;
            if (picture != null) GUI.DrawTexture(image, picture, ScaleMode.ScaleToFit); else Text(new Rect(image.x + 24, image.y + 80, image.width - 48, 120), save != null ? "No screenshot was recorded for this save." : "Select a save to preview it.", true);
            if (_saving)
            {
                Input(new Rect(1224, 520, 572, 76), "SAVE NAME", "SaveLibrary/Name", ref _name);
                Input(new Rect(1224, 610, 572, 76), "WORLD NAME", "SaveLibrary/World", ref _world);
                if (Button(new Rect(1224, 712, 572, 54), "CREATE NEW SAVE", CanSave && _name.Trim().Length > 0 && _world.Trim().Length > 0, true)) Save(null);
                if (Button(new Rect(1224, 778, 572, 50), "OVERWRITE SELECTED", CanSave && save != null && !save.Automatic && _name.Trim().Length > 0)) Confirm("overwrite", save.File);
            }
            else
            {
                Text(new Rect(1224, 520, 572, 44), save != null ? save.Name : _kind == 2 ? "Backup recovery" : "Choose a save", false, true);
                if (save != null) { Text(new Rect(1224, 575, 572, 36), save.World); Text(new Rect(1224, 620, 572, 36), save.Distance, true); Text(new Rect(1224, 662, 572, 36), save.Date, true); }
                bool canLoad = save != null && (SaveRuntime.InMainMenu || SaveSystem.LocalReady) && SaveSystem.Ready && SaveSystem.RecoveryError == null && !SaveSystem.Busy && !SaveSystem.Requested;
                if (Button(new Rect(1224, 712, 572, 54), "LOAD SAVE", canLoad, true))
                { if (!SaveRuntime.InMainMenu) Confirm("load", save.File); else { string file = save.File; ToolWindow.CloseThen(() => SinglePlayerSaving.Load(file)); } return; }
                if (CanSave && Button(new Rect(1224, 778, 572, 50), "MAKE A NEW SAVE")) { _saving = true; return; }
            }
            bool deletable = save != null && !(SaveSystem.LocalWorld && save.File == SaveSystem.ActiveFile) && !SaveSystem.Busy && !SaveSystem.Requested;
            if (Button(new Rect(1224, 900, 276, 44), "DELETE SAVE", deletable, false, true)) Confirm("delete", save.File);
            if (Button(new Rect(1516, 900, 280, 44), "SAVE HISTORY", save != null))
            { try { _backups.Clear(); _backupLabels.Clear(); _backups.AddRange(SaveSystem.Backups(save.File)); _kind = 2; _page = 0; } catch (Exception error) { _message = error.Message; } }
        }
        void DeletedBackups()
        {
            try
            {
                _backups.Clear(); _backupLabels.Clear();
                foreach (string path in SaveSystem.DeletedBackups())
                { string file = Path.GetFileName(Path.GetDirectoryName(path)); if (BackupLabel(path, file)[2] == "sp") _backups.Add(path); }
            }
            catch (Exception error) { _message = error.Message; }
        }
        string[] BackupLabel(string path, string file)
        {
            string[] label; if (_backupLabels.TryGetValue(path, out label)) return label;
            var info = SaveSystem.Info(file); string name = File.Exists(SaveFiles.WorldPath(file)) ? info.Name : "Deleted save", mode = "sp";
            var backup = SaveFiles.Read(path);
            for (int i = 0; i < backup.Entries.Count; i++) if (backup.Entries[i].Path == SaveFiles.MetaPath(file) && backup.Entries[i].Existed)
            { var lines = File.ReadAllLines(Path.Combine(path, "old-" + i + ".bin")); if (lines.Length >= 2 && lines[0] == "WW-SAVE-1") { name = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(lines[1])); if (lines.Length >= 9) mode = lines[7]; } break; }
            label = new[] { name, Directory.GetCreationTime(path).ToString("yyyy-MM-dd HH:mm:ss"), mode }; _backupLabels[path] = label; return label;
        }
        void DrawBackups()
        {
            Text(new Rect(100, 265, 1060, 60), "Restore previous versions or deleted saves. Current files are backed up first.", true);
            for (int row = 0; row < 5 && _page * 5 + row < _backups.Count; row++)
            {
                string path = _backups[_page * 5 + row], file = Path.GetFileName(Path.GetDirectoryName(path)); var r = new Rect(100, 366 + row * 106, 1060, 96); Ledger.Panel(r, Ledger.Surface, Ledger.Line);
                string[] label; try { label = BackupLabel(path, file); } catch (Exception error) { _message = error.Message; label = new[] { "Backup needs attention", "Open the backup folder to inspect it." }; _backupLabels[path] = label; }
                Text(new Rect(124, r.y + 8, 700, 38), label[0], false, true); Text(new Rect(124, r.y + 50, 700, 32), label[1], true);
                if (Button(new Rect(856, r.y + 22, 280, 52), "RESTORE", SaveRuntime.InMainMenu)) Confirm("restore", path);
            }
            if (_backups.Count == 0) Text(new Rect(150, 495, 940, 100), "No previous versions here yet. Backups appear after an overwrite or deletion.", true);
            if (Button(new Rect(100, 920, 220, 48), "OPEN FOLDER")) OpenBackups(); if (Button(new Rect(340, 920, 220, 48), "DELETED SAVES")) { DeletedBackups(); _page = 0; }
            if (Button(new Rect(710, 920, 210, 48), "PREVIOUS", _page > 0)) _page--; if (Button(new Rect(940, 920, 220, 48), "NEXT", (_page + 1) * 5 < _backups.Count)) _page++;
        }
        void Confirm(string kind, string file)
        { _confirmKind = kind; _confirmFile = file; var info = kind == "restore" ? null : SaveSystem.Info(file); _confirmLabel = info != null ? info.Name + "  ·  " + info.World : "Restore the world and all matching companion files."; GUI.FocusControl(null); }
        void DrawConfirmation()
        {
            Ledger.Fill(new Rect(0, 0, 1920, 1080), new Color(0, 0, 0, .82f)); Ledger.Panel(new Rect(500, 340, 920, 360), Ledger.Surface, _confirmKind == "delete" ? Ledger.Danger : Ledger.Accent);
            string title = _confirmKind == "delete" ? "DELETE THIS SAVE?" : _confirmKind == "restore" ? "RESTORE THIS BACKUP?" : _confirmKind == "load" ? "LOAD THIS SAVE?" : "OVERWRITE THIS SAVE?";
            Text(new Rect(540, 375, 840, 60), title, false, true); Text(new Rect(540, 445, 840, 44), _confirmLabel);
            Text(new Rect(540, 502, 840, 55), _confirmKind == "delete" ? "The save will leave this list. A recoverable backup will be kept." : _confirmKind == "load" ? "Unsaved progress will be lost. Saved files and backups are kept." : "The previous version will be backed up before anything changes.", true);
            if (Button(new Rect(540, 600, 390, 58), "CANCEL")) { _confirmFile = _confirmKind = null; return; }
            if (!Button(new Rect(950, 600, 430, 58), _confirmKind == "delete" ? "DELETE SAVE" : "CONFIRM", true, true, _confirmKind == "delete")) return;
            string kind = _confirmKind, file = _confirmFile; _confirmFile = _confirmKind = null;
            if (kind == "overwrite") { Save(file); return; }
            if (kind == "load") { ToolWindow.CloseThen(() => SinglePlayerSaving.Load(file)); return; }
            try { if (kind == "restore") SaveSystem.RestoreBackup(file); else SaveSystem.Delete(file); Refresh(); _message = kind == "restore" ? "Backup restored." : "Save deleted. Its backup is available in BACKUPS."; }
            catch (Exception error) { _message = error.Message; }
        }
        void Save(string overwrite) { string name = _name, world = _world; ToolWindow.CloseThen(() => SaveSystem.Request(name, overwrite, worldName: world)); }
        internal static void OpenBackups()
        { try { Directory.CreateDirectory(SaveFiles.BackupRoot); Process.Start(new ProcessStartInfo { FileName = SaveFiles.BackupRoot, UseShellExecute = true }); } catch (Exception error) { SaveSystem.Status = "Cannot open backups folder: " + error.Message; } }
        internal static void DrawSystem(ToolLayout l)
        {
            l.Heading("System"); l.Toggle(Plugin.EnableDebug, "Enable Debug");
            if (SaveSystem.Autosave == null) { l.Text("The save manager is unavailable."); return; }
            l.Heading("Autosave");
            l.Toggle(SaveSystem.Autosave, "Autosave"); l.Entry(SaveSystem.Interval); l.Entry(SaveSystem.Warning); l.Toggle(SaveSystem.WhileDriving, "Autosave while driving");
            l.Toggle(SaveSystem.ExitSave, "Exit save");
            l.Entry(SaveSystem.AutoKeep); l.Entry(SaveSystem.BackupKeep); l.Toggle(SaveSystem.Screenshots, "Save screenshots");
            if (l.Button("MANAGE SAVES")) ToolWindow.OpenSaves(SaveSystem.LocalWorld);
            if (l.Button("OPEN BACKUPS FOLDER")) OpenBackups();
            l.Heading("Cleanup on successful save");
            l.Text("Off by default. Requires continuous distance from the player. Attached cargo, player custody and uncertain references are protected. Unknown save records are preserved.");
            l.Toggle(SaveCleanup.Enabled, "Cleanup on save"); l.Toggle(SaveCleanup.Corpses, "Non-resource zombie remains"); l.Toggle(SaveCleanup.Packs, "Exhausted disposable packs");
            l.Entry(SaveCleanup.Distance); l.Entry(SaveCleanup.Minutes);
            if (SaveSystem.RecoveryError != null && SaveRuntime.InMainMenu && l.Button("RETRY SAVE RECOVERY")) SaveSystem.RecoverFiles();
            if (SaveSystem.LocalWorld && (SaveSystem.ReloadRequired || SaveSystem.RecoveryError != null) && l.Button("RETURN TO TITLE WITHOUT SAVING")) ToolWindow.CloseThen(SaveSystem.LeaveWithoutSaving);
            SaveBus.DrawCleanupSettings(l);
        }
    }
}
