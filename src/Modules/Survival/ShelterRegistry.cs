using ES3Internal;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Survival
{
    /// <summary>Owner-scoped shelter geometry contributed by vehicles or world structures.</summary>
    internal sealed class ShelterRegistry : IShelterRegistry
    {
        /// <summary>Tracks one provider and its lifecycle owner without copying geometry.</summary>
        sealed class Entry { internal int Id; internal IModule Owner; internal IShelterProvider Provider; }
        readonly List<Entry> _entries = new List<Entry>();
        int _next;
        /// <summary>Cheap native-hook gate when no shelter geometry is registered.</summary>
        internal bool AnyProviders { get { return _entries.Count != 0; } }
        /// <summary>Registers a provider once per owner and instance.</summary>
        public int Register(IModule owner, IShelterProvider provider)
        {
            if (provider == null) return 0;
            foreach (var entry in _entries) if (ReferenceEquals(entry.Owner, owner) && ReferenceEquals(entry.Provider, provider)) return entry.Id;
            var added = new Entry { Id = ++_next, Owner = owner, Provider = provider }; _entries.Add(added); return added.Id;
        }
        /// <summary>Removes the exact registration returned at admission.</summary>
        public void Remove(int id) { _entries.RemoveAll(e => e.Id == id); }
        /// <summary>Checks the actor's current world position against active shelter providers.</summary>
        public bool IsSheltered(GameObject actor) { return actor != null && Check(actor, Origin.World(actor.transform.position)); }
        /// <summary>Checks a world point without assuming a player actor.</summary>
        public bool IsSheltered(Vector3 world) { return Check(null, world); }
        /// <summary>Skips stopped owners and isolates a failing optional provider.</summary>
        bool Check(GameObject actor, Vector3 world)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Owner != null && !ModuleHost.IsRunning(entry.Owner)) continue;
                try { if (entry.Provider.Shelters(actor, world)) return true; }
                catch (System.Exception error) { ModuleHost.ReportHookException(entry.Owner, "shelter query", error); }
            }
            return false;
        }
        /// <summary>Releases provider references on Survival shutdown; live consumers re-register on API replacement.</summary>
        internal void Clear() { _entries.Clear(); }
    }
}
