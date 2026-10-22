using System.Collections.Generic;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Wave A (A6): the live part of the old Towns TownPaths class - its binary min-heap, used by TownNavigation's per-source
    /// Dijkstra. The dead grid A* path builder (TownPaths.Grid/Build/EntranceRoute) was deleted. B01 replaces this with a Core
    /// binary heap (namespace kept until then).
    /// </summary>
    internal static class TownPaths
    {
        private struct Open { internal int Cell; internal float Cost; }
        internal sealed class Heap
        {
            private readonly List<Open> _items = new List<Open>();
            internal int Count { get { return _items.Count; } }
            internal void Push(int cell, float cost)
            {
                int i = _items.Count; _items.Add(new Open());
                while (i > 0) { int p = (i - 1) / 2; if (_items[p].Cost <= cost) break; _items[i] = _items[p]; i = p; }
                _items[i] = new Open { Cell = cell, Cost = cost };
            }
            internal int Pop()
            {
                int result = _items[0].Cell, last = _items.Count - 1; var tail = _items[last]; _items.RemoveAt(last);
                if (last == 0) return result;
                int i = 0;
                while (i * 2 + 1 < last)
                {
                    int c = i * 2 + 1; if (c + 1 < last && _items[c + 1].Cost < _items[c].Cost) c++;
                    if (_items[c].Cost >= tail.Cost) break; _items[i] = _items[c]; i = c;
                }
                _items[i] = tail; return result;
            }
        }
    }
}
