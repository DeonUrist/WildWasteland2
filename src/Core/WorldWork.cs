using UnityEngine;

namespace WildWasteland.Core
{
    // Main-thread world preparation shares one owner per frame, including native spawn callbacks.
    // Jobs retain their own time budgets; round-robin demand prevents registration-order starvation.
    internal static class WorldWork
    {
        internal const int Vehicles = 0, Towns = 1, Population = 2;
        private static readonly int[] Requested = { -100, -100, -100 };
        private static int _frame = -1, _owner = -1, _last = -1;
        private static float _next;
        internal static bool Try(int owner)
        {
            int frame = Time.frameCount; Requested[owner] = frame;
            if (_frame == frame) return _owner == owner;
            if (SaveBus.Saving || SaveSystem.Busy) return false;
            bool loading = SaveBus.Busy || GameRefs.SaveLoadState == "Wait for Terrain Gen";
            if (!loading && (Time.timeScale <= 0 || Scheduler.FrameMs >= Scheduler.FrameBudgetMs || Time.unscaledTime < _next)) return false;
            for (int offset = 1; offset <= Requested.Length; offset++)
            {
                int candidate = (_last + offset) % Requested.Length;
                if (frame - Requested[candidate] > 4) continue;
                if (candidate != owner) return false;
                _frame = frame; _owner = _last = owner;
                // Give a badly delayed gameplay frame recovery time before another heavy slice.
                _next = Time.unscaledTime + (!loading && Time.unscaledDeltaTime > .05f ? .05f : 0);
                return true;
            }
            return false;
        }
    }
}
