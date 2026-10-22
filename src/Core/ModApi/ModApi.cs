using System;
using System.Collections.Generic;
using System.Text;

namespace WildWasteland.Core
{
    /// <summary>
    /// Typed service registry: the only way modules reach each other (README 3.2, core-modapi.md 1). Providers register in
    /// Init or <see cref="IModule.ProvideApis"/>; every entry owned by a module is removed automatically when that module shuts
    /// down (ModuleHost.Cleanup -> <see cref="RemoveAll"/>). Consumers call <see cref="Get{T}"/> at the point of use and handle null
    /// (provider module off). Main thread only.
    /// </summary>
    /// <remarks>The registry lives in namespace <c>WildWasteland.Core</c> (not <c>WildWasteland.Core.ModApi</c> as sketched in
    /// core-modapi.md): a namespace and a class both named ModApi would make <c>ModApi.Get</c> unresolvable inside Core.</remarks>
    public static class ModApi
    {
        /// <summary>Type-erased view of one <see cref="Slot{T}"/> so RemoveAll and the Debug listing can walk every registered contract.</summary>
        interface ISlot
        {
            Type Contract { get; }
            IModule Owner { get; }
            object Value { get; }
            void Clear();
        }

        static readonly List<ISlot> _slots = new List<ISlot>();

        /// <summary>Raised (with a level-1 log line) when a provider appears, is replaced or disappears, so consumers can drop caches.</summary>
        public static event Action<Type> Changed;

        /// <summary>Registers <paramref name="impl"/> as the provider of <typeparamref name="T"/> for <paramref name="owner"/>
        /// (null owner = Core). A second provider replaces the first and logs a level-1 line (Compatibility may wrap a module
        /// provider). Passing null withdraws the current provider.</summary>
        public static void Provide<T>(IModule owner, T impl) where T : class { Slot<T>.Set(owner, impl); }

        /// <summary>Withdraws the provider of <typeparamref name="T"/> only when <paramref name="owner"/> registered it
        /// (a module never clears another module's provider). Normally not needed: shutdown removes it.</summary>
        public static void Withdraw<T>(IModule owner) where T : class { if (Slot<T>.Value != null && Slot<T>.Owner == owner) Slot<T>.Set(owner, null); }

        /// <summary>Current provider or null when the providing module is off. One static field read, safe on hot paths.</summary>
        public static T Get<T>() where T : class { return Slot<T>.Value; }

        /// <summary>Convenience form of <see cref="Get{T}"/> for if-patterns.</summary>
        public static bool TryGet<T>(out T api) where T : class { api = Slot<T>.Value; return api != null; }

        /// <summary>The module that provides <typeparamref name="T"/> right now (null for Core or when nobody does).</summary>
        public static IModule OwnerOf<T>() where T : class { return Slot<T>.Value != null ? Slot<T>.Owner : null; }

        /// <summary>Removes every provider owned by <paramref name="owner"/>; called by ModuleHost.Cleanup after Shutdown.</summary>
        internal static void RemoveAll(IModule owner)
        {
            if (owner == null) return;
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.Owner == owner && s.Value != null) s.Clear();
            }
        }

        /// <summary>One line per registered contract ("INpcApi &lt;- NPC"), for the Debug module's pages and the startup summary.</summary>
        public static string Describe()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.Value == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(s.Contract.Name).Append(" <- ").Append(s.Owner != null ? s.Owner.Id : "Core");
            }
            return sb.ToString();
        }

        /// <summary>Raises <see cref="Changed"/> for <paramref name="contract"/>; each handler in its own try/catch.</summary>
        static void RaiseChanged(Type contract)
        {
            var h = Changed;
            if (h == null) return;
            foreach (Action<Type> d in h.GetInvocationList())
            {
                try { d(contract); } catch (Exception e) { Log.Error(null,"[Core] ModApi.Changed handler " + d.Method.Name, e); }
            }
        }

        /// <summary>Generic static holder: one per contract type, so <see cref="Get{T}"/> is a single field read.</summary>
        sealed class Slot<T> : ISlot where T : class
        {
            internal static T Value;
            internal static IModule Owner;
            static Slot<T> _registered;

            /// <summary>Contract type of the slot.</summary>
            public Type Contract { get { return typeof(T); } }
            IModule ISlot.Owner { get { return Owner; } }
            object ISlot.Value { get { return Value; } }

            /// <summary>Clears the provider (called by RemoveAll for the owning module).</summary>
            public void Clear() { Set(Owner, null); }

            /// <summary>Stores the provider, registers the slot for RemoveAll on first use and announces the change.</summary>
            internal static void Set(IModule owner, T value)
            {
                if (_registered == null) { _registered = new Slot<T>(); _slots.Add(_registered); }
                if (ReferenceEquals(Value, value) && Owner == owner) return;
                var previous = Owner;
                bool replaced = Value != null && value != null;
                Value = value; Owner = value != null ? owner : null;
                if (Log.L1)
                    Log.Main(owner ?? previous, (value == null ? "withdrew " : replaced ? "replaced provider of " : "provides ") + typeof(T).Name
                        + (replaced ? " (was " + (previous != null ? previous.Id : "Core") + ")" : ""));
                RaiseChanged(typeof(T));
            }
        }
    }
}
