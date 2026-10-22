using System.Collections.Generic;

namespace WildWasteland.Core
{
    /// <summary>Retains string metadata shared through Core; the save engine explicitly clears and persists it with the active world.</summary>
    public static class SessionMetadata
    {
        private static readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        private static int _generation = -1;

        /// <summary>Stores an exact persistence key in the current session, normalizing a null value to an empty string.</summary>
        public static void Set(string key, string value)
        { EnsureGeneration(); if (!string.IsNullOrEmpty(key)) _values[key] = value ?? ""; }

        /// <summary>Reads the session value first, then the current slot's ES3 cache and file without negatively caching missing values.</summary>
        public static string Get(string key)
        {
            EnsureGeneration();
            if (string.IsNullOrEmpty(key)) return null;
            string value;
            if (_values.TryGetValue(key, out value)) return value;
            string file = SaveBus.Slot; if (file == null) return null;
            foreach (var location in new[] { ES3.Location.Cache, ES3.Location.File })
            {
                var settings = new ES3Settings(file) { location = location };
                if (ES3.KeyExists(key, settings)) return ES3.Load<string>(key, settings);
            }
            return null;
        }

        /// <summary>Clears values at the save engine's load or new-world boundary.</summary>
        public static void Clear() { _values.Clear(); _generation = SaveBus.Generation; }

        /// <summary>Writes registered keys unchanged into the save engine's active file cache during its save transaction.</summary>
        public static void Save(string activeFile)
        { EnsureGeneration(); foreach (var pair in _values) ES3.Save<string>(pair.Key, pair.Value, new ES3Settings(activeFile) { location = ES3.Location.Cache }); }

        /// <summary>Discards stale world data even while the save module is disabled, retaining same-world writes across its restart.</summary>
        private static void EnsureGeneration()
        { if (_generation != SaveBus.Generation) { _values.Clear(); _generation = SaveBus.Generation; } }
    }
}
