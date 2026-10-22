using System;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    /// <summary>Working heap facade for Towns until its navigation package adopts the shared queue.</summary>
    internal static class TownPaths
    {
        /// <summary>Preserves the old cell-priority interface and tie order.</summary>
        [Obsolete("refactor: use Core.MinHeap.Heap")]
        internal sealed class Heap
        {
            readonly MinHeap.Heap _queue = new MinHeap.Heap();
            /// <summary>Number of queued cells.</summary>
            internal int Count { get { return _queue.Count; } }
            /// <summary>Queues a cell with its traversal cost.</summary>
            internal void Push(int cell, float cost) { _queue.Push(cell, cost); }
            /// <summary>Removes the cell with the smallest traversal cost.</summary>
            internal int Pop() { return _queue.Pop(); }
        }
    }
}
