using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Relation between two factions, or of a faction toward the player.</summary>
    public enum Stance { Unknown = 0, Ally = 1, Neutral = 2, Hostile = 3 }

    /// <summary>A module that knows faction membership (e.g. Quests factions.txt + tags, Patrols crews). Queried in priority order.</summary>
    public interface IFactionSource
    {
        /// <summary>Faction name of go, or null when this source does not know it.</summary>
        string FactionOf(GameObject go);
    }

    /// <summary>
    /// Shared faction relations service (README 4.2). Holds no game data of its own: Quests (factions.txt, story defaults such
    /// as Scrapyard hostile to the player and at war with the Coyotes) and Patrols (crew factions) feed it in Wave B through
    /// <see cref="Set"/>, <see cref="SetPlayer"/> and <see cref="AddSource"/>. Entries are owned (removed with the owner) and
    /// symmetric; names compare case-insensitively. Lookups do not allocate. <see cref="Revision"/> changes on every write so
    /// consumers can cache.
    /// </summary>
    public static class Factions
    {
        /// <summary>One relation entry and the module that set it.</summary>
        private struct Entry { public Stance Stance; public IModule Owner; }

        const string PlayerKey = "\u0001player";
        static readonly Dictionary<string, Dictionary<string, Entry>> _rel = new Dictionary<string, Dictionary<string, Entry>>(StringComparer.OrdinalIgnoreCase);
        static readonly List<KeyValuePair<IModule, IFactionSource>> _sources = new List<KeyValuePair<IModule, IFactionSource>>();
        static readonly List<string> _tmpA = new List<string>(), _tmpB = new List<string>();

        /// <summary>Incremented on every change.</summary>
        public static int Revision;

        /// <summary>Sets the symmetric relation between factions a and b (owned by owner). Unknown removes it.</summary>
        public static void Set(IModule owner, string a, string b, Stance stance)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return;
            Put(a, b, stance, owner); Put(b, a, stance, owner);
            Revision++;
        }

        /// <summary>Sets faction's stance toward the player (owned by owner). Unknown removes it.</summary>
        public static void SetPlayer(IModule owner, string faction, Stance stance)
        {
            if (string.IsNullOrEmpty(faction)) return;
            Put(faction, PlayerKey, stance, owner);
            Revision++;
        }

        /// <summary>Stores or removes one directed entry.</summary>
        static void Put(string a, string b, Stance stance, IModule owner)
        {
            Dictionary<string, Entry> row;
            if (!_rel.TryGetValue(a, out row)) { if (stance == Stance.Unknown) return; _rel[a] = row = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase); }
            if (stance == Stance.Unknown) row.Remove(b);
            else row[b] = new Entry { Stance = stance, Owner = owner };
        }

        /// <summary>Relation of a toward b: Ally for the same faction, else the stored stance, else Unknown.</summary>
        public static Stance Relation(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return Stance.Unknown;
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return Stance.Ally;
            Dictionary<string, Entry> row; Entry e;
            return _rel.TryGetValue(a, out row) && row.TryGetValue(b, out e) ? e.Stance : Stance.Unknown;
        }

        /// <summary>Stance of faction toward the player, or Unknown.</summary>
        public static Stance ToPlayer(string faction)
        {
            if (string.IsNullOrEmpty(faction)) return Stance.Unknown;
            Dictionary<string, Entry> row; Entry e;
            return _rel.TryGetValue(faction, out row) && row.TryGetValue(PlayerKey, out e) ? e.Stance : Stance.Unknown;
        }

        /// <summary>True when a and b are hostile to each other.</summary>
        public static bool Hostile(string a, string b) { return Relation(a, b) == Stance.Hostile; }

        /// <summary>Registers a membership source (later registrations are asked after earlier ones).</summary>
        public static void AddSource(IModule owner, IFactionSource source)
        {
            if (source == null) return;
            _sources.Add(new KeyValuePair<IModule, IFactionSource>(owner, source));
            Revision++;
        }

        /// <summary>Faction of go from the first source that knows it; null when none does. Source exceptions are swallowed.</summary>
        public static string Of(GameObject go)
        {
            if (go == null) return null;
            for (int i = 0; i < _sources.Count; i++)
            {
                string f = null;
                try { f = _sources[i].Value.FactionOf(go); } catch (Exception) { }
                if (!string.IsNullOrEmpty(f)) return f;
            }
            return null;
        }

        /// <summary>Removes every relation and source owned by owner (module shutdown).</summary>
        public static void RemoveAll(IModule owner)
        {
            for (int i = _sources.Count - 1; i >= 0; i--) if (_sources[i].Key == owner) _sources.RemoveAt(i);
            _tmpA.Clear(); _tmpB.Clear();
            foreach (var row in _rel)
                foreach (var e in row.Value)
                    if (e.Value.Owner == owner) { _tmpA.Add(row.Key); _tmpB.Add(e.Key); }
            for (int i = 0; i < _tmpA.Count; i++) _rel[_tmpA[i]].Remove(_tmpB[i]);
            _tmpA.Clear(); _tmpB.Clear();
            Revision++;
        }
    }
}
