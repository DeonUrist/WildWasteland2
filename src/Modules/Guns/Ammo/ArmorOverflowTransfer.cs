using System;

namespace WildWasteland.Modules.Guns
{
    /// Whole plates only: each returned plate represents 50 points lost to a capacity downgrade.
    internal sealed class ArmorOverflowTransfer
    {
        internal const int ArmorPerPlate = 50;
        private float _capacity, _retryAt;
        private bool _pending;
        internal ArmorOverflowTransfer(float capacity) { _capacity = Math.Max(0f, capacity); }

        /// (WW) Exactly the first steps of BeforeClamp when nothing is pending and the capacity did not shrink (see OverflowTransfer.Idle).
        internal bool Idle(float capacity)
        {
            capacity = Math.Max(0f, capacity);
            if (_pending || capacity < _capacity) return false;
            _capacity = capacity;
            return true;
        }

        internal bool BeforeClamp(float armor, float capacity, float now, Func<int, bool> drop, out float retained)
        {
            retained = armor;
            capacity = Math.Max(0f, capacity);
            if (capacity < _capacity) { _pending = true; _retryAt = 0f; }
            _capacity = capacity;
            if (!_pending) return true;
            double excess = Math.Max(0d, (double)armor - capacity);
            int plates = (int)Math.Min(int.MaxValue, Math.Floor(excess / ArmorPerPlate));
            if (plates == 0) { _pending = false; return true; }
            if (now < _retryAt) return false;
            if (drop(plates))
            {
                retained = armor - plates * (float)ArmorPerPlate;
                _pending = false;
                return true;
            }
            _retryAt = now + 1f;
            return false;
        }
    }
}
