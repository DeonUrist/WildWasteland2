using System;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// Tracks one capacity-clamped amount across capacity changes (ammo per caliber, body armor). When the capacity shrinks the excess is
    /// handed to a drop callback in whole units (1 round, or one armor plate per <see cref="ArmorPerPlate"/> points); a failed drop
    /// suppresses the vanilla clamp and retries after 1 s, so nothing is ever deleted. One class for ammo and armor (the former
    /// ArmorOverflowTransfer was a line-for-line float copy, README 6.3).
    /// </summary>
    internal sealed class OverflowTransfer
    {
        /// <summary>Armor points represented by one dropped plate.</summary>
        internal const int ArmorPerPlate = 50;

        private readonly float _unit;
        private float _capacity, _retryAt;
        private bool _pending;

        /// <summary>Ammo transfer (unit = 1 round) starting from the observed capacity.</summary>
        internal OverflowTransfer(int capacity) { _unit = 1f; _capacity = Math.Max(0, capacity); }

        /// <summary>Armor transfer (unit = <see cref="ArmorPerPlate"/> points) starting from the observed capacity.</summary>
        internal OverflowTransfer(float capacity) { _unit = ArmorPerPlate; _capacity = Math.Max(0f, capacity); }

        /// <summary>
        /// The first steps of BeforeClamp when nothing is pending and the capacity did not shrink (vanilla clamp allowed, count unchanged):
        /// lets the caller skip building the drop closure on the every-frame path.
        /// </summary>
        internal bool Idle(float capacity)
        {
            capacity = Math.Max(0f, capacity);
            if (_pending || capacity < _capacity) return false;
            _capacity = capacity;
            return true;
        }

        /// <summary>Ammo: true = let the vanilla clamp run; <paramref name="retained"/> is the count to keep after a successful drop.</summary>
        internal bool BeforeClamp(int count, int capacity, float now, Func<int, bool> drop, out int retained)
        {
            retained = count;
            int units;
            if (!Begin(Math.Max(0, capacity), out units, count - (double)Math.Max(0, capacity), now)) return !_pending || units < 0;
            if (drop(units)) { retained = count - units; _pending = false; return true; }
            _retryAt = now + 1f;
            return false;
        }

        /// <summary>Armor: true = let the vanilla clamp run; <paramref name="retained"/> is the armor kept after the plates were dropped.</summary>
        internal bool BeforeClamp(float armor, float capacity, float now, Func<int, bool> drop, out float retained)
        {
            retained = armor;
            capacity = Math.Max(0f, capacity);
            int units;
            if (!Begin(capacity, out units, (double)armor - capacity, now)) return !_pending || units < 0;
            if (drop(units)) { retained = armor - units * (float)ArmorPerPlate; _pending = false; return true; }
            _retryAt = now + 1f;
            return false;
        }

        /// <summary>
        /// Shared state machine: notes a shrink, then decides. False = do not drop now (<paramref name="units"/> = -1 means "allow the
        /// clamp", i.e. nothing pending or nothing whole to drop; 0 means "wait for the retry time"). True = drop <paramref name="units"/>.
        /// </summary>
        private bool Begin(float capacity, out int units, double excess, float now)
        {
            units = -1;
            if (capacity < _capacity) { _pending = true; _retryAt = 0f; }
            _capacity = capacity;
            if (!_pending) return false;
            double whole = Math.Floor(Math.Max(0d, excess) / _unit);
            if (whole <= 0d) { _pending = false; return false; }
            if (now < _retryAt) { units = 0; return false; }
            units = (int)Math.Min(int.MaxValue, whole);
            return true;
        }
    }
}
