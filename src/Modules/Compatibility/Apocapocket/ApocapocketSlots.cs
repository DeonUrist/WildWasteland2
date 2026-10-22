using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.CleanHUD;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// <see cref="IInventorySlots"/> provider over Apocapocket's extra slots (README 6.12, A2-7). Reads the mod through its own
    /// <see cref="ApocapocketInventory"/> at most every 0.25 s (unscaled) and caches the slot item names, so queries never allocate.
    /// </summary>
    internal sealed class ApocapocketSlots : IInventorySlots
    {
        private readonly ApocapocketInventory _reader = new ApocapocketInventory();
        private readonly string[] _ids = new string[6];
        private readonly GameObject[] _named = new GameObject[6];
        private float _next = -1f;

        /// <summary>Re-reads the mod when the cached snapshot is older than 0.25 s.</summary>
        private void Refresh()
        {
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + .25f;
            _reader.Read();
            for (int i = 0; i < 6; i++)
            {
                var item = _reader.Active ? _reader.Items[i] : null;
                if (!ReferenceEquals(item, _named[i])) { _named[i] = item; _ids[i] = item != null ? item.name : null; }
                else if (item == null) _ids[i] = null;
            }
        }

        /// <summary>Number of usable extra slots (0 while the mod is off or absent).</summary>
        public int Count { get { Refresh(); return _reader.Active ? _reader.Count : 0; } }
        /// <summary>Selected slot (-1 = none).</summary>
        public int Selected { get { Refresh(); return _reader.Active ? _reader.Selected : -1; } }
        /// <summary>Save-stable item id (the item object's name) in a slot; null when empty.</summary>
        public string ItemIn(int slot) { Refresh(); return slot >= 0 && slot < 6 ? _ids[slot] : null; }
        /// <summary>True when the slot holds an ordinary (non-weapon) item.</summary>
        public bool IsOrdinary(int slot) { Refresh(); return _reader.Active && slot >= 0 && slot < 6 && _reader.Ordinary[slot]; }
        /// <summary>The mod's icon of an item currently in a slot (borrowed), or null.</summary>
        public Texture2D IconOf(string itemId)
        {
            Refresh();
            if (itemId == null) return null;
            for (int i = 0; i < 6; i++)
                if (_ids[i] == itemId && _named[i] != null) return ApocapocketInventory.ItemIcon(_named[i]) as Texture2D;
            return null;
        }
    }
}
