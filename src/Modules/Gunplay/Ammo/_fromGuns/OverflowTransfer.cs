using System;

namespace WildWasteland.Modules.Guns
{
    /// Tracks a single caliber across capacity changes. A failed transfer must suppress vanilla clamping.
    internal sealed class OverflowTransfer
    {
        private int _capacity;
        private bool _pending;
        private float _retryAt;

        internal OverflowTransfer(int capacity) { _capacity = Math.Max(0, capacity); }

        /// (WW) Exactly the first steps of BeforeClamp when nothing is pending and the capacity did not shrink (vanilla clamp allowed,
        /// count unchanged). Lets the caller skip building the drop closure on the every-frame path.
        internal bool Idle(int capacity)
        {
            capacity = Math.Max(0, capacity);
            if (_pending || capacity < _capacity) return false;
            _capacity = capacity;
            return true;
        }

        internal bool BeforeClamp(int count, int capacity, float now, Func<int, bool> drop, out int retained)
        {
            retained = count;
            capacity = Math.Max(0, capacity);
            if (capacity < _capacity) { _pending = true; _retryAt = 0f; }
            _capacity = capacity;
            if (!_pending) return true;
            if (count <= capacity) { _pending = false; return true; }
            if (now < _retryAt) return false;
            int excess = count - capacity;
            if (drop(excess))
            {
                retained = count - excess;
                _pending = false;
                return true;
            }
            _retryAt = now + 1f;
            return false;
        }
    }
}
