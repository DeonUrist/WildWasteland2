using System;
using System.IO;
using System.Collections.Generic;

namespace WildWasteland.Core
{
    /// <summary>
    /// One module-owned ES3 key inside the save slot file (core-modapi.md 6; replaces six hand-written copies). Writing follows the
    /// ES3 cache rule: values enter the cache now and are reapplied together after the native cache flush. Each slot is flushed
    /// once and every record is verified before Saves publishes the transaction. A failed write or verification is reported
    /// through <see cref="SaveBus.ReportFailure(IModule,string)"/> so the save transaction fails instead of publishing a partial save.
    /// Obtain instances with <see cref="SaveBus.Key"/>. New keys use a "WildWasteland.&lt;Module&gt;." prefix; existing keys keep their names.
    /// </summary>
    public sealed class SaveKey
    {
        /// <summary>ES3 key name.</summary>
        public readonly string Name;

        /// <summary>Created by <see cref="SaveBus.Key"/>.</summary>
        internal SaveKey(string name) { Name = name; }

        /// <summary>Latest snapshot and completion callbacks for a key in the pending native save.</summary>
        sealed class Pending
        {
            internal IModule Owner;
            internal string Value;
            internal readonly List<Action<bool>> Callbacks = new List<Action<bool>>();
        }
        static readonly Dictionary<string, Dictionary<string, Pending>> Writes = new Dictionary<string, Dictionary<string, Pending>>(StringComparer.OrdinalIgnoreCase);
        static bool _queued;

        /// <summary>Discards snapshots when serialization is cancelled or the world changes.</summary>
        internal static void CancelPending() { Writes.Clear(); _queued = false; }

        /// <summary>Writes <paramref name="value"/> for the save that is running (or the current slot) with the cache rule and
        /// verification. <paramref name="verified"/> (optional) is called on the main thread with true/false after verification.</summary>
        public void Write(IModule owner, string value, Action<bool> verified = null) { Write(owner, SaveBus.SaveSlot ?? SaveBus.Slot, value, verified); }

        /// <summary>Writes <paramref name="value"/> into <paramref name="slot"/> (slot file name) with the cache rule and verification.</summary>
        public void Write(IModule owner, string slot, string value, Action<bool> verified = null)
        {
            if (string.IsNullOrEmpty(slot)) { SaveBus.ReportFailure(owner, "save key " + Name + ": no slot"); if (verified != null) verified(false); return; }
            if ((!SaveBus.Saving && SaveBus.State != "SaveGame") || !string.Equals(slot, SaveBus.SaveSlot ?? SaveBus.Slot, StringComparison.OrdinalIgnoreCase))
            {
                Log.Warn(owner, "save key " + Name + " rejected outside the active save");
                SaveBus.ReportFailure(owner, "save key " + Name + ": not the active save transaction");
                if (verified != null) verified(false);
                return;
            }
            ES3Settings cache;
            try { cache = new ES3Settings(slot) { location = ES3.Location.Cache }; ES3.Save<string>(Name, value, cache); }
            catch (Exception e) { Log.Error(owner, "save key " + Name + " write failed", e); SaveBus.ReportFailure(owner, "save key " + Name + ": " + e.Message); if (verified != null) verified(false); return; }
            Dictionary<string, Pending> keys;
            if (!Writes.TryGetValue(slot, out keys)) Writes.Add(slot, keys = new Dictionary<string, Pending>(StringComparer.Ordinal));
            Pending pending;
            if (!keys.TryGetValue(Name, out pending)) keys.Add(Name, pending = new Pending());
            pending.Owner = owner; pending.Value = value;
            if (verified != null) pending.Callbacks.Add(verified);
            if (!_queued) { _queued = true; SaveBus.AfterSave(Flush); }
        }

        /// <summary>Reapplies all snapshots, stores each slot once and verifies every key before publication.</summary>
        static void Flush()
        {
            var slots = new Dictionary<string, Dictionary<string, Pending>>(Writes, StringComparer.OrdinalIgnoreCase);
            CancelPending();
            foreach (var slot in slots)
            {
                bool ok = false;
                try
                {
                    var cache = new ES3Settings(slot.Key) { location = ES3.Location.Cache };
                    foreach (var key in slot.Value) ES3.Save<string>(key.Key, key.Value.Value, cache);
                    ES3.StoreCachedFile(slot.Key);
                    var disk = new ES3Settings(slot.Key) { location = ES3.Location.File };
                    foreach (var key in slot.Value)
                        if (!ES3.KeyExists(key.Key, disk) || ES3.Load<string>(key.Key, disk) != key.Value.Value) throw new IOException("key did not persist: " + key.Key);
                    ok = true;
                }
                catch (Exception e) { Log.Error(null, "Embedded save verification failed for " + slot.Key, e); SaveBus.ReportFailure(null, "Embedded save verification: " + e.Message); }
                foreach (var key in slot.Value) foreach (var callback in key.Value.Callbacks)
                    try { callback(ok); } catch (Exception e) { Log.Error(key.Value.Owner, "save key " + key.Key + " callback", e); SaveBus.ReportFailure(key.Value.Owner, "save key callback: " + e.Message); }
            }
        }

        /// <summary>Reads a disk record, returning null only when absent; corrupt records must not become an empty world.</summary>
        public string Read(string slot = null)
        {
            slot = slot ?? SaveBus.Slot;
            if (string.IsNullOrEmpty(slot)) return null;
            try
            {
                var disk = new ES3Settings(slot) { location = ES3.Location.File };
                return File.Exists(disk.FullPath) && ES3.KeyExists(Name, disk) ? ES3.Load<string>(Name, disk) : null;
            }
            catch (Exception error) { throw new IOException("Cannot read embedded save key " + Name + " in " + slot, error); }
        }

        /// <summary>True when the key exists on disk in <paramref name="slot"/> (or the current slot).</summary>
        public bool Exists(string slot = null) { return Read(slot) != null; }

        /// <summary>Deletes the key from the slot's cache and file (no-op when absent).</summary>
        public void Delete(string slot = null)
        {
            slot = slot ?? SaveBus.Slot;
            if (string.IsNullOrEmpty(slot)) return;
            try
            {
                var cache = new ES3Settings(slot) { location = ES3.Location.Cache };
                if (ES3.KeyExists(Name, cache)) ES3.DeleteKey(Name, cache);
                var disk = new ES3Settings(slot) { location = ES3.Location.File };
                if (File.Exists(disk.FullPath) && ES3.KeyExists(Name, disk)) ES3.DeleteKey(Name, disk);
            }
            catch (Exception e) { Log.Warn(null, "save key " + Name + " delete failed: " + e.Message); }
        }
    }
}
