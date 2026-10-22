using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Core
{
    // The active native file is a working location. Published manual saves and autosaves have separate identities.
    internal static class SaveSystem
    {
        internal const string CommitKey = "WildWasteland.SaveCommit";
        internal sealed class SaveInfo
        {
            internal string File, Name, Kind, Date;
            internal string World;
            internal float? Kilometres;
            internal bool Automatic { get { return Kind == "auto"; } }
            internal string Distance { get { return Kilometres.HasValue ? Kilometres.Value.ToString("0.0", CultureInfo.InvariantCulture) + " km" : "Distance not recorded"; } }
        }
        internal static ConfigEntry<bool> Autosave, ExitSave, WhileDriving, Screenshots;
        internal static ConfigEntry<int> Interval, Warning, AutoKeep, BackupKeep;
        internal static string Status = "", RecoveryError;
        internal static bool ReloadRequired;
        internal static bool Busy { get { return _transaction != null; } }
        internal static bool AutomaticSaving { get { return _automatic; } }
        internal static bool Requested { get { return _requested; } }
        internal static bool Exiting { get { return _quitting; } }
        internal static bool Ready;
        internal static bool Installed;
        internal static bool LocalWorld { get { return !SaveRuntime.InMainMenu && GameRefs.Player != null && GameRefs.SaveLoadGame != null; } }
        internal static bool LocalReady { get { return LocalWorld && SaveBus.State == "isPlay"; } }
        internal static string ActiveFile { get { return SaveRuntime.ActiveFile ?? SaveBus.Slot; } }
        internal static string CurrentWorldName { get { if (_localGeneration == SaveBus.Generation && _localWorldName != null) return _localWorldName; string file = ActiveFile; if (file != null && File.Exists(SaveFiles.MetaPath(file))) return Info(file).World; var seed = GameRefs.SaveLoadGame != null ? GameRefs.SaveLoadGame.FsmVariables.FindFsmInt("seed") : null; return seed != null ? "World " + seed.Value : "My world"; } }
        internal static int CatalogueVersion;
        static bool _requested, _automatic, _quitting, _scheduled;
        static string _destination, _name, _error, _stamp;
        static string _world;
        static string _localWorldName;
        static int _localGeneration = -1;
        static byte[] _picture;
        static float _kilometres = -1;
        static SaveFiles _transaction;
        static string[] _oldKeys;
        static float _nextAuto, _countdown = -1, _lastWarning = -1, _saveNow = -1;
        static string _session;
        static Func<float> _health;
        static readonly HashSet<string> ProtectedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static string _pendingNotice;
        internal static void Install()
        {
            var cfg = Plugin.Settings;
            Autosave = TunedSettings.Bind(cfg, "System / Saving", "Autosave", true, "Create separate autosaves for singleplayer worlds. Manual saves are never replaced by autosaving.");
            ExitSave = TunedSettings.Bind(cfg, "System / Saving", "Exit save", false, "Create a separate autosave when quitting to the main menu. Independent of timed autosaves; disabled by default.");
            Interval = TunedSettings.Bind(cfg, "System / Saving", "Autosave minutes", 10, new ConfigDescription("Real minutes between autosaves.", new AcceptableValueRange<int>(1, 120)));
            Warning = TunedSettings.Bind(cfg, "System / Saving", "Warning seconds", 30, new ConfigDescription("Visible autosave countdown. At least 30 seconds, with notices at 30 and 15 seconds and a final warning before saving.", new AcceptableValueRange<int>(30, 120)));
            WhileDriving = TunedSettings.Bind(cfg, "System / Saving", "Autosave while driving", false, "Allow autosaves while the player is driving or riding in any vehicle. Manual and exit saves are always available in vehicles.");
            AutoKeep = TunedSettings.Bind(cfg, "System / Saving", "Autosaves to keep", 5, new ConfigDescription("Number of separate autosaves per world lineage. Manual saves are not pruned.", new AcceptableValueRange<int>(1, 100)));
            BackupKeep = TunedSettings.Bind(cfg, "System / Saving", "Backups to keep", 5, new ConfigDescription("Verified previous versions per save. Interrupted recovery and deleted-save backups are always retained.", new AcceptableValueRange<int>(1, 100)));
            Screenshots = TunedSettings.Bind(cfg, "System / Saving", "Save screenshots", true, "Capture a gameplay thumbnail for each save.");
            Autosave.SettingChanged += ScheduleChanged; Interval.SettingChanged += ScheduleChanged; Warning.SettingChanged += ScheduleChanged; WhileDriving.SettingChanged += ScheduleChanged;
            _health = SaveRuntime.Health;
            Hooks.ActivateGameObjectDo.Prefix(null, VehicleSaveButton);
            Require(typeof(ES3PlayMaker.ActionBase), "HandleError", "NativeError");
            if (!Patches.Patch(null, AccessTools.Method(typeof(ES3File), "Sync", new[] { typeof(ES3Settings) }), prefix: AccessTools.Method(typeof(SaveSystem), "BeforeSync"))) throw new InvalidOperationException("Native end-of-frame world-write guard failed");
            if (!Patches.Patch(null, AccessTools.Method(typeof(ES3Internal.ES3IO), "CommitBackup"), finalizer: AccessTools.Method(typeof(SaveSystem), "StorageError"))) throw new InvalidOperationException("Native disk failure observation hook failed");
            if (!Patches.Patch(null, AccessTools.Method(typeof(ES3PlayMaker.PMDataWrapper), "ApplyVariables"), postfix: AccessTools.Method(typeof(SaveSystem), "LoadedPath"))) throw new InvalidOperationException("Save identity hook failed");
            SaveCleanup.Install();
            SaveCombat.Install(); SaveWarning.Install();
            SinglePlayerSaving.Install();
            WorldHistory.Install(); SaveCompatibility.Install();
            VehicleLoadPlacement.Install();
            SaveBus.StateChanged += StateChanged;
            Scheduler.Interval(null, "Singleplayer autosave warning", .25f, Tick);
            Log.Always("[System] Autosave warnings: game-font compass countdown, 30/15-second notices, combat deferral and 3-second final warning");
            Installed = true;
            Log.Always("[System] Singleplayer saves ready: separate manual/autosaves, complete backups and interrupted-write recovery");
        }
        internal static void RecoverFiles()
        {
            if (Autosave == null) return;
            try { SaveFiles.Recover(); Ready = true; RecoveryError = null; }
            catch (Exception error) { Ready = false; RecoveryError = "Save recovery needs attention: " + error.Message; Log.Error("[System] " + RecoveryError, error); }
        }
        static Exception StorageError(Exception __exception) { if (__exception != null) Fail("World persistence failed: " + __exception.Message); return __exception; }
        static bool BeforeSync(ES3Settings settings)
        {
            if (!Installed) return true;
            string file = settings != null ? Path.GetFileName(settings.path) : SaveBus.Slot;
            if (file == null || !SaveRuntime.IsSaveFile(file)) return true;
            if (RecoveryError != null) return false;
            if (LocalWorld && file == ActiveFile) ProtectedSlots.Add(file);
            return !ProtectedSlots.Contains(file) || Busy && file == ActiveFile;
        }
        static void StateChanged(string previous, string current)
        { if (current == "LoadGame" || current == "setSeed") { ReloadRequired = false; _session = null; _pendingNotice = null; } }
        static void Require(Type type, string method, string prefix)
        { if (!Patches.Patch(null, AccessTools.Method(type, method), prefix: AccessTools.Method(typeof(SaveSystem), prefix))) throw new InvalidOperationException("System save hook failed: " + method); }
        static void LoadedPath(Fsm fsm)
        {
            VehicleLoadPlacement.Loaded();
            // A snapshot carries the original global SaveFile. Keep the file explicitly chosen by the launcher.
            if (fsm == null || fsm.GameObject == null || fsm.GameObject.name != "SaveLoadGame" || fsm.Name != "SaveLoadGame") return;
            string file = SaveRuntime.SelectedFile ?? ActiveFile;
            if (!SaveRuntime.IsSaveFile(file)) return;
            var variable = fsm.Variables.FindFsmString("SaveFile"); if (variable != null) variable.Value = file;
            ES3Settings.defaultSettings.path = file;
        }
        static bool NativeError(string msg) { if (WorldHistory.NativeError(msg)) return false; Fail("Native save error: " + msg); return true; }
        internal static void Fail(string message) { if (Busy && _error == null) _error = message; }
        internal static void Notify(string text) { Status = text; Log.Always("[System] " + text); _pendingNotice = text; }
        static void DeliverNotice()
        {
            if (_pendingNotice == null || !LocalReady || LoadingProgress.MenusBlocked) return;
            var text = _pendingNotice; _pendingNotice = null;
            Modules.CleanHUD.MessageHook.Push(text);
        }
        
        internal static SaveInfo Info(string file, string fallback = null)
        {
            var info = new SaveInfo { File = file, Name = fallback ?? Path.GetFileNameWithoutExtension(file), World = "World not recorded", Kind = "legacy", Date = "" };
            string path = SaveFiles.MetaPath(file);
            try { if (File.Exists(path))
            {
                var lines = File.ReadAllLines(path);
                if (lines.Length < 5 || lines[0] != "WW-SAVE-1") throw new IOException("Invalid save description: " + file);
                info.Name = Encoding.UTF8.GetString(Convert.FromBase64String(lines[1])); info.Kind = lines[2]; info.Date = lines[3];
                float km;
                if (lines.Length >= 6 && float.TryParse(lines[5], NumberStyles.Float, CultureInfo.InvariantCulture, out km) && km >= 0 && !float.IsInfinity(km)) info.Kilometres = km;
                if (lines.Length >= 9)
                {
                    info.World = Encoding.UTF8.GetString(Convert.FromBase64String(lines[6])); if (lines[7] != "sp") throw new IOException("Not a singleplayer save description");
                }
            }
            else if (File.Exists(SaveFiles.WorldPath(file))) info.Date = File.GetLastWriteTime(SaveFiles.WorldPath(file)).ToString("yyyy-MM-dd HH:mm"); }
            catch (Exception error) { Log.Warn("[System] Save description unreadable; world retained: " + file + ": " + error.Message); }
            return info;
        }
        internal static string Lineage(string file)
        {
            string path = SaveFiles.MetaPath(file);
            if (File.Exists(path)) { var lines = File.ReadAllLines(path); if (lines.Length >= 5 && SaveRuntime.IsSaveFile(lines[4])) return lines[4]; }
            return file;
        }
        static byte[] Description(string name, string kind, string lineage)
        { return Encoding.UTF8.GetBytes(string.Join("\n", new[] { "WW-SAVE-1", Convert.ToBase64String(Encoding.UTF8.GetBytes(name)), kind, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), lineage, _kilometres >= 0 ? _kilometres.ToString("R", CultureInfo.InvariantCulture) : "", Convert.ToBase64String(Encoding.UTF8.GetBytes(_world ?? "My world")), "sp", "" })); }
        static float DistanceTravelled()
        {
            var fsm = GameRefs.GameManager != null ? FsmUtil.Find(GameRefs.GameManager, "DistanceTravelled") : null;
            var distance = fsm != null && fsm.Fsm.Initialized ? fsm.FsmVariables.FindFsmFloat("distance") : null;
            float km = distance != null ? distance.Value : -1;
            return km >= 0 && !float.IsInfinity(km) ? km : -1;
        }
        internal static bool Request(string name, string overwrite = null, bool automatic = false, bool scheduled = false, string worldName = null)
        {
            if (!LocalReady || Busy || _requested || _quitting) return false;
            if (!Ready || RecoveryError != null || ReloadRequired) { Notify(RecoveryError ?? "Reload a save before saving again after a failed write."); return false; }
            if (_health() < 1) { Notify("Saving deferred while the player is dead."); return false; }
            if (string.IsNullOrEmpty(name) || name.Trim().Length == 0) { Notify("Enter a save name."); return false; }
            foreach (char ch in name) if (char.IsControl(ch) || ch == '<' || ch == '>') { Notify("Use a save name without control characters or markup."); return false; }
            if (name.Length > 64 || (overwrite != null && !SaveRuntime.IsSaveFile(overwrite))) { Notify("Invalid save name or destination."); return false; }
            if (worldName != null) { if (worldName.Trim().Length == 0 || worldName.Length > 64) return false; foreach (char ch in worldName) if (char.IsControl(ch) || ch == '<' || ch == '>') return false; }
            if (overwrite != null && Info(overwrite).Automatic) { Notify("Create a manual save instead of overwriting an autosave."); return false; }
            _world = worldName ?? CurrentWorldName;
            _localWorldName = _world; _localGeneration = SaveBus.Generation;
            _automatic = automatic; _scheduled = scheduled; _name = name.Trim(); _destination = overwrite; _requested = true;
            if (!(SinglePlayerSaving.Request())) { _requested = false; Notify("The current world's save target is unavailable."); return false; }
            return true;
        }
        // Recheck immediately before freezing: combat can begin during the warning.
        internal static bool DeferScheduledSave()
        {
            if (!_scheduled || !SaveCombat.Check()) return false;
            _requested = _scheduled = false; _countdown = _lastWarning = _saveNow = -1;
            SaveWarning.Show(2); Notify("Autosave deferred until combat has ended.");
            return true;
        }
        // Called after local time/input freeze, before the native serializer starts.
        internal static void Begin()
        {
            if (RecoveryError != null || ReloadRequired) throw new IOException(RecoveryError ?? "Reload required after a failed save");
            if (_health() < 1) throw new IOException("Saving deferred: the player died before serialization");
            string file = ActiveFile; SaveFiles.WorldPath(file);
            if (!_requested) { _automatic = true; _name = "Autosave " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); _destination = null; _world = CurrentWorldName; }
            _error = null; _picture = null; _stamp = Guid.NewGuid().ToString("N");
            _oldKeys = File.Exists(SaveFiles.WorldPath(file)) ? ES3.GetKeys(SaveFiles.Disk(file)) : new string[0];
            var tx = new SaveFiles(file, "working save"); tx.Prepare(SaveFiles.Participants(file)); _transaction = tx;
            _kilometres = DistanceTravelled();
            if (Screenshots.Value) CapturePicture();
            SaveCleanup.Prepare();
        }
        internal static void Stamp()
        {
            WorldHistory.Merge(); var settings = new ES3Settings(ActiveFile) { location = ES3.Location.Cache };
            ES3.Save<string>(CommitKey, _stamp, settings);

            SaveCleanup.SaveProtection(); SaveCompatibility.SavePoses();
        }
        static void CapturePicture()
        {
            var camera = GameRefs.MainCamera; if (camera == null || Application.isBatchMode) return;
            RenderTexture old = RenderTexture.active, target = camera.targetTexture; RenderTexture image = null; Texture2D pixels = null;
            try
            {
                image = RenderTexture.GetTemporary(480, 270, 24); camera.targetTexture = image; camera.Render(); RenderTexture.active = image;
                pixels = new Texture2D(480, 270, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); pixels.Apply(); _picture = pixels.EncodeToPNG();
            }
            catch (Exception error) { Log.Warn("[System] Save thumbnail unavailable: " + error.Message); }
            finally { camera.targetTexture = target; RenderTexture.active = old; if (image != null) RenderTexture.ReleaseTemporary(image); if (pixels != null) UnityEngine.Object.Destroy(pixels); }
        }
        static void Validate(string file, string stamp)
        {
            var settings = SaveFiles.Disk(file); var keys = new HashSet<string>(ES3.GetKeys(settings), StringComparer.Ordinal);
            foreach (string key in new[] { "global", "Player", "newGOArray_Cars", "newGOArray_Items", "newGOArray_Sandstorms", CommitKey })
                if (!keys.Contains(key)) throw new IOException("World save is missing " + key);
            if (stamp != null && ES3.Load<string>(CommitKey, settings) != stamp) throw new IOException("World save did not persist this transaction");

            if (file == ActiveFile) foreach (string key in _oldKeys) if (!keys.Contains(key) && !SaveCleanup.RemovesKey(key)) throw new IOException("Previous save record was lost: " + key);
        }
        internal static bool Complete()
        {
            if (!Busy) return false;
            string source = ActiveFile, target = _destination ?? "WW_" + (_automatic ? "auto-" : "save-") + Guid.NewGuid().ToString("N") + ".es3";
            try
            {
                if (_error != null) throw new IOException(_error);
                ES3.StoreCachedFile(source); Validate(source, _stamp);
                if (target == source)
                {
                    SaveFiles.WriteDurable(SaveFiles.MetaPath(source), Description(_name, "manual", Lineage(source)));
                    if (_picture != null) SaveFiles.WriteDurable(SaveFiles.ImagePath(source), _picture);
                    else if (File.Exists(SaveFiles.ImagePath(source))) File.Delete(SaveFiles.ImagePath(source));
                    _transaction.Commit();
                }
                else
                {
                    PublishSnapshot(source, target);
                    // Autosaving / Save As must not overwrite the loaded save. Keep the fresh in-memory cache.
                    _transaction.Rollback(false); _transaction.Commit();
                }
                _transaction = null; _requested = false; Schedule();
                CatalogueVersion++;
                WorldHistory.Accepted();
                try { SaveCleanup.Commit(); } catch (Exception cleanup) { SaveSystem.ReloadRequired = true; Log.Error("[System] Save committed, but cleanup could not finish; reload before saving", cleanup); }
                Status = (_automatic ? "Autosave" : "Save") + " completed: " + _name;
                Log.Always("[System] " + Status);
                try { SaveFiles.Prune(source, BackupKeep.Value); SaveFiles.Prune(target, BackupKeep.Value); if (_automatic) PruneAutosaves(Lineage(target), target); }
                catch (Exception error) { Log.Warn("[System] Retention deferred; saves retained: " + error.Message); }
                return true;
            }
            catch (Exception error) { Abort(error); return false; }
        }
        static void PublishSnapshot(string source, string target)
        {
            var copies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in SaveFiles.Participants(source)) if (File.Exists(path))
            {
                // The verified embedded record is authoritative; do not create Towns sidecars for new snapshots.
                if (path.StartsWith(Path.Combine(Assets.ConfigDir, "Towns", "Saves") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && ES3.KeyExists("WildWasteland.Towns.World.v1", SaveFiles.Disk(source))) continue;
                // Native .bak and checkpoint files are recovery history, not the new snapshot's authority.
                if (path.EndsWith(".wwsave", StringComparison.Ordinal) || path.EndsWith(".wwsave.png", StringComparison.Ordinal) || Path.GetFileName(path).IndexOf(".bak", StringComparison.OrdinalIgnoreCase) >= 0 || path.EndsWith(".player-checkpoint.txt", StringComparison.Ordinal)) continue;
                copies[SaveFiles.Remap(path, source, target)] = path;
            }
            var participants = SaveFiles.Participants(target); foreach (string path in copies.Keys) participants.Add(path);
            var tx = new SaveFiles(target, _automatic ? "autosave" : "manual save"); tx.Prepare(participants);
            try
            {
                // Copy the whole snapshot into private staging before publishing any destination file.
                int i = 0; var stages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in copies)
                {
                    string path = Path.Combine(tx.DirectoryPath, "new-" + i++ + ".bin"); SaveFiles.CopyVerified(pair.Value, path);
                    if (pair.Value.EndsWith(".senses.txt", StringComparison.Ordinal))
                    {
                        string text = File.ReadAllText(path), header = "v1 " + source;
                        if (!text.StartsWith(header + "\n", StringComparison.Ordinal) && !text.StartsWith(header + "\r\n", StringComparison.Ordinal)) throw new IOException("NPC senses companion identity is invalid");
                        SaveFiles.WriteDurable(path, Encoding.UTF8.GetBytes("v1 " + target + text.Substring(header.Length)));
                    }
                    stages[pair.Key] = path;
                }
                foreach (var pair in stages) SaveFiles.Publish(pair.Value, pair.Key);
                foreach (var entry in tx.Entries) if (!stages.ContainsKey(entry.Path) && entry.Path != SaveFiles.MetaPath(target) && entry.Path != SaveFiles.ImagePath(target) && File.Exists(entry.Path)) File.Delete(entry.Path);
                SaveFiles.WriteDurable(SaveFiles.MetaPath(target), Description(_name, _automatic ? "auto" : "manual", Lineage(source)));
                if (_picture != null) SaveFiles.WriteDurable(SaveFiles.ImagePath(target), _picture);
                else if (File.Exists(SaveFiles.ImagePath(target))) File.Delete(SaveFiles.ImagePath(target));
                Validate(target, _stamp); tx.Commit();
            }
            catch { try { tx.Rollback(); } catch (Exception error) { RecoveryError = "Snapshot recovery required: " + error.Message; } throw; }
        }
        internal static void Abort(Exception error)
        {
            var tx = _transaction;
            if (tx != null) try { SaveBus.CancelSave(); } catch (Exception cancel) { _error = "Native save cancellation failed: " + cancel.Message; }
            _transaction = null;
            SaveCleanup.Rollback();
            if (tx != null) try { tx.Rollback(); } catch (Exception recovery) { RecoveryError = "Save recovery required at " + tx.DirectoryPath + ": " + recovery.Message; }
            // A failed write leaves live module state ahead of disk. Require a reload before another world write.
            if (tx != null) ReloadRequired = true;
            _requested = false; _picture = null; Status = "Save failed: " + error.Message;
            Log.Error("[System] " + Status, error); _pendingNotice = Status;
        }
        internal static void Delete(string file, bool expiredAutosave = false)
        {
            if (Busy || _requested || (LocalWorld && file == ActiveFile)) throw new IOException("Cannot delete the running world's source save");
            if (RecoveryError != null) throw new IOException(RecoveryError);
            var tx = new SaveFiles(file, expiredAutosave ? "expired autosave" : "delete"); tx.Prepare(SaveFiles.Participants(file));
            try { foreach (var entry in tx.Entries) if (File.Exists(entry.Path)) File.Delete(entry.Path); tx.Commit(); ES3File.cachedFiles.Remove(file); CatalogueVersion++; }
            catch { tx.Rollback(); throw; }
        }
        internal static List<string> Backups(string file)
        {
            var result = new List<string>(); string directory = Path.Combine(SaveFiles.BackupRoot, file);
            if (Directory.Exists(directory)) foreach (string path in Directory.GetDirectories(directory))
                if (SaveFiles.Marked(Path.Combine(path, "committed")) && File.Exists(Path.Combine(path, "undo.bin")))
                { var backup = SaveFiles.Read(path); foreach (var entry in backup.Entries) if (entry.Path == SaveFiles.WorldPath(file) && entry.Existed) { result.Add(path); break; } }
            result.Sort(StringComparer.Ordinal); result.Reverse(); return result;
        }
        internal static List<string> DeletedBackups()
        {
            var result = new List<string>(); if (!Directory.Exists(SaveFiles.BackupRoot)) return result;
            foreach (string directory in Directory.GetDirectories(SaveFiles.BackupRoot))
            {
                string file = Path.GetFileName(directory);
                if (SaveRuntime.IsSaveFile(file) && !File.Exists(SaveFiles.WorldPath(file))) result.AddRange(Backups(file));
            }
            return result;
        }
        internal static void RestoreBackup(string directory)
        {
            if (!SaveRuntime.InMainMenu || Busy || _requested) throw new IOException("Restore backups from the title menu only");
            string full = Path.GetFullPath(directory), prefix = Path.GetFullPath(SaveFiles.BackupRoot) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new IOException("Invalid backup directory");
            var backup = SaveFiles.Read(full); bool hasWorld = false;
            var paths = SaveFiles.Participants(backup.Slot);
            foreach (var entry in backup.Entries) { paths.Add(entry.Path); if (entry.Path == SaveFiles.WorldPath(backup.Slot) && entry.Existed) hasWorld = true; }
            if (!hasWorld) throw new IOException("This backup predates the first save; there is no previous world to restore");
            var undo = new SaveFiles(backup.Slot, "restore backup"); undo.Prepare(paths);
            try
            {
                var previous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in backup.Entries) previous.Add(entry.Path);
                foreach (var entry in undo.Entries) if (!previous.Contains(entry.Path) && File.Exists(entry.Path)) File.Delete(entry.Path);
                backup.Rollback();
                // Legacy backups need not contain WW's new marker, but their ES3 records must remain readable.
                var keys = new HashSet<string>(ES3.GetKeys(SaveFiles.Disk(backup.Slot)));
                if (!keys.Contains("global") || !keys.Contains("Player") || !keys.Contains("newGOArray_Items")) throw new IOException("Backup world is incomplete");
                undo.Commit(); CatalogueVersion++; Status = "Backup restored: " + backup.Slot;
            }
            catch { undo.Rollback(); throw; }
        }
        internal static void LeaveWithoutSaving()
        {
            if (Busy || !LocalWorld || (!ReloadRequired && RecoveryError == null)) return;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
        static void PruneAutosaves(string lineage, string newest)
        {
            var files = new List<string>();
            foreach (string path in Directory.GetFiles(SaveFiles.WorldRoot, "WW_*.es3.wwsave"))
            {
                string file = Path.GetFileName(path).Substring(0, Path.GetFileName(path).Length - 7);
                if (SaveRuntime.IsSaveFile(file) && Info(file).Automatic && Lineage(file) == lineage && File.Exists(SaveFiles.WorldPath(file))) files.Add(file);
            }
            files.Sort(CompareNewest); int kept = 0;
            foreach (string file in files) { if (kept++ < AutoKeep.Value || file == newest || file == ActiveFile) continue; Delete(file, true); SaveFiles.Prune(file, 0); }
        }
        static int CompareNewest(string a, string b) { return File.GetLastWriteTimeUtc(SaveFiles.WorldPath(b)).CompareTo(File.GetLastWriteTimeUtc(SaveFiles.WorldPath(a))); }
        static void Schedule() { _nextAuto = Time.unscaledTime + Interval.Value * 60f; _countdown = _lastWarning = _saveNow = -1; _scheduled = false; SaveWarning.Show(0); }
        static void ScheduleChanged(object sender, EventArgs args) { Schedule(); }
        static void Tick()
        {
            DeliverNotice();
            if (!LocalWorld) { _session = null; SaveWarning.Show(0); return; }
            if (_session != ActiveFile) { _session = ActiveFile; Schedule(); }
            bool combat = SaveCombat.Check();
            if (!Autosave.Value || Busy || _requested || _quitting || RecoveryError != null || ReloadRequired)
            { _countdown = _lastWarning = _saveNow = -1; SaveWarning.Show(0); return; }
            float now = Time.unscaledTime;
            int warning = Mathf.Min(Mathf.Max(30, Warning.Value), Interval.Value * 60);
            if (now < _nextAuto - warning) return;
            bool eligible = !combat && !SaveBus.Busy && GameRefs.InGame && !UI.EditorSession.Busy && _health() >= 1 && (WhileDriving.Value || !SaveRuntime.InCar);
            if (!eligible) { _countdown = _lastWarning = _saveNow = -1; SaveWarning.Show(2); return; }
            if (_countdown < 0) _countdown = now + warning;
            int seconds = Mathf.CeilToInt(_countdown - now);
            if (seconds <= 0)
            {
                SaveWarning.Show(3);
                if (_saveNow < 0) { _saveNow = now + 3f; Notify("The game will save now"); return; }
                if (now >= _saveNow && !Request("Autosave " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), null, true, true))
                { _countdown = _lastWarning = _saveNow = -1; }
                return;
            }
            SaveWarning.Show(1, seconds);
            // Threshold crossings survive a delayed frame without skipping either warning.
            if (_lastWarning < 0 || _lastWarning > 30 && seconds <= 30 || _lastWarning > 15 && seconds <= 15)
                Notify("Autosave in " + seconds + " seconds.");
            _lastWarning = seconds;
        }
        static bool VehicleSaveButton(HutongGames.PlayMaker.Actions.ActivateGameObject action)
        {
            if (!Ready || action.Fsm == null || action.Fsm.Name != "InCar" || action.Fsm.GameObject != GameRefs.Player) return true;
            var target = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
            if (target == null || target.transform.parent == null || target.transform.parent.name != "MainMenu") return true;
            if (target.name != "Save_Game_slots" && target.name != "get out to save") return true;
            target.SetActive(target.name == "Save_Game_slots"); return false;
        }
        
        internal static void SaveBeforeExit(Action then)
        { if (!_quitting) { _quitting = true; Scheduler.StartCoroutine(Quit(then)); } }
        static IEnumerator Quit(Action leave)
        {
            try
            {
                while (Busy || _requested) yield return null;
                if (!ExitSave.Value) { leave(); yield break; }
                if (RecoveryError != null || ReloadRequired) { Notify("Save and exit cancelled: resolve the save failure or return to the menu without saving."); yield break; }
                _quitting = false; bool requested = Request("Exit autosave " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), null, true); _quitting = true;
                if (!requested) { Notify("Exit cancelled because a new save could not start."); yield break; }
                while (_requested || Busy) yield return null;
                if (RecoveryError != null || ReloadRequired || !Status.StartsWith("Autosave completed:", StringComparison.Ordinal)) { Notify("Exit cancelled because the save failed."); yield break; }
                yield return new WaitForSecondsRealtime(.4f);
                leave();
            }
            finally { _quitting = false; }
        }
    }
}
