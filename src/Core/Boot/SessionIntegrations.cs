using System;
using System.Collections.Generic;

namespace WildWasteland.Core
{
    /// <summary>Installs process-lifetime integrations that cannot safely be reversed by an ordinary module switch.</summary>
    internal static class SessionIntegrations
    {
        /// <summary>Retains one bootstrap contribution and its outcome for the lifetime of the process.</summary>
        internal sealed class Entry
        {
            internal readonly string Id;
            internal readonly Action Install;
            internal bool Installed;
            internal Exception Error;

            /// <summary>Records the contribution without executing it during module registration.</summary>
            internal Entry(string id, Action install) { Id = id; Install = install; }
        }

        private static readonly List<Entry> _entries = new List<Entry>();
        private static bool _started;

        /// <summary>Registers a mandatory session contribution at the composition root, before bootstrap installation begins.</summary>
        internal static void Register(string id, Action install)
        {
            if (_started) throw new InvalidOperationException("Session integration registration is closed");
            if (string.IsNullOrEmpty(id) || install == null) throw new ArgumentException("Session integration requires an id and installer");
            foreach (var entry in _entries) if (entry.Id == id) throw new InvalidOperationException("Duplicate session integration: " + id);
            _entries.Add(new Entry(id, install));
        }

        /// <summary>Runs each contribution once; failures remain latched and abort bootstrap rather than retrying a destructive takeover.</summary>
        internal static void InstallAll()
        {
            _started = true;
            foreach (var entry in _entries)
            {
                if (entry.Error != null) throw new InvalidOperationException("Session integration remains failed: " + entry.Id, entry.Error);
                if (entry.Installed) continue;
                try { entry.Install(); entry.Installed = true; }
                catch (Exception error)
                {
                    entry.Error = error;
                    Log.Error("[Session] " + entry.Id + " failed; restart is required before retrying", error);
                    throw new InvalidOperationException("Mandatory session integration failed: " + entry.Id, error);
                }
            }
        }
    }
}
