using System.Collections.Generic;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>
    /// Reusable PlayMakerFSM lists for the per-hit component lookups (README 6.3 perf: GetComponents / GetComponentsInChildren allocated
    /// an array per bullet hit). Rent/Return is re-entrant (an FSM event sent while a list is in use can start another lookup).
    /// </summary>
    internal static class FsmBuffers
    {
        static readonly Stack<List<PlayMakerFSM>> _free = new Stack<List<PlayMakerFSM>>();

        /// <summary>An empty list (return it with <see cref="Return"/>).</summary>
        internal static List<PlayMakerFSM> Rent() { return _free.Count > 0 ? _free.Pop() : new List<PlayMakerFSM>(16); }

        /// <summary>Clears and pools a rented list.</summary>
        internal static void Return(List<PlayMakerFSM> list)
        {
            if (list == null) return;
            list.Clear();
            if (_free.Count < 16) _free.Push(list);
        }
    }
}
