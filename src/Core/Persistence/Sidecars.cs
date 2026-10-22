using System;
using System.Collections.Generic;
using System.IO;

namespace WildWasteland.Core
{
    /// <summary>
    /// Registry of per-slot companion files (core-modapi.md 6). A module declares each sidecar pattern once (in BindConfig, so it is
    /// known even while the module is off and its files still travel with the save); the Saves module includes every registered file
    /// in transactions, backups and copies (replaces the hard-coded list in SaveFiles.Participants and the Towns special case).
    /// Patterns are relative to config/WildWasteland and contain "{slot}" for the slot file name, e.g. "CleanHUD/Saves/{slot}.fog.bin".
    /// </summary>
    public static class Sidecars
    {
        /// <summary>One registered pattern.</summary>
        public sealed class Entry
        {
            /// <summary>Registering module (null = Core).</summary>
            public IModule Owner;
            /// <summary>Pattern relative to config/WildWasteland with "{slot}".</summary>
            public string Pattern;
        }

        /// <summary>Slot token inside patterns.</summary>
        public const string SlotToken = "{slot}";

        static readonly List<Entry> _entries = new List<Entry>();

        /// <summary>Every registered pattern in registration order.</summary>
        public static IList<Entry> All { get { return _entries; } }

        /// <summary>Raised when a pattern is registered (Saves refreshes its participant list).</summary>
        public static event Action Changed;

        /// <summary>Declares a per-slot companion file pattern of <paramref name="owner"/>. Registering the same owner + pattern twice is a no-op.</summary>
        public static void Register(IModule owner, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return;
            if (pattern.IndexOf(SlotToken, StringComparison.Ordinal) < 0) { Log.Warn(owner, "sidecar pattern without " + SlotToken + " ignored: " + pattern); return; }
            for (int i = 0; i < _entries.Count; i++) if (_entries[i].Owner == owner && _entries[i].Pattern == pattern) return;
            _entries.Add(new Entry { Owner = owner, Pattern = pattern });
            var h = Changed; if (h != null) try { h(); } catch (Exception e) { Log.Error(null, "Sidecars.Changed handler", e); }
        }

        /// <summary>Absolute path of <paramref name="pattern"/> for <paramref name="slot"/> (slot file name, e.g. "WW_save-....es3").</summary>
        public static string PathFor(IModule owner, string pattern, string slot)
        {
            string rel = pattern.Replace(SlotToken, slot ?? "").Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Assets.ConfigDir, rel);
        }

        /// <summary>Appends the absolute paths of every registered sidecar of <paramref name="slot"/> to <paramref name="into"/>.</summary>
        public static void PathsFor(string slot, List<string> into)
        {
            for (int i = 0; i < _entries.Count; i++) into.Add(PathFor(_entries[i].Owner, _entries[i].Pattern, slot));
        }
    }
}
