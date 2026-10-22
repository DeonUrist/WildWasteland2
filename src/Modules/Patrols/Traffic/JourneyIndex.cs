using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Keeps ordered persistence iteration and constant-time driving lookups synchronized for every journey mutation.</summary>
    internal sealed class JourneyIndex : Collection<TrafficJourney>
    {
        private readonly Dictionary<string, TrafficJourney> _byId = new Dictionary<string, TrafficJourney>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _nextNumber;
        /// <summary>Returns the non-persisted integer identity of a currently registered journey.</summary>
        internal int Number(string id) { int number; return id != null && _numbers.TryGetValue(id, out number) ? number : -1; }
        /// <summary>Resolves the current journey without scanning all virtual and materialized groups.</summary>
        internal TrafficJourney Find(string id) { TrafficJourney journey; return id != null && _byId.TryGetValue(id, out journey) ? journey : null; }
        /// <summary>Rejects duplicate identities before modifying either index.</summary>
        protected override void InsertItem(int index, TrafficJourney item)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) throw new ArgumentException("Journey identity is required");
            if (_byId.ContainsKey(item.id)) throw new ArgumentException("Duplicate journey identity: " + item.id);
            base.InsertItem(index, item); _byId.Add(item.id, item); _numbers.Add(item.id, _nextNumber++);
        }
        /// <summary>Withdraws the identity whenever a journey ends or is discarded.</summary>
        protected override void RemoveItem(int index) { var item = this[index]; base.RemoveItem(index); _byId.Remove(item.id); _numbers.Remove(item.id); }
        /// <summary>Updates both representations if a caller replaces an ordered journey.</summary>
        protected override void SetItem(int index, TrafficJourney item)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) throw new ArgumentException("Journey identity is required");
            var previous = this[index];
            if (previous.id != item.id && _byId.ContainsKey(item.id)) throw new ArgumentException("Duplicate journey identity: " + item.id);
            base.SetItem(index, item); _byId.Remove(previous.id); _byId[item.id] = item;
            if (previous.id != item.id) { _numbers.Remove(previous.id); _numbers[item.id] = _nextNumber++; }
        }
        /// <summary>Clears identities and ordered records together at scene/reset boundaries.</summary>
        protected override void ClearItems() { base.ClearItems(); _byId.Clear(); _numbers.Clear(); _nextNumber = 0; }
    }
}
