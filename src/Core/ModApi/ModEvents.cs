using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace WildWasteland.Core
{
    /// <summary>Marker for event payloads. Payloads are structs with ids, enums, numbers, strings and world-space vectors only -
    /// never GameObject/Component references (use <see cref="EntityId"/>) and never scene positions (use Origin.World).</summary>
    public interface IModEvent { }

    /// <summary>Handler signature; the payload is passed by ref so large structs are not copied per subscriber.
    /// Handlers must not keep the ref beyond the call.</summary>
    public delegate void ModEventHandler<T>(ref T evt) where T : struct, IModEvent;

    /// <summary>Who may originate an event in multiplayer: Local (never sent), Host (world simulation), Owner (the controlling peer).</summary>
    public enum Authority { Local, Host, Owner }

    /// <summary>Marks an event type as broadcastable by a future network transport. Events without it are local-only.</summary>
    [AttributeUsage(AttributeTargets.Struct, Inherited = false)]
    public sealed class BroadcastAttribute : Attribute
    {
        /// <summary>Peer allowed to originate the event.</summary>
        public readonly Authority Authority;

        /// <summary>Declares the event broadcastable with the given originating authority.</summary>
        public BroadcastAttribute(Authority authority) { Authority = authority; }
    }

    /// <summary>Optional network transport, installed by a future multiplayer mod through
    /// <c>ModApi.Provide&lt;IEventTransport&gt;</c>. WW itself never provides one.</summary>
    public interface IEventTransport
    {
        /// <summary>True when this peer should send an event of <paramref name="eventType"/> with that authority (e.g. Host events only on the host).</summary>
        bool ShouldSend(Type eventType, Authority authority);

        /// <summary>Serializes and sends <paramref name="evt"/> to the other peers; called after local delivery.</summary>
        void Send<T>(ref T evt) where T : struct, IModEvent;
    }

    /// <summary>
    /// Typed event bus (core-modapi.md 2). Subscribers are owned by modules and removed automatically on that module's shutdown
    /// (ModuleHost.Cleanup -> <see cref="RemoveAll"/>). Dispatch is allocation-free: handler arrays are copied on subscribe/unsubscribe,
    /// never on publish. Each handler runs in its own try/catch counted against its module (ModuleHost error rule) and is timed while
    /// <see cref="Trace.On"/>. Main thread only.
    /// </summary>
    public static class ModEvents
    {
        /// <summary>Per-handler counters for the Debug module.</summary>
        public sealed class HandlerStat
        {
            /// <summary>Event type name.</summary>
            public string Event;
            /// <summary>Module that subscribed (null = Core).</summary>
            public IModule Owner;
            /// <summary>Handler method ("Type.Method").</summary>
            public string Method;
            /// <summary>Deliveries while tracing was on.</summary>
            public long Calls;
            /// <summary>Stopwatch ticks spent in the handler while tracing was on.</summary>
            public long Ticks;
            /// <summary>Exceptions thrown by the handler.</summary>
            public int Errors;
        }

        /// <summary>Type-erased view of a <see cref="Bus{T}"/> so RemoveAll and Stats can walk all event types.</summary>
        interface IBus
        {
            void Remove(IModule owner);
            void Stats(List<HandlerStat> into);
            long Published { get; }
        }

        static readonly List<IBus> _buses = new List<IBus>();

        /// <summary>Adds a handler owned by <paramref name="owner"/> (null = Core); removed automatically on that module's shutdown.</summary>
        public static void Subscribe<T>(IModule owner, ModEventHandler<T> handler) where T : struct, IModEvent { Bus<T>.Add(owner, handler); }

        /// <summary>Removes one handler (normally not needed: RemoveAll(owner) runs on shutdown).</summary>
        public static void Unsubscribe<T>(IModule owner, ModEventHandler<T> handler) where T : struct, IModEvent { Bus<T>.Remove(owner, handler); }

        /// <summary>Delivers <paramref name="evt"/> to local subscribers in registration order, then hands it to the transport when
        /// the type is [Broadcast] and a transport wants it. Subscribers may modify the payload (later ones see the change).</summary>
        public static void Publish<T>(ref T evt) where T : struct, IModEvent { Bus<T>.Raise(ref evt, true); }

        /// <summary>Applies a remote event (called by a transport): runs local subscribers only, never re-sends.</summary>
        public static void ApplyRemote<T>(ref T evt) where T : struct, IModEvent { Bus<T>.Raise(ref evt, false); }

        /// <summary>True when anyone listens (locally or through an installed transport for a [Broadcast] type) - lets publishers skip
        /// building payloads.</summary>
        public static bool HasListeners<T>() where T : struct, IModEvent
        {
            return Bus<T>.Count > 0 || (Bus<T>.Broadcast != null && ModApi.Get<IEventTransport>() != null);
        }

        /// <summary>The [Broadcast] authority of <typeparamref name="T"/>, or <see cref="Authority.Local"/> for local-only events.</summary>
        public static Authority AuthorityOf<T>() where T : struct, IModEvent { var b = Bus<T>.Broadcast; return b != null ? b.Authority : Authority.Local; }

        /// <summary>Removes every handler of <paramref name="owner"/> from every event type; called by ModuleHost.Cleanup.</summary>
        internal static void RemoveAll(IModule owner)
        {
            if (owner == null) return;
            for (int i = 0; i < _buses.Count; i++) _buses[i].Remove(owner);
        }

        /// <summary>Fills <paramref name="into"/> (not cleared) with one entry per subscribed handler of every event type.</summary>
        public static void Stats(List<HandlerStat> into) { for (int i = 0; i < _buses.Count; i++) _buses[i].Stats(into); }

        /// <summary>One static bus per event type: handler/owner/stat arrays swapped together as one snapshot.</summary>
        sealed class Bus<T> : IBus where T : struct, IModEvent
        {
            /// <summary>Immutable handler list; replaced on every subscribe/unsubscribe.</summary>
            sealed class Snapshot
            {
                internal readonly ModEventHandler<T>[] H; internal readonly IModule[] O; internal readonly HandlerStat[] S;
                /// <summary>Wraps the three parallel arrays.</summary>
                internal Snapshot(ModEventHandler<T>[] h, IModule[] o, HandlerStat[] s) { H = h; O = o; S = s; }
            }

            static Snapshot _list = new Snapshot(new ModEventHandler<T>[0], new IModule[0], new HandlerStat[0]);
            static Bus<T> _registered;
            static long _published;
            internal static readonly BroadcastAttribute Broadcast = ReadBroadcast();
            static readonly string Name = typeof(T).Name;
            static readonly string Where = "event " + typeof(T).Name;

            /// <summary>Number of local handlers.</summary>
            internal static int Count { get { return _list.H.Length; } }

            /// <summary>Total Publish/ApplyRemote calls of this type.</summary>
            public long Published { get { return _published; } }

            /// <summary>Reads the [Broadcast] attribute once per event type.</summary>
            static BroadcastAttribute ReadBroadcast()
            {
                var a = typeof(T).GetCustomAttributes(typeof(BroadcastAttribute), false);
                return a.Length > 0 ? (BroadcastAttribute)a[0] : null;
            }

            /// <summary>Registers this bus for RemoveAll/Stats on first subscription.</summary>
            static void Register() { if (_registered == null) { _registered = new Bus<T>(); _buses.Add(_registered); } }

            /// <summary>Appends a handler (copy-on-write).</summary>
            internal static void Add(IModule owner, ModEventHandler<T> handler)
            {
                if (handler == null) return;
                Register();
                var l = _list; int n = l.H.Length;
                var h = new ModEventHandler<T>[n + 1]; var o = new IModule[n + 1]; var s = new HandlerStat[n + 1];
                Array.Copy(l.H, h, n); Array.Copy(l.O, o, n); Array.Copy(l.S, s, n);
                h[n] = handler; o[n] = owner;
                s[n] = new HandlerStat { Event = Name, Owner = owner, Method = handler.Method.DeclaringType != null ? handler.Method.DeclaringType.Name + "." + handler.Method.Name : handler.Method.Name };
                _list = new Snapshot(h, o, s);
            }

            /// <summary>Removes handlers matching owner (and handler when given), copy-on-write.</summary>
            static void Filter(IModule owner, ModEventHandler<T> handler)
            {
                var l = _list;
                var h = new List<ModEventHandler<T>>(); var o = new List<IModule>(); var s = new List<HandlerStat>();
                for (int i = 0; i < l.H.Length; i++)
                {
                    if (l.O[i] == owner && (handler == null || l.H[i] == handler)) continue;
                    h.Add(l.H[i]); o.Add(l.O[i]); s.Add(l.S[i]);
                }
                if (h.Count != l.H.Length) _list = new Snapshot(h.ToArray(), o.ToArray(), s.ToArray());
            }

            /// <summary>Removes one handler of <paramref name="owner"/>.</summary>
            internal static void Remove(IModule owner, ModEventHandler<T> handler) { if (handler != null) Filter(owner, handler); }

            /// <summary>Removes every handler of <paramref name="owner"/>.</summary>
            void IBus.Remove(IModule owner) { Filter(owner, null); }

            /// <summary>Copies the handler stats into the Debug list.</summary>
            void IBus.Stats(List<HandlerStat> into) { into.AddRange(_list.S); }

            /// <summary>Delivers to every handler (try/catch + optional timing), then to the transport when <paramref name="send"/>.</summary>
            internal static void Raise(ref T evt, bool send)
            {
                _published++;
                var l = _list; var hs = l.H; bool trace = Trace.On;
                for (int i = 0; i < hs.Length; i++)
                {
                    long t0 = trace ? Stopwatch.GetTimestamp() : 0L;
                    try { hs[i](ref evt); }
                    catch (Exception e) { l.S[i].Errors++; ModuleHost.ReportHookException(l.O[i], Where, e); }
                    if (trace) { var st = l.S[i]; st.Calls++; st.Ticks += Stopwatch.GetTimestamp() - t0; }
                }
                if (!send || Broadcast == null) return;
                var transport = ModApi.Get<IEventTransport>();
                if (transport == null) return;
                try { if (transport.ShouldSend(typeof(T), Broadcast.Authority)) transport.Send(ref evt); }
                catch (Exception e) { ModuleHost.ReportHookException(ModApi.OwnerOf<IEventTransport>(), "transport " + Name, e); }
            }
        }
    }
}
