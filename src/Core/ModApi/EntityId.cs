using System;
using System.Globalization;

namespace WildWasteland.Core
{
    /// <summary>
    /// Stable id for anything an event refers to (core-modapi.md 3). Saved objects use their ES3 reference id (the same on every
    /// peer that loaded the save); runtime-only objects get a WW-assigned id from a per-session counter (high bit 62 set, so it can
    /// never collide with an ES3 id). 0 = none; <see cref="PlayerValue"/> = the local player. Resolve with <see cref="Entities"/>.
    /// </summary>
    public struct EntityId : IEquatable<EntityId>
    {
        /// <summary>Raw id value (0 = none).</summary>
        public readonly long Value;

        /// <summary>Reserved value of the player entity (constant per peer).</summary>
        public const long PlayerValue = 1L;

        /// <summary>Flag bit of WW-assigned runtime ids.</summary>
        public const long RuntimeBit = 1L << 62;

        /// <summary>The empty id.</summary>
        public static readonly EntityId None = new EntityId(0L);

        /// <summary>Wraps a raw id (from a save, a payload or a transport).</summary>
        public EntityId(long value) { Value = value; }

        /// <summary>True for any id except <see cref="None"/>.</summary>
        public bool IsValid { get { return Value != 0L; } }

        /// <summary>True when the id was assigned by WW at runtime (not an ES3 reference id).</summary>
        public bool IsRuntime { get { return (Value & RuntimeBit) != 0L && Value > 0L; } }

        /// <summary>Value equality.</summary>
        public bool Equals(EntityId other) { return Value == other.Value; }

        /// <summary>Value equality against a boxed id.</summary>
        public override bool Equals(object obj) { return obj is EntityId && ((EntityId)obj).Value == Value; }

        /// <summary>Hash of the raw value.</summary>
        public override int GetHashCode() { return Value.GetHashCode(); }

        /// <summary>"#&lt;value&gt;" (invariant culture), "none" for 0.</summary>
        public override string ToString() { return Value == 0L ? "none" : "#" + Value.ToString(CultureInfo.InvariantCulture); }

        /// <summary>Value equality operator.</summary>
        public static bool operator ==(EntityId a, EntityId b) { return a.Value == b.Value; }

        /// <summary>Value inequality operator.</summary>
        public static bool operator !=(EntityId a, EntityId b) { return a.Value != b.Value; }
    }
}
