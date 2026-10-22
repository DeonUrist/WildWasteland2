using System.Collections.Generic;

namespace WildWasteland.Core
{
    /// <summary>Numeric priority queue shared by native navigation adapters; duplicate cells are allowed.</summary>
    public static class MinHeap
    {
        /// <summary>One queued cell with its priority.</summary>
        private struct Open { internal int Cell; internal float Cost; }
        /// <summary>Binary min-heap of (cell, cost); duplicates allowed (callers skip closed cells).</summary>
        public sealed class Heap
        {
            private readonly List<Open> _items = new List<Open>();
            /// <summary>Queued entries.</summary>
            public int Count { get { return _items.Count; } }
            /// <summary>Adds a cell with its cost (sift up).</summary>
            public void Push(int cell, float cost)
            {
                int i = _items.Count; _items.Add(new Open());
                while (i > 0) { int p = (i - 1) / 2; if (_items[p].Cost <= cost) break; _items[i] = _items[p]; i = p; }
                _items[i] = new Open { Cell = cell, Cost = cost };
            }
            /// <summary>Removes and returns the cell with the lowest cost (sift down).</summary>
            public int Pop()
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
