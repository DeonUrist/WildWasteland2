using System;
using System.Collections.Generic;
using System.IO;
using ES3Internal;
using WildWasteland.Core;

namespace WildWasteland.Modules.Saves
{
    /// <summary>Retains recovery evidence and missing records while a manual save rescues current gameplay.</summary>
    internal static class SaveRecovery
    {
        internal const string ReportKey = "WildWasteland.Saves.Issues.v1";
        static readonly List<string> Issues = new List<string>();
        static readonly Dictionary<string, ES3Data> Baseline = new Dictionary<string, ES3Data>();
        static readonly HashSet<string> FailedKeys = new HashSet<string>(StringComparer.Ordinal);
        static readonly Func<object, Dictionary<string, ES3Data>> Data = Access.Get<Dictionary<string, ES3Data>>(typeof(ES3File), "cache");
        static int _generation = -1, _loadedGeneration = -1, _attempt;
        static string _lastIssue;
        static string _report;
        /// <summary>Reports current-world warnings without carrying them into a different world.</summary>
        internal static bool HasIssues { get { return SaveSystem.ReloadRequired || SaveSystem.RecoveryError != null || !SaveSystem.Ready || _generation == SaveBus.Generation && _lastIssue != null; } }
        internal static bool AttemptHasIssues { get { return Issues.Count != 0; } }
        /// <summary>Starts a diagnostic record before queuing, including failures that predate the first manual save.</summary>
        internal static void Start()
        {
            string previous = _generation == SaveBus.Generation ? _lastIssue : null;
            _generation = SaveBus.Generation; _attempt++; Issues.Clear(); Baseline.Clear(); FailedKeys.Clear();
            if (SaveSystem.RecoveryError != null) Issue(SaveSystem.RecoveryError);
            if (SaveSystem.ReloadRequired) Issue(SaveSystem.ReloadReason ?? "A previous save requires recovery");
            if (SaveSystem.ReloadRequired && previous != null) Issue(previous);
            if (!SaveSystem.Ready) Issue("Interrupted-save recovery is incomplete; existing journals and backups will be retained");
        }
        /// <summary>Always logs every distinct cause per attempt, independently of the optional logging level.</summary>
        internal static void Issue(string reason)
        {
            if (_generation != SaveBus.Generation) { Issues.Clear(); Baseline.Clear(); FailedKeys.Clear(); }
            _generation = SaveBus.Generation; _lastIssue = reason;
            if (Issues.Contains(reason)) return;
            Issues.Add(reason); Log.Always(SavesModule.Instance, "Save issue #" + _attempt + ": " + reason + "\n" + Context());
        }
        /// <summary>Records the original exception and call stack, including failures before a transaction starts.</summary>
        internal static void Failed(Exception error)
        { Issue(error.ToString()); SaveSystem.Notify("Save operation failed; previous snapshots and backups are retained. See LogOutput.log."); }
        /// <summary>Retains the exact previous key when its cache write failed before publication.</summary>
        internal static void RetainFailedKey(string message)
        {
            if (!message.StartsWith("save key ", StringComparison.Ordinal) || message.StartsWith("save key callback:", StringComparison.Ordinal)) return;
            int end = message.IndexOf(':', 9); if (end > 9) FailedKeys.Add(message.Substring(9, end - 9));
        }
        /// <summary>Restores a saved warning after loading without carrying issues across world generations.</summary>
        internal static void Loaded(string slot)
        {
            if (_loadedGeneration == SaveBus.Generation) return;
            _loadedGeneration = SaveBus.Generation;
            if (_generation != SaveBus.Generation) { Issues.Clear(); Baseline.Clear(); FailedKeys.Clear(); _lastIssue = null; }
            _generation = SaveBus.Generation;
            try
            {
                if (File.Exists(SaveFiles.WorldPath(slot)) && ES3.KeyExists(ReportKey, SaveFiles.Disk(slot)))
                { string report = ES3.Load<string>(ReportKey, SaveFiles.Disk(slot)); if (_lastIssue == null) _lastIssue = report; Log.Always(SavesModule.Instance, "Loaded save with issues: " + slot + "\n" + report); }
            }
            catch (Exception error) { Issue("Could not read the saved issue report: " + error); }
        }
        static string Context()
        {
            return "WW " + Plugin.VERSION + "; generation=" + SaveBus.Generation + "; slot=" + SaveSystem.ActiveFile
                + "; native=" + SaveBus.State + "; installed=" + SaveSystem.Installed + "; ready=" + SaveSystem.Ready
                + "; requested=" + SaveSystem.Requested + "; transaction=" + SaveSystem.Busy + "; serializing=" + SinglePlayerSaving.Serializing
                + "; loading=" + SinglePlayerSaving.Loading + "; nativeSaving=" + SaveBus.Saving + "; nativeLoading=" + SaveBus.Loading
                + "; afterSave=" + SaveBus.PendingAfterSave + "; exit=" + SaveSystem.Exiting + "; phase=" + SinglePlayerSaving.ProgressText;
        }
        /// <summary>Keeps both disk and live-cache records; live data takes precedence over the last stored version.</summary>
        internal static string[] Capture()
        {
            string slot = SaveSystem.ActiveFile; Baseline.Clear();
            if (File.Exists(SaveFiles.WorldPath(slot)))
            {
                try { foreach (var pair in Data(new ES3File(SaveFiles.Disk(slot), true))) Baseline[pair.Key] = pair.Value; }
                catch (Exception error) { if (!SaveSystem.RecoverySaving) throw; Issue("Stored world records could not all be read; original bytes will be backed up before writing: " + error); }
            }
            ES3File cache;
            if (ES3File.cachedFiles.TryGetValue(slot, out cache)) foreach (var pair in Data(cache)) Baseline[pair.Key] = pair.Value;
            if (SaveSystem.RecoverySaving || cache == null)
            {
                cache = new ES3File(new ES3Settings(slot) { location = ES3.Location.Cache }, false);
                foreach (var pair in Baseline) Data(cache)[pair.Key] = pair.Value;
                ES3File.cachedFiles[slot] = cache;
            }
            var keys = new string[Baseline.Count]; Baseline.Keys.CopyTo(keys, 0); return keys;
        }
        /// <summary>Repairs absent records from retained bytes without replacing freshly serialized records.</summary>
        internal static void PreserveMissing()
        {
            ES3File cache;
            if (!ES3File.cachedFiles.TryGetValue(SaveSystem.ActiveFile, out cache)) throw new IOException("The live save cache is unavailable");
            var records = Data(cache);
            foreach (string key in FailedKeys)
            {
                ES3Data previous;
                if (Baseline.TryGetValue(key, out previous)) { records[key] = previous; Issue("Restored the prior record after a failed module write: " + key); }
                else { records.Remove(key); Issue("No prior record available for failed module write: " + key); }
            }
            foreach (var pair in Baseline) if (!records.ContainsKey(pair.Key) && !SaveCleanup.RemovesKey(pair.Key))
            { records[pair.Key] = pair.Value; Issue("Restored missing save record from the pre-save cache: " + pair.Key); }
        }
        /// <summary>Embeds the same recovery report in the independent snapshot for later support.</summary>
        internal static void WriteReport()
        {
            var settings = new ES3Settings(SaveSystem.ActiveFile) { location = ES3.Location.Cache };
            _report = AttemptHasIssues ? string.Join("\n\n", Issues.ToArray()) : null;
            if (_report != null) ES3.Save<string>(ReportKey, _report, settings);
            else ES3.DeleteKey(ReportKey, settings);
        }
        /// <summary>Verifies that a rescued snapshot retains its full diagnostic report.</summary>
        internal static void VerifyReport(string slot)
        {
            if (_report != null && (!ES3.KeyExists(ReportKey, SaveFiles.Disk(slot)) || ES3.Load<string>(ReportKey, SaveFiles.Disk(slot)) != _report))
                throw new IOException("The save issue report did not persist");
        }
        /// <summary>Releases temporary records only after publication, retaining a visible warning for the next attempt.</summary>
        internal static void Accepted(string target)
        {
            _lastIssue = AttemptHasIssues ? string.Join("\n", Issues.ToArray()) : null; Baseline.Clear();
            if (AttemptHasIssues) Log.Always(SavesModule.Instance, "Save recovery completed: " + target + "; existing saves/backups retained; issues=" + Issues.Count + "\n" + _lastIssue);
        }
    }
}
