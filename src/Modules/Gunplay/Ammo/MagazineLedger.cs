using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WildWasteland.Modules.Guns
{
    /// One magazine per save-stable item name. A vanilla type pool can be inherited only once.
    internal sealed class MagazineLedger
    {
        /// <summary>One item's weapon type and magazine.</summary>
        private sealed class Entry { internal string Type; internal Magazine Magazine; }
        private readonly Dictionary<string, Entry> _items = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly HashSet<string> _claimed = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Items with a magazine.</summary>
        internal int Count { get { return _items.Count; } }
        /// <summary>Weapon types whose vanilla pool was inherited.</summary>
        internal int ClaimedCount { get { return _claimed.Count; } }

        /// <summary>Magazine of an item; a new item inherits the vanilla type pool once, later items of that type start empty.</summary>
        internal Magazine Draw(string item, string type, Magazine vanilla)
        {
            Entry entry;
            if (_items.TryGetValue(item, out entry))
            {
                if (entry.Type != type) throw new InvalidOperationException("Weapon identity changed type: " + item);
                return entry.Magazine;
            }
            var magazine = _claimed.Add(type) ? vanilla : new Magazine(0, false);
            _items.Add(item, new Entry { Type = type, Magazine = magazine });
            return magazine;
        }

        /// <summary>Stores the current magazine of an item already in the ledger.</summary>
        internal void Capture(string item, string type, Magazine magazine)
        {
            Entry entry;
            if (!_items.TryGetValue(item, out entry) || entry.Type != type)
                throw new InvalidOperationException("Cannot capture an unowned magazine: " + item);
            entry.Magazine = magazine;
        }

        /// <summary>"Apocaunloader.Magazines|1" text (frozen format): claimed types and items, sorted ordinally.</summary>
        internal string Encode()
        {
            var lines = new List<string> { "Apocaunloader.Magazines|1" };
            foreach (var type in _claimed.OrderBy(t => t, StringComparer.Ordinal)) lines.Add("C|" + Uri.EscapeDataString(type));
            foreach (var item in _items.OrderBy(p => p.Key, StringComparer.Ordinal))
                lines.Add(string.Join("|", new[] { "M", Uri.EscapeDataString(item.Key), Uri.EscapeDataString(item.Value.Type),
                    item.Value.Magazine.Rounds.ToString(CultureInfo.InvariantCulture), item.Value.Magazine.Hammer ? "1" : "0" }));
            return string.Join("\n", lines.ToArray());
        }

        /// <summary>Parses the |1 text strictly; any malformed line throws (PerGunAmmo then suspends to protect the save).</summary>
        internal static MagazineLedger Decode(string text)
        {
            var lines = (text ?? "").Split('\n');
            if (lines.Length == 0 || lines[0] != "Apocaunloader.Magazines|1") throw new FormatException("Unsupported magazine save format");
            var ledger = new MagazineLedger();
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split('|');
                if (parts.Length == 2 && parts[0] == "C")
                {
                    string type = Uri.UnescapeDataString(parts[1]);
                    if (type.Length == 0 || !ledger._claimed.Add(type)) throw new FormatException("Invalid claimed weapon type");
                }
                else if (parts.Length == 5 && parts[0] == "M")
                {
                    string item = Uri.UnescapeDataString(parts[1]), type = Uri.UnescapeDataString(parts[2]);
                    int rounds;
                    if (item.Length == 0 || type.Length == 0 || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out rounds)
                        || (parts[4] != "0" && parts[4] != "1") || (rounds == 0 && parts[4] == "1") || ledger._items.ContainsKey(item))
                        throw new FormatException("Invalid magazine entry");
                    ledger._items.Add(item, new Entry { Type = type, Magazine = new Magazine(rounds, parts[4] == "1") });
                }
                else throw new FormatException("Invalid magazine save line");
            }
            foreach (var entry in ledger._items.Values)
                if (!ledger._claimed.Contains(entry.Type)) throw new FormatException("Magazine has no claimed weapon type");
            return ledger;
        }
    }
}
