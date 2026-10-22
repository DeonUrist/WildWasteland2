using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// Shared Harmony dispatchers (AGENTS.md 5.6): ONE patch per target per kind for the whole mod. Modules subscribe
    /// handlers instead of patching these targets themselves. Semantics match HarmonyX: every prefix handler runs
    /// (in module init order) and the original is skipped when any returned false; postfix/finalizer handlers always run.
    /// Handlers must start with a cheap filter (static bool, reference compare, cached set) - they run for every FSM in the game.
    /// Table-driven (README 4.1): every dispatcher registers itself in <see cref="All"/>, which drives <see cref="RemoveAll"/>,
    /// <see cref="ApplyUsed"/>, <see cref="Summary"/> and <see cref="Stats"/>, so a new dispatcher needs only its field and its patch
    /// class. <see cref="HookBase.Calls"/> counts every dispatch of the target (postfix-only targets too); per-handler time is
    /// measured while <see cref="Trace.On"/>. Note for Fsm.ProcessEvent subscribers: Optimization PM5/PM6 skip unhandled
    /// TRIGGER/COLLISION/BECAME VISIBLE events, so those never reach the dispatcher.
    /// </summary>
    public static class Hooks
    {
        /// <summary>Prefix handler: return false to skip the original (all handlers still run).</summary>
        public delegate bool PrefixHandler<T>(T action);
        /// <summary>Postfix handler.</summary>
        public delegate void PostfixHandler<T>(T action);
        /// <summary>Finalizer handler: returns the exception to rethrow (null swallows it).</summary>
        public delegate Exception FinalizerHandler<T>(T action, Exception exception);
        /// <summary>Prefix handler of a static/argument target: fields of <paramref name="args"/> are the call's arguments and result.</summary>
        public delegate bool RefPrefix<A>(ref A args) where A : struct;
        /// <summary>Postfix handler of a static/argument target (may change the result field).</summary>
        public delegate void RefPostfix<A>(ref A args) where A : struct;

        /// <summary>Patch methods a dispatcher's Harmony class contains.</summary>
        [Flags] public enum Kinds { Prefix = 1, Postfix = 2, Finalizer = 4 }

        /// <summary>Counters of one subscribed handler (Debug "hooks" page).</summary>
        public sealed class HandlerStat
        {
            /// <summary>Subscribing module (null = Core).</summary>
            public IModule Owner;
            /// <summary>Dispatcher target ("CreateObject.OnEnter").</summary>
            public string Target;
            /// <summary>"prefix", "postfix" or "finalizer".</summary>
            public string Kind;
            /// <summary>Handler method ("Type.Method").</summary>
            public string Method;
            /// <summary>Calls while tracing was on.</summary>
            public long Calls;
            /// <summary>Stopwatch ticks spent while tracing was on.</summary>
            public long Ticks;
            /// <summary>Exceptions thrown by the handler.</summary>
            public int Errors;
        }

        static readonly List<HookBase> _all = new List<HookBase>();

        /// <summary>Every dispatcher, in declaration order (= summary order).</summary>
        public static IList<HookBase> All { get { return _all; } }

        /// <summary>Base of every dispatcher: identity, patch class, counters and the table hooks.</summary>
        public abstract class HookBase
        {
            /// <summary>Target name shown in the summary and error reports.</summary>
            public readonly string Target;
            /// <summary>Harmony class that forwards the target to this dispatcher.</summary>
            internal readonly Type PatchClass;
            /// <summary>Patch methods contained in <see cref="PatchClass"/>.</summary>
            public readonly Kinds PatchKinds;
            /// <summary>Patched even without subscribers (FloatingOrigin.Jump).</summary>
            internal readonly bool Always;
            /// <summary>True once the Harmony patch was applied.</summary>
            public bool Applied { get; internal set; }
            /// <summary>Dispatches of the target since start (counted once per original call).</summary>
            public long Calls;

            /// <summary>Registers the dispatcher in <see cref="All"/>.</summary>
            protected HookBase(string target, Type patchClass, Kinds kinds, bool always)
            {
                Target = target; PatchClass = patchClass; PatchKinds = kinds; Always = always;
                _all.Add(this);
            }

            /// <summary>Prefix (or resolver) handler count.</summary>
            public abstract int PrefixCount { get; }
            /// <summary>Postfix handler count.</summary>
            public virtual int PostfixCount { get { return 0; } }
            /// <summary>Finalizer handler count.</summary>
            public virtual int FinalizerCount { get { return 0; } }
            /// <summary>Total handler count.</summary>
            public int SubscriberCount { get { return PrefixCount + PostfixCount + FinalizerCount; } }

            /// <summary>Modules with at least one handler.</summary>
            public IEnumerable<IModule> Subscribers
            {
                get { var l = new List<HandlerStat>(); Stats(l); var s = new HashSet<IModule>(); foreach (var h in l) s.Add(h.Owner); return s; }
            }

            /// <summary>Removes every handler of <paramref name="owner"/>.</summary>
            internal abstract void Remove(IModule owner);

            /// <summary>Appends the stats of every handler to <paramref name="into"/>.</summary>
            public abstract void Stats(List<HandlerStat> into);

            /// <summary>True when the postfix/finalizer runner counts <see cref="Calls"/> (the patch class has no prefix).</summary>
            protected bool CountsAfter { get { return (PatchKinds & Kinds.Prefix) == 0; } }

            /// <summary>Warns once when a module subscribes a kind the patch class does not have (the handler would never run).</summary>
            protected void Check(Kinds kind, IModule owner)
            {
                if ((PatchKinds & kind) == 0) Log.Warn(owner, "Hooks." + Target + " has no " + kind + " patch: handler ignored by the dispatcher");
            }
        }

        /// <summary>Copy-on-write handler list (handlers, owners and stats swapped together as one snapshot, so a dispatch that is
        /// running while a handler's module gets removed still reports to the right owner).</summary>
        internal sealed class Handlers<D> where D : class
        {
            /// <summary>Immutable snapshot.</summary>
            internal sealed class Snap
            {
                internal readonly D[] H; internal readonly IModule[] O; internal readonly HandlerStat[] S;
                /// <summary>Wraps the three parallel arrays.</summary>
                internal Snap(D[] h, IModule[] o, HandlerStat[] s) { H = h; O = o; S = s; }
            }

            internal Snap L = new Snap(new D[0], new IModule[0], new HandlerStat[0]);
            readonly string _target, _kind;

            /// <summary>Handler list of one kind of one dispatcher.</summary>
            internal Handlers(string target, string kind) { _target = target; _kind = kind; }

            /// <summary>Number of handlers.</summary>
            internal int Count { get { return L.H.Length; } }

            /// <summary>Appends a handler.</summary>
            internal void Add(IModule owner, D handler)
            {
                if (handler == null) return;
                var l = L; int n = l.H.Length;
                var h = new D[n + 1]; var o = new IModule[n + 1]; var s = new HandlerStat[n + 1];
                Array.Copy(l.H, h, n); Array.Copy(l.O, o, n); Array.Copy(l.S, s, n);
                var m = ((Delegate)(object)handler).Method;
                h[n] = handler; o[n] = owner;
                s[n] = new HandlerStat { Owner = owner, Target = _target, Kind = _kind, Method = m.DeclaringType != null ? m.DeclaringType.Name + "." + m.Name : m.Name };
                L = new Snap(h, o, s);
            }

            /// <summary>Removes every handler of <paramref name="owner"/>.</summary>
            internal void Remove(IModule owner)
            {
                var l = L;
                int keep = 0;
                for (int i = 0; i < l.O.Length; i++) if (l.O[i] != owner) keep++;
                if (keep == l.O.Length) return;
                var h = new D[keep]; var o = new IModule[keep]; var s = new HandlerStat[keep];
                for (int i = 0, k = 0; i < l.O.Length; i++)
                    if (l.O[i] != owner) { h[k] = l.H[i]; o[k] = l.O[i]; s[k] = l.S[i]; k++; }
                L = new Snap(h, o, s);
            }

            /// <summary>Appends this list's stats.</summary>
            internal void Stats(List<HandlerStat> into) { into.AddRange(L.S); }
        }

        /// <summary>Reports a handler exception against its module and counts it.</summary>
        static void Fail(HandlerStat stat, IModule owner, string target, Exception e)
        {
            stat.Errors++;
            ModuleHost.ReportHookException(owner, target, e);
        }

        /// <summary>Adds the elapsed time since <paramref name="t0"/> to a handler's stats.</summary>
        static void Timed(HandlerStat stat, long t0) { stat.Calls++; stat.Ticks += Stopwatch.GetTimestamp() - t0; }

        // ------------------------------------------------------------------ dispatcher kinds

        /// <summary>Dispatcher of an instance method (handlers receive the instance: PlayMaker action, PlayMakerFSM, Fsm...).</summary>
        public sealed class Hook<T> : HookBase where T : class
        {
            readonly Handlers<PrefixHandler<T>> _pre;
            readonly Handlers<PostfixHandler<T>> _post;
            readonly Handlers<FinalizerHandler<T>> _fin;

            /// <summary>Declares the dispatcher of <paramref name="target"/> forwarded by <paramref name="patchClass"/>.</summary>
            internal Hook(string target, Type patchClass, Kinds kinds, bool always = false) : base(target, patchClass, kinds, always)
            {
                _pre = new Handlers<PrefixHandler<T>>(target, "prefix");
                _post = new Handlers<PostfixHandler<T>>(target, "postfix");
                _fin = new Handlers<FinalizerHandler<T>>(target, "finalizer");
            }

            /// <summary>Prefix handler count.</summary>
            public override int PrefixCount { get { return _pre.Count; } }
            /// <summary>Postfix handler count.</summary>
            public override int PostfixCount { get { return _post.Count; } }
            /// <summary>Finalizer handler count.</summary>
            public override int FinalizerCount { get { return _fin.Count; } }

            /// <summary>Subscribes a prefix (return false to skip the original).</summary>
            public void Prefix(IModule owner, PrefixHandler<T> h) { Check(Kinds.Prefix, owner); _pre.Add(owner, h); }
            /// <summary>Subscribes a postfix.</summary>
            public void Postfix(IModule owner, PostfixHandler<T> h) { Check(Kinds.Postfix, owner); _post.Add(owner, h); }
            /// <summary>Subscribes a finalizer.</summary>
            public void Finalizer(IModule owner, FinalizerHandler<T> h) { Check(Kinds.Finalizer, owner); _fin.Add(owner, h); }

            /// <summary>Removes every handler of <paramref name="owner"/>.</summary>
            internal override void Remove(IModule owner) { _pre.Remove(owner); _post.Remove(owner); _fin.Remove(owner); }

            /// <summary>Appends handler stats.</summary>
            public override void Stats(List<HandlerStat> into) { _pre.Stats(into); _post.Stats(into); _fin.Stats(into); }

            /// <summary>Runs every prefix; false when any asked to skip the original.</summary>
            public bool RunPrefix(T a)
            {
                Calls++;
                var l = _pre.L; var hs = l.H; bool run = true, tr = Trace.On;
                for (int i = 0; i < hs.Length; i++)
                {
                    long t0 = tr ? Stopwatch.GetTimestamp() : 0L;
                    try { if (!hs[i](a)) run = false; }
                    catch (Exception e) { Fail(l.S[i], l.O[i], Target, e); }
                    if (tr) Timed(l.S[i], t0);
                }
                return run;
            }

            /// <summary>Runs every postfix.</summary>
            public void RunPostfix(T a)
            {
                if (CountsAfter) Calls++;
                var l = _post.L; var hs = l.H; bool tr = Trace.On;
                for (int i = 0; i < hs.Length; i++)
                {
                    long t0 = tr ? Stopwatch.GetTimestamp() : 0L;
                    try { hs[i](a); }
                    catch (Exception e) { Fail(l.S[i], l.O[i], Target, e); }
                    if (tr) Timed(l.S[i], t0);
                }
            }

            /// <summary>Runs every finalizer in order, threading the exception through them.</summary>
            public Exception RunFinalizer(T a, Exception ex)
            {
                if ((PatchKinds & (Kinds.Prefix | Kinds.Postfix)) == 0) Calls++;
                var l = _fin.L; var hs = l.H; bool tr = Trace.On;
                for (int i = 0; i < hs.Length; i++)
                {
                    long t0 = tr ? Stopwatch.GetTimestamp() : 0L;
                    try { ex = hs[i](a, ex); }
                    catch (Exception e) { Fail(l.S[i], l.O[i], Target, e); }
                    if (tr) Timed(l.S[i], t0);
                }
                return ex;
            }
        }

        /// <summary>Dispatcher of a static method or a method whose arguments/result handlers need: one args struct passed by ref.</summary>
        public sealed class RefHook<A> : HookBase where A : struct
        {
            readonly Handlers<RefPrefix<A>> _pre;
            readonly Handlers<RefPostfix<A>> _post;

            /// <summary>Declares the dispatcher of <paramref name="target"/> forwarded by <paramref name="patchClass"/>.</summary>
            internal RefHook(string target, Type patchClass, Kinds kinds) : base(target, patchClass, kinds, false)
            {
                _pre = new Handlers<RefPrefix<A>>(target, "prefix");
                _post = new Handlers<RefPostfix<A>>(target, "postfix");
            }

            /// <summary>Prefix handler count.</summary>
            public override int PrefixCount { get { return _pre.Count; } }
            /// <summary>Postfix handler count.</summary>
            public override int PostfixCount { get { return _post.Count; } }

            /// <summary>Subscribes a prefix (return false to skip the original; set the result field when skipping).</summary>
            public void Prefix(IModule owner, RefPrefix<A> h) { Check(Kinds.Prefix, owner); _pre.Add(owner, h); }
            /// <summary>Subscribes a postfix.</summary>
            public void Postfix(IModule owner, RefPostfix<A> h) { Check(Kinds.Postfix, owner); _post.Add(owner, h); }

            /// <summary>Removes every handler of <paramref name="owner"/>.</summary>
            internal override void Remove(IModule owner) { _pre.Remove(owner); _post.Remove(owner); }

            /// <summary>Appends handler stats.</summary>
            public override void Stats(List<HandlerStat> into) { _pre.Stats(into); _post.Stats(into); }

            /// <summary>Runs every prefix; false when any asked to skip the original.</summary>
            public bool RunPrefix(ref A a)
            {
                Calls++;
                var l = _pre.L; var hs = l.H; bool run = true, tr = Trace.On;
                for (int i = 0; i < hs.Length; i++)
                {
                    long t0 = tr ? Stopwatch.GetTimestamp() : 0L;
                    try { if (!hs[i](ref a)) run = false; }
                    catch (Exception e) { Fail(l.S[i], l.O[i], Target, e); }
                    if (tr) Timed(l.S[i], t0);
                }
                return run;
            }

            /// <summary>Runs every postfix.</summary>
            public void RunPostfix(ref A a)
            {
                if (CountsAfter) Calls++;
                var l = _post.L; var hs = l.H; bool tr = Trace.On;
                for (int i = 0; i < hs.Length; i++)
                {
                    long t0 = tr ? Stopwatch.GetTimestamp() : 0L;
                    try { hs[i](ref a); }
                    catch (Exception e) { Fail(l.S[i], l.O[i], Target, e); }
                    if (tr) Timed(l.S[i], t0);
                }
            }
        }

        /// <summary>Fsm.ProcessEvent(FsmEvent, FsmEventData) prefix handler.</summary>
        public delegate bool FsmEventPrefix(Fsm fsm, FsmEvent ev, FsmEventData data);

        /// <summary>Dispatcher of Fsm.ProcessEvent: handlers get the Fsm, the event and its data.</summary>
        public sealed class FsmEventHook : HookBase
        {
            readonly Handlers<FsmEventPrefix> _pre;

            /// <summary>Declares the dispatcher.</summary>
            internal FsmEventHook(string target, Type patchClass) : base(target, patchClass, Kinds.Prefix, false) { _pre = new Handlers<FsmEventPrefix>(target, "prefix"); }

            /// <summary>Prefix handler count.</summary>
            public override int PrefixCount { get { return _pre.Count; } }
            /// <summary>Subscribes a prefix.</summary>
            public void Prefix(IModule owner, FsmEventPrefix h) { _pre.Add(owner, h); }
            /// <summary>Removes every handler of <paramref name="owner"/>.</summary>
            internal override void Remove(IModule owner) { _pre.Remove(owner); }
            /// <summary>Appends handler stats.</summary>
            public override void Stats(List<HandlerStat> into) { _pre.Stats(into); }

            /// <summary>Runs every prefix; false when any asked to skip the original.</summary>
            public bool Run(Fsm fsm, FsmEvent ev, FsmEventData data)
            {
                Calls++;
                var l = _pre.L; var hs = l.H; bool run = true, tr = Trace.On;
                for (int i = 0; i < hs.Length; i++)
                {
                    long t0 = tr ? Stopwatch.GetTimestamp() : 0L;
                    try { if (!hs[i](fsm, ev, data)) run = false; }
                    catch (Exception e) { Fail(l.S[i], l.O[i], Target, e); }
                    if (tr) Timed(l.S[i], t0);
                }
                return run;
            }
        }

        /// <summary>Fsm.EnterState prefix handler.</summary>
        public delegate bool FsmStatePrefix(Fsm fsm, FsmState state);
        /// <summary>Fsm.EnterState postfix handler.</summary>
        public delegate void FsmStatePostfix(Fsm fsm, FsmState state);

        /// <summary>Dispatcher of Fsm.EnterState(FsmState): prefix/postfix with (Fsm, FsmState).</summary>
        public sealed class FsmStateHook : HookBase
        {
            readonly Handlers<FsmStatePrefix> _pre;
            readonly Handlers<FsmStatePostfix> _post;

            /// <summary>Declares the dispatcher.</summary>
            internal FsmStateHook(string target, Type patchClass) : base(target, patchClass, Kinds.Prefix | Kinds.Postfix, false)
            {
                _pre = new Handlers<FsmStatePrefix>(target, "prefix");
                _post = new Handlers<FsmStatePostfix>(target, "postfix");
            }

            /// <summary>Prefix handler count.</summary>
            public override int PrefixCount { get { return _pre.Count; } }
            /// <summary>Postfix handler count.</summary>
            public override int PostfixCount { get { return _post.Count; } }
            /// <summary>Subscribes a prefix.</summary>
            public void Prefix(IModule owner, FsmStatePrefix h) { _pre.Add(owner, h); }
            /// <summary>Subscribes a postfix.</summary>
            public void Postfix(IModule owner, FsmStatePostfix h) { _post.Add(owner, h); }
            /// <summary>Removes every handler of <paramref name="owner"/>.</summary>
            internal override void Remove(IModule owner) { _pre.Remove(owner); _post.Remove(owner); }
            /// <summary>Appends handler stats.</summary>
            public override void Stats(List<HandlerStat> into) { _pre.Stats(into); _post.Stats(into); }

            /// <summary>Runs every prefix; false when any asked to skip the original.</summary>
            public bool RunPrefix(Fsm fsm, FsmState st)
            {
                Calls++;
                var l = _pre.L; var hs = l.H; bool run = true, tr = Trace.On;
                for (int i = 0; i < hs.Length; i++)
                {
                    long t0 = tr ? Stopwatch.GetTimestamp() : 0L;
                    try { if (!hs[i](fsm, st)) run = false; }
                    catch (Exception e) { Fail(l.S[i], l.O[i], Target, e); }
                    if (tr) Timed(l.S[i], t0);
                }
                return run;
            }

            /// <summary>Runs every postfix.</summary>
            public void RunPostfix(Fsm fsm, FsmState st)
            {
                var l = _post.L; var hs = l.H; bool tr = Trace.On;
                for (int i = 0; i < hs.Length; i++)
                {
                    long t0 = tr ? Stopwatch.GetTimestamp() : 0L;
                    try { hs[i](fsm, st); }
                    catch (Exception e) { Fail(l.S[i], l.O[i], Target, e); }
                    if (tr) Timed(l.S[i], t0);
                }
            }
        }

        /// <summary>ES3 prefab resolver: returns the prefab for a saved prefab id, or null.</summary>
        public delegate ES3Internal.ES3Prefab PrefabResolver(long id);

        /// <summary>Dispatcher of ES3ReferenceMgrBase.GetPrefab(long,bool): the first resolver that returns a prefab wins.</summary>
        public sealed class PrefabHook : HookBase
        {
            readonly Handlers<PrefabResolver> _pre;

            /// <summary>Declares the dispatcher.</summary>
            internal PrefabHook(string target, Type patchClass) : base(target, patchClass, Kinds.Prefix, false) { _pre = new Handlers<PrefabResolver>(target, "resolver"); }

            /// <summary>Resolver count.</summary>
            public override int PrefixCount { get { return _pre.Count; } }
            /// <summary>Subscribes a resolver.</summary>
            public void Resolver(IModule owner, PrefabResolver h) { _pre.Add(owner, h); }
            /// <summary>Removes every resolver of <paramref name="owner"/>.</summary>
            internal override void Remove(IModule owner) { _pre.Remove(owner); }
            /// <summary>Appends handler stats.</summary>
            public override void Stats(List<HandlerStat> into) { _pre.Stats(into); }

            /// <summary>First non-null prefab of the resolvers, or null.</summary>
            public ES3Internal.ES3Prefab Run(long id)
            {
                Calls++;
                var l = _pre.L; var hs = l.H; bool tr = Trace.On;
                for (int i = 0; i < hs.Length; i++)
                {
                    long t0 = tr ? Stopwatch.GetTimestamp() : 0L;
                    ES3Internal.ES3Prefab p = null;
                    try { p = hs[i](id); }
                    catch (Exception e) { Fail(l.S[i], l.O[i], Target, e); }
                    if (tr) Timed(l.S[i], t0);
                    if (p != null) return p;
                }
                return null;
            }
        }

        // ------------------------------------------------------------------ argument structs of the RefHook dispatchers

        /// <summary>ActionHelpers.DoMousePick(float distance, int layerMask) (static, void).</summary>
        public struct MousePickArgs
        {
            /// <summary>Pick distance.</summary>
            public float Distance;
            /// <summary>Layer mask.</summary>
            public int LayerMask;
        }

        /// <summary>ActionHelpers.GetGameObjectFsm(GameObject, string) (static): set <see cref="Result"/> and return false to skip.</summary>
        public struct GetFsmArgs
        {
            /// <summary>Object searched.</summary>
            public GameObject Go;
            /// <summary>FSM name searched.</summary>
            public string FsmName;
            /// <summary>Result (postfix: the original's result).</summary>
            public PlayMakerFSM Result;
        }

        /// <summary>Explosion.ShouldApplyForce(GameObject) (private instance, bool).</summary>
        public struct ExplosionForceArgs
        {
            /// <summary>The Explosion action.</summary>
            public Explosion Explosion;
            /// <summary>Object the force would apply to.</summary>
            public GameObject Target;
            /// <summary>Result (postfix: the original's result; set it to change the outcome).</summary>
            public bool Result;
        }

        /// <summary>Which Fsm physics callback ran.</summary>
        public enum FsmPhysicsKind { TriggerEnter, TriggerStay, TriggerExit, CollisionEnter, CollisionStay, CollisionExit }

        /// <summary>Fsm.OnTrigger*/OnCollision* (instance, void): <see cref="Collider"/> for triggers, <see cref="Collision"/> for collisions.</summary>
        public struct FsmPhysicsArgs
        {
            /// <summary>The Fsm.</summary>
            public Fsm Fsm;
            /// <summary>Callback kind.</summary>
            public FsmPhysicsKind Kind;
            /// <summary>Other collider (trigger callbacks).</summary>
            public Collider Collider;
            /// <summary>Collision info (collision callbacks).</summary>
            public Collision Collision;
        }

        // ------------------------------------------------------------------ dispatchers (declaration order = summary order)

        // ---- PlayMaker actions (instance = the action) ----
        public static readonly Hook<CreateObject> CreateObjectEnter = new Hook<CreateObject>("CreateObject.OnEnter", typeof(P_CreateObjectEnter), Kinds.Prefix | Kinds.Postfix);
        public static readonly Hook<SendEvent> SendEventEnter = new Hook<SendEvent>("SendEvent.OnEnter", typeof(P_SendEventEnter), Kinds.Prefix | Kinds.Postfix);
        public static readonly Hook<SendEvent> SendEventUpdate = new Hook<SendEvent>("SendEvent.OnUpdate", typeof(P_SendEventUpdate), Kinds.Prefix);
        public static readonly Hook<SetFsmFloat> SetFsmFloatEnter = new Hook<SetFsmFloat>("SetFsmFloat.OnEnter", typeof(P_SetFsmFloatEnter), Kinds.Prefix | Kinds.Postfix);
        public static readonly Hook<SetFsmFloat> SetFsmFloatDo = new Hook<SetFsmFloat>("SetFsmFloat.DoSetFsmFloat", typeof(P_SetFsmFloatDo), Kinds.Prefix);
        public static readonly Hook<SetVelocity> SetVelocityDo = new Hook<SetVelocity>("SetVelocity.DoSetVelocity", typeof(P_SetVelocityDo), Kinds.Prefix);
        public static readonly Hook<MouseLook> MouseLookDo = new Hook<MouseLook>("MouseLook.DoMouseLook", typeof(P_MouseLookDo), Kinds.Prefix);
        public static readonly Hook<Wait> WaitUpdate = new Hook<Wait>("Wait.OnUpdate", typeof(P_WaitUpdate), Kinds.Prefix);
        public static readonly Hook<GetButtonDown> GetButtonDownUpdate = new Hook<GetButtonDown>("GetButtonDown.OnUpdate", typeof(P_GetButtonDownUpdate), Kinds.Prefix);
        public static readonly Hook<GetButton> GetButtonDo = new Hook<GetButton>("GetButton.DoGetButton", typeof(P_GetButtonDo), Kinds.Prefix);
        public static readonly Hook<GetButtonUp> GetButtonUpUpdate = new Hook<GetButtonUp>("GetButtonUp.OnUpdate", typeof(P_GetButtonUpUpdate), Kinds.Prefix);
        public static readonly Hook<GetMouseButtonDown> GetMouseButtonDownUpdate = new Hook<GetMouseButtonDown>("GetMouseButtonDown.OnUpdate", typeof(P_GetMouseButtonDownUpdate), Kinds.Prefix);
        public static readonly Hook<Raycast> RaycastEnter = new Hook<Raycast>("Raycast.OnEnter", typeof(P_RaycastEnter), Kinds.Prefix | Kinds.Postfix);
        public static readonly Hook<Raycast> RaycastDo = new Hook<Raycast>("Raycast.DoRaycast", typeof(P_RaycastDo), Kinds.Prefix);
        public static readonly Hook<SmoothLookAt> SmoothLookAtDo = new Hook<SmoothLookAt>("SmoothLookAt.DoSmoothLookAt", typeof(P_SmoothLookAtDo), Kinds.Prefix);
        public static readonly Hook<ActivateGameObject> ActivateGameObjectEnter = new Hook<ActivateGameObject>("ActivateGameObject.OnEnter", typeof(P_ActivateGameObjectEnter), Kinds.Prefix | Kinds.Postfix);
        public static readonly Hook<ActivateGameObject> ActivateGameObjectDo = new Hook<ActivateGameObject>("ActivateGameObject.DoActivateGameObject", typeof(P_ActivateGameObjectDo), Kinds.Prefix);
        public static readonly Hook<SetPosition> SetPositionDo = new Hook<SetPosition>("SetPosition.DoSetPosition", typeof(P_SetPositionDo), Kinds.Prefix | Kinds.Postfix);
        public static readonly Hook<SensorGetDetectionRayHit> SensorRayHitEnter = new Hook<SensorGetDetectionRayHit>("SensorGetDetectionRayHit.OnEnter", typeof(P_SensorRayHitEnter), Kinds.Prefix | Kinds.Finalizer);
        public static readonly Hook<SensorGetLineOfSightResult> LosEnter3D = new Hook<SensorGetLineOfSightResult>("SensorGetLineOfSightResult.OnEnter3D", typeof(P_LosEnter3D), Kinds.Prefix);
        public static readonly Hook<SensorGetLineOfSightResult> LosUpdate3D = new Hook<SensorGetLineOfSightResult>("SensorGetLineOfSightResult.OnUpdate3D", typeof(P_LosUpdate3D), Kinds.Prefix);
        public static readonly Hook<SensorGetDetections> SensorDetectionsDo = new Hook<SensorGetDetections>("SensorGetDetections.DoAction", typeof(P_SensorDetectionsDo), Kinds.Prefix);

        // ---- Fsm core ----
        /// <summary>Fsm.ProcessEvent(FsmEvent, FsmEventData): handlers get the Fsm and the event.</summary>
        public static readonly FsmEventHook ProcessEvent = new FsmEventHook("Fsm.ProcessEvent", typeof(P_ProcessEvent));
        /// <summary>Fsm.EnterState(FsmState): prefix/postfix with (Fsm, FsmState).</summary>
        public static readonly FsmStateHook EnterState = new FsmStateHook("Fsm.EnterState", typeof(P_EnterState));
        /// <summary>ES3ReferenceMgrBase.GetPrefab(long,bool): the first handler that returns a prefab wins.</summary>
        public static readonly PrefabHook GetPrefab = new PrefabHook("ES3.GetPrefab", typeof(P_GetPrefab));
        /// <summary>PlayMakerFSM.OnEnable postfix (after vanilla appended the FSM to FsmList). Postfix handlers only.</summary>
        public static readonly Hook<PlayMakerFSM> FsmEnable = new Hook<PlayMakerFSM>("PlayMakerFSM.OnEnable", typeof(P_FsmEnable), Kinds.Postfix);
        /// <summary>NWH FloatingOrigin.Jump (always patched): Core raises IModule.OnOriginShift; modules needing the raw before/after use these.</summary>
        public static readonly Hook<NWH.Common.FloatingOrigin.FloatingOrigin> OriginJump = new Hook<NWH.Common.FloatingOrigin.FloatingOrigin>("FloatingOrigin.Jump", typeof(P_OriginJump), Kinds.Prefix | Kinds.Postfix, true);
        /// <summary>Apocaspawner Catalog.Entries postfix (optional third-party target; the instance is the returned list).</summary>
        public static readonly Hook<object> SpawnerCatalogEntries = new Hook<object>("Apocaspawner.Catalog.Entries", typeof(P_SpawnerCatalogEntries), Kinds.Postfix);

        // ---- new in W0 (README 4.1; subscribers move here in Wave A/B) ----
        /// <summary>SetAudioClip.OnEnter (Guns tracer sounds postfix, PlayerBody female pain prefix+finalizer).</summary>
        public static readonly Hook<SetAudioClip> SetAudioClipEnter = new Hook<SetAudioClip>("SetAudioClip.OnEnter", typeof(P_SetAudioClipEnter), Kinds.Prefix | Kinds.Postfix | Kinds.Finalizer);
        /// <summary>UiTextSetText.OnEnter (Guns damage text prefix+finalizer, CleanHUD message postfix).</summary>
        public static readonly Hook<UiTextSetText> UiTextSetTextEnter = new Hook<UiTextSetText>("UiTextSetText.OnEnter", typeof(P_UiTextSetTextEnter), Kinds.Prefix | Kinds.Postfix | Kinds.Finalizer);
        /// <summary>ActionHelpers.DoMousePick (Optimization PM7 skip-prefix, Presentation PickAssist postfix).</summary>
        public static readonly RefHook<MousePickArgs> DoMousePick = new RefHook<MousePickArgs>("ActionHelpers.DoMousePick", typeof(P_DoMousePick), Kinds.Prefix | Kinds.Postfix);
        /// <summary>ActionHelpers.GetGameObjectFsm(GameObject,string) (Core QuietFsm).</summary>
        public static readonly RefHook<GetFsmArgs> GetGameObjectFsm = new RefHook<GetFsmArgs>("ActionHelpers.GetGameObjectFsm", typeof(P_GetGameObjectFsm), Kinds.Prefix | Kinds.Postfix);
        /// <summary>PlayMakerFSM.Awake postfix (Optimization FsmEdits + FS1).</summary>
        public static readonly Hook<PlayMakerFSM> FsmAwake = new Hook<PlayMakerFSM>("PlayMakerFSM.Awake", typeof(P_FsmAwake), Kinds.Postfix);
        /// <summary>Fsm.OnEnable prefix (Optimization FsmRegistry + FS1).</summary>
        public static readonly Hook<Fsm> FsmInnerEnable = new Hook<Fsm>("Fsm.OnEnable", typeof(P_FsmInnerEnable), Kinds.Prefix);
        /// <summary>PlayMakerFSM.OnDisable prefix (Optimization FsmRegistry skip-copy + GuiLoop).</summary>
        public static readonly Hook<PlayMakerFSM> FsmDisable = new Hook<PlayMakerFSM>("PlayMakerFSM.OnDisable", typeof(P_FsmDisable), Kinds.Prefix);
        /// <summary>PlayMakerFSM.OnDestroy prefix (Optimization FsmRegistry skip-copy + GuiLoop).</summary>
        public static readonly Hook<PlayMakerFSM> FsmDestroy = new Hook<PlayMakerFSM>("PlayMakerFSM.OnDestroy", typeof(P_FsmDestroy), Kinds.Prefix);
        /// <summary>Fsm.OnTriggerEnter/Stay/Exit and OnCollisionEnter/Stay/Exit prefixes (Optimization TriggerProxy skip-copy + FS1 wake).</summary>
        public static readonly RefHook<FsmPhysicsArgs> FsmPhysicsCallback = new RefHook<FsmPhysicsArgs>("Fsm.OnTrigger*/OnCollision*", typeof(P_FsmPhysicsCallback), Kinds.Prefix);
        /// <summary>Explosion.ShouldApplyForce (Patrols War Rig shelter postfix; Survival shelter registry in Wave B).</summary>
        public static readonly RefHook<ExplosionForceArgs> ExplosionShouldApplyForce = new RefHook<ExplosionForceArgs>("Explosion.ShouldApplyForce", typeof(P_ExplosionShouldApplyForce), Kinds.Prefix | Kinds.Postfix);

        // ------------------------------------------------------------------ table operations

        /// <summary>Removes every handler of <paramref name="owner"/> from every dispatcher (ModuleHost.Cleanup). Table-driven, so no
        /// dispatcher can be forgotten (README 9: FsmEnable handlers survived shutdown).</summary>
        public static void RemoveAll(IModule owner)
        {
            for (int i = 0; i < _all.Count; i++) _all[i].Remove(owner);
        }

        /// <summary>
        /// Applies the dispatcher patch of every hook that has at least one subscriber (or is always patched) and is not patched yet
        /// (each separately, in try/catch). Unused targets are never patched, so a disabled feature costs nothing. Called by ModuleHost after Init.
        /// </summary>
        public static void ApplyUsed()
        {
            for (int i = 0; i < _all.Count; i++)
            {
                var h = _all[i];
                if (h.Applied || (!h.Always && h.SubscriberCount == 0)) continue;
                if (Patches.ApplyClass(null, h.PatchClass) > 0) h.Applied = true;
            }
        }

        /// <summary>"hooks: Target[pre/post] ..." for every subscribed dispatcher (startup summary, F7 Harmony page).</summary>
        public static string Summary()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("hooks: ");
            for (int i = 0; i < _all.Count; i++)
            {
                var h = _all[i];
                int pre = h.PrefixCount, post = h.PostfixCount + h.FinalizerCount;
                if (pre == 0 && post == 0) continue;
                sb.Append(h.Target).Append(h.Applied ? "" : "(NOT PATCHED)").Append('[').Append(pre).Append('/').Append(post).Append("] ");
            }
            return sb.ToString();
        }

        /// <summary>Fills <paramref name="into"/> (not cleared) with the stats of every handler of every dispatcher (Debug).</summary>
        public static void Stats(List<HandlerStat> into) { for (int i = 0; i < _all.Count; i++) _all[i].Stats(into); }

        // ---- the actual Harmony patch classes: one per target, each a thin forwarder ----
        [HarmonyPatch]
        static class P_SpawnerCatalogEntries
        {
            /// <summary>Apocaspawner.Catalog.Entries (optional).</summary>
            static MethodBase TargetMethod() { return Patches.Method("Apocaspawner.Catalog", "Entries"); }
            /// <summary>Forwards to the dispatcher.</summary>
            static void Postfix(object __result) { SpawnerCatalogEntries.RunPostfix(__result); }
        }
        [HarmonyPatch(typeof(CreateObject), "OnEnter")] static class P_CreateObjectEnter { static bool Prefix(CreateObject __instance) { return CreateObjectEnter.RunPrefix(__instance); } static void Postfix(CreateObject __instance) { CreateObjectEnter.RunPostfix(__instance); } }
        [HarmonyPatch(typeof(SendEvent), "OnEnter")] static class P_SendEventEnter { static bool Prefix(SendEvent __instance) { return SendEventEnter.RunPrefix(__instance); } static void Postfix(SendEvent __instance) { SendEventEnter.RunPostfix(__instance); } }
        [HarmonyPatch(typeof(SendEvent), "OnUpdate")] static class P_SendEventUpdate { static bool Prefix(SendEvent __instance) { return SendEventUpdate.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(SetFsmFloat), "OnEnter")] static class P_SetFsmFloatEnter { static bool Prefix(SetFsmFloat __instance) { return SetFsmFloatEnter.RunPrefix(__instance); } static void Postfix(SetFsmFloat __instance) { SetFsmFloatEnter.RunPostfix(__instance); } }
        [HarmonyPatch(typeof(SetFsmFloat), "DoSetFsmFloat")] static class P_SetFsmFloatDo { static bool Prefix(SetFsmFloat __instance) { return SetFsmFloatDo.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(SetVelocity), "DoSetVelocity")] static class P_SetVelocityDo { static bool Prefix(SetVelocity __instance) { return SetVelocityDo.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(MouseLook), "DoMouseLook")] static class P_MouseLookDo { static bool Prefix(MouseLook __instance) { return MouseLookDo.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(Wait), "OnUpdate")] static class P_WaitUpdate { static bool Prefix(Wait __instance) { return WaitUpdate.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(GetButtonDown), "OnUpdate")] static class P_GetButtonDownUpdate { static bool Prefix(GetButtonDown __instance) { return GetButtonDownUpdate.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(GetButton), "DoGetButton")] static class P_GetButtonDo { static bool Prefix(GetButton __instance) { return GetButtonDo.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(GetButtonUp), "OnUpdate")] static class P_GetButtonUpUpdate { static bool Prefix(GetButtonUp __instance) { return GetButtonUpUpdate.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(GetMouseButtonDown), "OnUpdate")] static class P_GetMouseButtonDownUpdate { static bool Prefix(GetMouseButtonDown __instance) { return GetMouseButtonDownUpdate.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(Raycast), "OnEnter")] static class P_RaycastEnter { static bool Prefix(Raycast __instance) { return RaycastEnter.RunPrefix(__instance); } static void Postfix(Raycast __instance) { RaycastEnter.RunPostfix(__instance); } }
        [HarmonyPatch(typeof(Raycast), "DoRaycast")] static class P_RaycastDo { static bool Prefix(Raycast __instance) { return RaycastDo.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(SmoothLookAt), "DoSmoothLookAt")] static class P_SmoothLookAtDo { static bool Prefix(SmoothLookAt __instance) { return SmoothLookAtDo.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(ActivateGameObject), "OnEnter")] static class P_ActivateGameObjectEnter { static bool Prefix(ActivateGameObject __instance) { return ActivateGameObjectEnter.RunPrefix(__instance); } static void Postfix(ActivateGameObject __instance) { ActivateGameObjectEnter.RunPostfix(__instance); } }
        [HarmonyPatch(typeof(ActivateGameObject), "DoActivateGameObject")] static class P_ActivateGameObjectDo { static bool Prefix(ActivateGameObject __instance) { return ActivateGameObjectDo.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(SetPosition), "DoSetPosition")] static class P_SetPositionDo { static bool Prefix(SetPosition __instance) { return SetPositionDo.RunPrefix(__instance); } static void Postfix(SetPosition __instance) { SetPositionDo.RunPostfix(__instance); } }
        [HarmonyPatch(typeof(SensorGetDetectionRayHit), "OnEnter")] static class P_SensorRayHitEnter { static bool Prefix(SensorGetDetectionRayHit __instance) { return SensorRayHitEnter.RunPrefix(__instance); } static Exception Finalizer(SensorGetDetectionRayHit __instance, Exception __exception) { return SensorRayHitEnter.RunFinalizer(__instance, __exception); } }
        [HarmonyPatch(typeof(SensorGetLineOfSightResult), "OnEnter3D")] static class P_LosEnter3D { static bool Prefix(SensorGetLineOfSightResult __instance) { return LosEnter3D.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(SensorGetLineOfSightResult), "OnUpdate3D")] static class P_LosUpdate3D { static bool Prefix(SensorGetLineOfSightResult __instance) { return LosUpdate3D.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(SensorGetDetections), "DoAction")] static class P_SensorDetectionsDo { static bool Prefix(SensorGetDetections __instance) { return SensorDetectionsDo.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(PlayMakerFSM), "OnEnable")] static class P_FsmEnable { static void Postfix(PlayMakerFSM __instance) { FsmEnable.RunPostfix(__instance); } }
        [HarmonyPatch(typeof(Fsm), "ProcessEvent")] static class P_ProcessEvent { static bool Prefix(Fsm __instance, FsmEvent fsmEvent, FsmEventData eventData) { return ProcessEvent.Run(__instance, fsmEvent, eventData); } }
        [HarmonyPatch(typeof(Fsm), "EnterState")] static class P_EnterState { static bool Prefix(Fsm __instance, FsmState state) { return EnterState.RunPrefix(__instance, state); } static void Postfix(Fsm __instance, FsmState state) { EnterState.RunPostfix(__instance, state); } }
        [HarmonyPatch(typeof(ES3Internal.ES3ReferenceMgrBase), "GetPrefab", typeof(long), typeof(bool))]
        /// <summary>Forwards ES3ReferenceMgrBase.GetPrefab to the resolver chain (original runs when no resolver answers).</summary>
        static class P_GetPrefab { static bool Prefix(long id, ref ES3Internal.ES3Prefab __result) { if (GetPrefab.PrefixCount == 0) return true; var p = GetPrefab.Run(id); if (p == null) return true; __result = p; return false; } }
        [HarmonyPatch(typeof(NWH.Common.FloatingOrigin.FloatingOrigin), "Jump")]
        /// <summary>Forwards FloatingOrigin.Jump to Origin and the dispatcher.</summary>
        static class P_OriginJump { static bool Prefix(NWH.Common.FloatingOrigin.FloatingOrigin __instance) { Origin.BeforeJump(__instance); return OriginJump.RunPrefix(__instance); } static void Postfix(NWH.Common.FloatingOrigin.FloatingOrigin __instance) { OriginJump.RunPostfix(__instance); Origin.AfterJump(__instance); } }

        [HarmonyPatch(typeof(SetAudioClip), "OnEnter")]
        static class P_SetAudioClipEnter
        {
            /// <summary>Forwards to the dispatcher prefixes.</summary>
            static bool Prefix(SetAudioClip __instance) { return SetAudioClipEnter.RunPrefix(__instance); }
            /// <summary>Forwards to the dispatcher postfixes.</summary>
            static void Postfix(SetAudioClip __instance) { SetAudioClipEnter.RunPostfix(__instance); }
            /// <summary>Forwards to the dispatcher finalizers.</summary>
            static Exception Finalizer(SetAudioClip __instance, Exception __exception) { return SetAudioClipEnter.RunFinalizer(__instance, __exception); }
        }

        [HarmonyPatch(typeof(UiTextSetText), "OnEnter")]
        static class P_UiTextSetTextEnter
        {
            /// <summary>Forwards to the dispatcher prefixes.</summary>
            static bool Prefix(UiTextSetText __instance) { return UiTextSetTextEnter.RunPrefix(__instance); }
            /// <summary>Forwards to the dispatcher postfixes.</summary>
            static void Postfix(UiTextSetText __instance) { UiTextSetTextEnter.RunPostfix(__instance); }
            /// <summary>Forwards to the dispatcher finalizers.</summary>
            static Exception Finalizer(UiTextSetText __instance, Exception __exception) { return UiTextSetTextEnter.RunFinalizer(__instance, __exception); }
        }

        [HarmonyPatch(typeof(ActionHelpers), "DoMousePick")]
        static class P_DoMousePick
        {
            /// <summary>Forwards (distance, layerMask) to the dispatcher prefixes.</summary>
            static bool Prefix(float __0, int __1) { var a = new MousePickArgs { Distance = __0, LayerMask = __1 }; return DoMousePick.RunPrefix(ref a); }
            /// <summary>Forwards (distance, layerMask) to the dispatcher postfixes.</summary>
            static void Postfix(float __0, int __1) { var a = new MousePickArgs { Distance = __0, LayerMask = __1 }; DoMousePick.RunPostfix(ref a); }
        }

        [HarmonyPatch]
        static class P_GetGameObjectFsm
        {
            /// <summary>ActionHelpers.GetGameObjectFsm(GameObject, string).</summary>
            static MethodBase TargetMethod() { return AccessTools.Method(typeof(ActionHelpers), "GetGameObjectFsm", new[] { typeof(GameObject), typeof(string) }); }
            /// <summary>Forwards to the prefixes; a skipping handler provides the result.</summary>
            static bool Prefix(GameObject __0, string __1, ref PlayMakerFSM __result)
            {
                var a = new GetFsmArgs { Go = __0, FsmName = __1, Result = __result };
                if (GetGameObjectFsm.RunPrefix(ref a)) return true;
                __result = a.Result; return false;
            }
            /// <summary>Forwards to the postfixes, which may replace the result.</summary>
            static void Postfix(GameObject __0, string __1, ref PlayMakerFSM __result)
            {
                if (GetGameObjectFsm.PostfixCount == 0) return;
                var a = new GetFsmArgs { Go = __0, FsmName = __1, Result = __result };
                GetGameObjectFsm.RunPostfix(ref a); __result = a.Result;
            }
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Awake")] static class P_FsmAwake { static void Postfix(PlayMakerFSM __instance) { FsmAwake.RunPostfix(__instance); } }
        [HarmonyPatch(typeof(Fsm), "OnEnable")] static class P_FsmInnerEnable { static bool Prefix(Fsm __instance) { return FsmInnerEnable.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(PlayMakerFSM), "OnDisable")] static class P_FsmDisable { static bool Prefix(PlayMakerFSM __instance) { return FsmDisable.RunPrefix(__instance); } }
        [HarmonyPatch(typeof(PlayMakerFSM), "OnDestroy")] static class P_FsmDestroy { static bool Prefix(PlayMakerFSM __instance) { return FsmDestroy.RunPrefix(__instance); } }

        [HarmonyPatch]
        static class P_FsmPhysicsCallback
        {
            /// <summary>Builds the args and runs the prefixes.</summary>
            static bool Run(Fsm fsm, FsmPhysicsKind kind, Collider other, Collision collision)
            {
                var a = new FsmPhysicsArgs { Fsm = fsm, Kind = kind, Collider = other, Collision = collision };
                return FsmPhysicsCallback.RunPrefix(ref a);
            }
            /// <summary>Fsm.OnTriggerEnter(Collider).</summary>
            [HarmonyPatch(typeof(Fsm), "OnTriggerEnter"), HarmonyPrefix] static bool TriggerEnter(Fsm __instance, Collider __0) { return Run(__instance, FsmPhysicsKind.TriggerEnter, __0, null); }
            /// <summary>Fsm.OnTriggerStay(Collider).</summary>
            [HarmonyPatch(typeof(Fsm), "OnTriggerStay"), HarmonyPrefix] static bool TriggerStay(Fsm __instance, Collider __0) { return Run(__instance, FsmPhysicsKind.TriggerStay, __0, null); }
            /// <summary>Fsm.OnTriggerExit(Collider).</summary>
            [HarmonyPatch(typeof(Fsm), "OnTriggerExit"), HarmonyPrefix] static bool TriggerExit(Fsm __instance, Collider __0) { return Run(__instance, FsmPhysicsKind.TriggerExit, __0, null); }
            /// <summary>Fsm.OnCollisionEnter(Collision).</summary>
            [HarmonyPatch(typeof(Fsm), "OnCollisionEnter"), HarmonyPrefix] static bool CollisionEnter(Fsm __instance, Collision __0) { return Run(__instance, FsmPhysicsKind.CollisionEnter, null, __0); }
            /// <summary>Fsm.OnCollisionStay(Collision).</summary>
            [HarmonyPatch(typeof(Fsm), "OnCollisionStay"), HarmonyPrefix] static bool CollisionStay(Fsm __instance, Collision __0) { return Run(__instance, FsmPhysicsKind.CollisionStay, null, __0); }
            /// <summary>Fsm.OnCollisionExit(Collision).</summary>
            [HarmonyPatch(typeof(Fsm), "OnCollisionExit"), HarmonyPrefix] static bool CollisionExit(Fsm __instance, Collision __0) { return Run(__instance, FsmPhysicsKind.CollisionExit, null, __0); }
        }

        [HarmonyPatch]
        static class P_ExplosionShouldApplyForce
        {
            /// <summary>Explosion.ShouldApplyForce(GameObject) (private).</summary>
            static MethodBase TargetMethod() { return AccessTools.Method(typeof(Explosion), "ShouldApplyForce"); }
            /// <summary>Forwards to the prefixes; a skipping handler provides the result.</summary>
            static bool Prefix(Explosion __instance, GameObject __0, ref bool __result)
            {
                var a = new ExplosionForceArgs { Explosion = __instance, Target = __0, Result = __result };
                if (ExplosionShouldApplyForce.RunPrefix(ref a)) return true;
                __result = a.Result; return false;
            }
            /// <summary>Forwards to the postfixes, which may replace the result.</summary>
            static void Postfix(Explosion __instance, GameObject __0, ref bool __result)
            {
                if (ExplosionShouldApplyForce.PostfixCount == 0) return;
                var a = new ExplosionForceArgs { Explosion = __instance, Target = __0, Result = __result };
                ExplosionShouldApplyForce.RunPostfix(ref a); __result = a.Result;
            }
        }
    }
}
