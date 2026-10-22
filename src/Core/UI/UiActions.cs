using System;
using System.Collections.Generic;

namespace WildWasteland.Core.UI
{
    /// <summary>Routes named presentation actions to their active owner without exposing feature-specific types to Core.</summary>
    public static class UiActions
    {
        sealed class Entry
        {
            internal IModule Owner;
            internal Action Invoke;
            internal Func<string> Caption;
        }
        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>Registers an action; a different owner cannot replace an existing key.</summary>
        public static void Register(IModule owner, string key, Action invoke, Func<string> caption = null)
        {
            if (owner == null || string.IsNullOrEmpty(key) || invoke == null) throw new ArgumentException("An owned UI action needs a key and callback");
            Entry existing;
            if (Entries.TryGetValue(key, out existing) && !ReferenceEquals(existing.Owner, owner))
                throw new InvalidOperationException("UI action already belongs to " + existing.Owner.Id + ": " + key);
            Entries[key] = new Entry { Owner = owner, Invoke = invoke, Caption = caption };
        }

        /// <summary>Invokes a registered action; true means it was found, including when its owner reports an error.</summary>
        public static bool TryInvoke(string key)
        {
            Entry entry;
            if (key == null || !Entries.TryGetValue(key, out entry)) return false;
            var invoke = entry.Invoke; var owner = entry.Owner;
            try { invoke(); } catch (Exception error) { ModuleHost.ReportHookException(owner, "UI action " + key, error); }
            return true;
        }

        /// <summary>Reads an action's current caption, or null when it has no active caption provider.</summary>
        public static string Caption(string key)
        {
            Entry entry;
            if (key == null || !Entries.TryGetValue(key, out entry)) return null;
            var caption = entry.Caption; var owner = entry.Owner;
            if (caption == null) return "";
            try { return caption(); } catch (Exception error) { ModuleHost.ReportHookException(owner, "UI caption " + key, error); return ""; }
        }

        /// <summary>Removes only the actions registered by the stopping owner.</summary>
        public static void RemoveAll(IModule owner)
        {
            var keys = new List<string>();
            foreach (var pair in Entries) if (ReferenceEquals(pair.Value.Owner, owner)) keys.Add(pair.Key);
            foreach (string key in keys) Entries.Remove(key);
        }
    }
}
