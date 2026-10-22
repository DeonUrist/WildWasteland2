using System;
using System.IO;

namespace WildWasteland.Core
{
    /// <summary>
    /// One module-owned ES3 key inside the save slot file (core-modapi.md 6; replaces six hand-written copies). Writing follows the
    /// ES3 cache rule: the value goes into the slot's cache now and again ~1 s after the save returned to isPlay (after the game's own
    /// cache flush), then the cached file is stored and the key is verified on disk. A failed write or verification is reported
    /// through <see cref="SaveBus.ReportFailure(IModule,string)"/> so the save transaction fails instead of publishing a partial save.
    /// Obtain instances with <see cref="SaveBus.Key"/>. New keys use a "WildWasteland.&lt;Module&gt;." prefix; existing keys keep their names.
    /// </summary>
    public sealed class SaveKey
    {
        /// <summary>ES3 key name.</summary>
        public readonly string Name;

        /// <summary>Created by <see cref="SaveBus.Key"/>.</summary>
        internal SaveKey(string name) { Name = name; }

        /// <summary>Writes <paramref name="value"/> for the save that is running (or the current slot) with the cache rule and
        /// verification. <paramref name="verified"/> (optional) is called on the main thread with true/false after verification.</summary>
        public void Write(IModule owner, string value, Action<bool> verified = null) { Write(owner, SaveBus.SaveSlot ?? SaveBus.Slot, value, verified); }

        /// <summary>Writes <paramref name="value"/> into <paramref name="slot"/> (slot file name) with the cache rule and verification.</summary>
        public void Write(IModule owner, string slot, string value, Action<bool> verified = null)
        {
            if (string.IsNullOrEmpty(slot)) { SaveBus.ReportFailure(owner, "save key " + Name + ": no slot"); if (verified != null) verified(false); return; }
            ES3Settings cache;
            try { cache = new ES3Settings(slot) { location = ES3.Location.Cache }; ES3.Save<string>(Name, value, cache); }
            catch (Exception e) { Log.Error(owner, "save key " + Name + " write failed", e); SaveBus.ReportFailure(owner, "save key " + Name + ": " + e.Message); if (verified != null) verified(false); return; }
            SaveBus.AfterSave(() =>
            {
                bool ok = false;
                try
                {
                    ES3.Save<string>(Name, value, cache); ES3.StoreCachedFile(slot);
                    var disk = new ES3Settings(slot) { location = ES3.Location.File };
                    if (!ES3.KeyExists(Name, disk) || ES3.Load<string>(Name, disk) != value) throw new IOException("key did not persist");
                    ok = true;
                    if (Log.L1) Log.Main(owner, "save key " + Name + " verified in " + slot);
                }
                catch (Exception e) { Log.Error(owner, "save key " + Name + " verification failed", e); SaveBus.ReportFailure(owner, "save key " + Name + ": " + e.Message); }
                if (verified != null) try { verified(ok); } catch (Exception e) { Log.Error(owner, "save key " + Name + " callback", e); }
            });
        }

        /// <summary>Value stored on disk in <paramref name="slot"/> (or the current slot), or null when absent/unreadable.</summary>
        public string Read(string slot = null)
        {
            slot = slot ?? SaveBus.Slot;
            if (string.IsNullOrEmpty(slot)) return null;
            try
            {
                var disk = new ES3Settings(slot) { location = ES3.Location.File };
                return File.Exists(disk.FullPath) && ES3.KeyExists(Name, disk) ? ES3.Load<string>(Name, disk) : null;
            }
            catch (Exception) { return null; }
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
