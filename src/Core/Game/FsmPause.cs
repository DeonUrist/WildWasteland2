using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HutongGames.PlayMaker;

namespace WildWasteland.Core
{
    /// <summary>
    /// The single owner of Fsm.ManualUpdate (PlayMakerFSM.Update skips Fsm.Update while it is set; events, trigger/collision
    /// callbacks and state changes still run). Port of Performance FsmEdits.FsmPause, extended with owner-allocated bits so FS1
    /// (FsmSleep), PollSleep, CarLod, LightFlicker/Stepped and HudFsms share one claim table instead of FS1 setting the flag
    /// directly (two ManualUpdate owners today). Several reasons (bits) may pause one FSM; it runs again only when all are gone.
    /// An FSM whose ManualUpdate was already set by someone else is never touched, and if someone clears it while we hold it,
    /// <see cref="Held"/> / <see cref="Set"/> notice and drop our claim. Main thread only.
    /// </summary>
    public static class FsmPause
    {
        /// <summary>Fixed bits of the Performance copy (kept so ported callers keep their constants).</summary>
        public const int CarLod = 1, PollSleep = 2, Stepped = 4, FsmSleep = 8;
        const int FirstDynamic = 16;

        /// <summary>Reference-identity comparer (Fsm is a plain class; avoids any custom Equals).</summary>
        private sealed class RefComparer : IEqualityComparer<Fsm>
        {
            public static readonly RefComparer Instance = new RefComparer();
            /// <summary>Reference equality.</summary>
            public bool Equals(Fsm a, Fsm b) { return ReferenceEquals(a, b); }
            /// <summary>Identity hash.</summary>
            public int GetHashCode(Fsm o) { return RuntimeHelpers.GetHashCode(o); }
        }

        static readonly Dictionary<Fsm, int> _bits = new Dictionary<Fsm, int>(RefComparer.Instance);
        static readonly List<Fsm> _tmp = new List<Fsm>();
        /// <summary>Identity-scoped dynamic pause reason; equal module ids do not share claims.</summary>
        readonly struct Reason : IEquatable<Reason>
        {
            readonly IModule _owner; readonly string _name;
            /// <summary>Stores the exact owner instance and ordinal reason text.</summary>
            internal Reason(IModule owner, string name) { _owner = owner; _name = name ?? ""; }
            /// <summary>Compares module instances and reason text without module-defined equality.</summary>
            public bool Equals(Reason other) { return ReferenceEquals(_owner, other._owner) && string.Equals(_name, other._name, StringComparison.Ordinal); }
            /// <summary>Compares another boxed reason key.</summary>
            public override bool Equals(object other) { return other is Reason && Equals((Reason)other); }
            /// <summary>Hashes the reference identity and ordinal reason string.</summary>
            public override int GetHashCode() { return (_owner == null ? 0 : RuntimeHelpers.GetHashCode(_owner)) * 397 ^ StringComparer.Ordinal.GetHashCode(_name); }
        }
        /// <summary>Identity comparer for module-owned masks.</summary>
        sealed class OwnerComparer : IEqualityComparer<IModule>
        {
            /// <summary>Uses module instance identity.</summary>
            public bool Equals(IModule first, IModule second) { return ReferenceEquals(first, second); }
            /// <summary>Hashes a module by identity.</summary>
            public int GetHashCode(IModule owner) { return RuntimeHelpers.GetHashCode(owner); }
        }
        static readonly Dictionary<Reason, int> _named = new Dictionary<Reason, int>();
        static readonly Dictionary<IModule, int> _ownerBits = new Dictionary<IModule, int>(new OwnerComparer());
        static int _nextBit = FirstDynamic;

        /// <summary>FSMs currently paused by any of our bits.</summary>
        public static int Count { get { return _bits.Count; } }

        /// <summary>
        /// A stable bit for (owner, reason): the same owner instance and reason text always get the same bit; the bit is remembered as owned by
        /// owner so <see cref="ReleaseAll"/> frees its pauses. Returns 0 when all 32 bits are used (then Set always fails).
        /// </summary>
        public static int Bit(IModule owner, string reason)
        {
            int b; var key = new Reason(owner, reason);
            if (!_named.TryGetValue(key, out b))
            {
                if (_nextBit == 0) return 0;
                b = _nextBit;
                _nextBit = _nextBit == int.MinValue ? 0 : _nextBit << 1;
                _named[key] = b;
            }
            if (owner != null) { int o; _ownerBits.TryGetValue(owner, out o); _ownerBits[owner] = o | b; }
            return b;
        }

        /// <summary>Records that owner uses one of the fixed bits (CarLod, PollSleep, Stepped, FsmSleep) so ReleaseAll covers it.</summary>
        public static void Own(IModule owner, int bits)
        {
            if (owner == null) return;
            int o; _ownerBits.TryGetValue(owner, out o); _ownerBits[owner] = o | bits;
        }

        /// <summary>Claims bit on f; false if the FSM is paused by somebody else.</summary>
        public static bool Set(Fsm f, int bit)
        {
            if (f == null || bit == 0) return false;
            int b;
            if (_bits.TryGetValue(f, out b))
            {
                if (!f.ManualUpdate) { _bits.Remove(f); b = 0; }    // released by someone else: start over
                else { _bits[f] = b | bit; return true; }
            }
            if (f.ManualUpdate) return false;
            f.ManualUpdate = true;
            _bits[f] = bit;
            return true;
        }

        /// <summary>Drops bit on f; the FSM runs again when no bit is left.</summary>
        public static void Clear(Fsm f, int bit)
        {
            int b;
            if (f == null || !_bits.TryGetValue(f, out b) || (b & bit) == 0) return;
            b &= ~bit;
            if (b == 0) { _bits.Remove(f); f.ManualUpdate = false; }
            else _bits[f] = b;
        }

        /// <summary>True while our bit still holds f paused.</summary>
        public static bool Held(Fsm f, int bit)
        {
            int b;
            if (f == null || !_bits.TryGetValue(f, out b) || (b & bit) == 0) return false;
            if (!f.ManualUpdate) { _bits.Remove(f); return false; }
            return true;
        }

        /// <summary>True when f is paused by any of our bits.</summary>
        public static bool Paused(Fsm f) { return f != null && _bits.ContainsKey(f) && f.ManualUpdate; }

        /// <summary>Drops bit on every FSM (feature switched off).</summary>
        public static void ClearAll(int bit)
        {
            _tmp.Clear();
            foreach (var kv in _bits) if ((kv.Value & bit) != 0) _tmp.Add(kv.Key);
            for (int i = 0; i < _tmp.Count; i++) Clear(_tmp[i], bit);
            _tmp.Clear();
        }

        /// <summary>Drops every bit owner registered through <see cref="Bit"/> / <see cref="Own"/> (module shutdown).</summary>
        public static void ReleaseAll(IModule owner)
        {
            int bits;
            if (owner == null || !_ownerBits.TryGetValue(owner, out bits)) return;
            _ownerBits.Remove(owner);
            if (bits != 0) ClearAll(bits);
        }

        /// <summary>Forgets FSMs whose component is gone (scene load, periodic).</summary>
        public static void Prune()
        {
            _tmp.Clear();
            foreach (var kv in _bits) { var o = kv.Key.Owner; if (o == null) _tmp.Add(kv.Key); }
            for (int i = 0; i < _tmp.Count; i++) _bits.Remove(_tmp[i]);
            _tmp.Clear();
        }

        /// <summary>
        /// For the PlayMakerFSM.Awake postfix (FsmEdits.cs:180 and FsmSleepFeature both did this): vanilla never sets
        /// ManualUpdate, so a fresh FSM carrying it was Instantiate-copied from one we had paused; clear it. Cheap no-op while
        /// nothing is paused.
        /// </summary>
        public static void ClearCopiedFlag(PlayMakerFSM fsm)
        {
            if (_bits.Count == 0 || fsm == null) return;
            var f = fsm.Fsm;
            if (f != null && f.ManualUpdate && !_bits.ContainsKey(f)) f.ManualUpdate = false;
        }
    }
}
