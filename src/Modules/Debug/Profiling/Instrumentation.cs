using WildWasteland.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Patches = WildWasteland.Core.Patches;

namespace WildWasteland.Modules.Debug
{
    internal sealed class Scope
    {
        internal string Name;
        internal volatile bool Enabled, Trace;
        internal readonly List<MethodInfo> Methods = new List<MethodInfo>();
        internal readonly List<FieldInfo> Counters = new List<FieldInfo>();
        internal int Patched, Failed, Eligible;
    }
    internal sealed class Probe
    {
        internal Scope Scope; internal string Name;
        internal long Calls, Inclusive, Self, Maximum, Errors, Allocated, Main, Worker;
        internal long OldCalls, OldInclusive, OldSelf, OldErrors, OldAllocated, OldMain, OldWorker;
        internal long EpochCalls, EpochInclusive, EpochSelf, EpochErrors, EpochAllocated, EpochMain, EpochWorker;
    }
    internal sealed class MethodRow
    {
        internal string Scope, Name; internal double Calls, Inclusive, Self, Maximum, Allocated, Main, Worker; internal long Errors;
    }
    internal struct TraceEvent
    {
        internal string Method, Error; internal long Ticks; internal int Thread, Frame;
    }
    internal static class Instrumentation
    {
        internal static readonly List<Scope> Scopes = new List<Scope>();
        internal static string[] ScopeNames = new string[0];
        internal static readonly List<Probe> Probes = new List<Probe>();
        internal static readonly List<string> Failures = new List<string>();
        internal static readonly ConcurrentQueue<TraceEvent> Events = new ConcurrentQueue<TraceEvent>();
        private static readonly ConcurrentDictionary<MethodBase, Probe> Map = new ConcurrentDictionary<MethodBase, Probe>();
        private static readonly Dictionary<string, Probe> VistaFeatures = new Dictionary<string, Probe>(StringComparer.Ordinal);
        private static readonly Queue<MethodInfo> Pending = new Queue<MethodInfo>();
        private static readonly HashSet<MethodBase> Queued = new HashSet<MethodBase>();
        internal static int MainThread, Frame, Dropped;
        internal static string Fault;
        private static int _eventCount;
        internal static volatile bool Timing, Allocations, Spikes, Lifecycle;
        internal static double Threshold = 2;
        /// <summary>Number of optional methods waiting for bounded installation.</summary>
        internal static int PendingCount { get { return Pending.Count; } }
        private static readonly Func<long> ReadAllocation = AllocationReader();
        internal static bool AllocationSupported { get { return ReadAllocation != null; } }
        /// <summary>Tracks child method ticks for one thread-local inclusive/self timing frame.</summary>
        private struct StackEntry { internal long Children; }
        [ThreadStatic] private static StackEntry[] _stack;
        [ThreadStatic] private static int _depth;
        /// <summary>Carries one in-flight probe invocation without altering its original return or exception behavior.</summary>
        internal struct Token { internal Probe Probe; internal long Start, Allocation; internal int Depth, Thread; }

        /// <summary>Discovers managed WW probe candidates once, excluding Debug itself and the already-timed scheduler.</summary>
        internal static void Initialize()
        {
            MainThread = Thread.CurrentThread.ManagedThreadId;
            Scopes.Clear(); Probes.Clear(); Failures.Clear(); Map.Clear(); VistaFeatures.Clear(); Pending.Clear(); Queued.Clear(); Fault = null;
            var byName = new Dictionary<string, Scope>(StringComparer.Ordinal);
            Type[] types;
            try { types = typeof(WildWasteland.Core.Scheduler).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; foreach (var error in e.LoaderExceptions) Failures.Add("Type discovery: " + error.Message); }
            foreach (var type in types)
            {
                if (type == null) continue;
                string ns = type.Namespace ?? "";
                if (!ns.StartsWith("WildWasteland.", StringComparison.Ordinal) || ns.StartsWith("WildWasteland.Modules.Debug", StringComparison.Ordinal) || type.ContainsGenericParameters) continue;
                bool generated = type.IsDefined(typeof(CompilerGeneratedAttribute), false);
                string scopeName = ns.StartsWith("WildWasteland.Modules.", StringComparison.Ordinal) ? ns.Substring(22).Split('.')[0] : "Core";
                Scope scope; if (!byName.TryGetValue(scopeName, out scope)) { scope = new Scope { Name = scopeName }; byName.Add(scopeName, scope); Scopes.Add(scope); }
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (method.IsAbstract || method.ContainsGenericParameters || method.IsSpecialName || method.IsDefined(typeof(CompilerGeneratedAttribute), false) || method.GetMethodBody() == null) continue;
                    if (generated && method.Name != "MoveNext") continue;
                    if (type == typeof(WildWasteland.Core.Scheduler)) continue; // existing scheduler timings are read without patching the scheduler
                    scope.Methods.Add(method); if (Boundary(method)) scope.Eligible++;
                }
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (typeof(ICollection).IsAssignableFrom(field.FieldType)) scope.Counters.Add(field);
            }
            Scopes.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            ScopeNames = new string[Scopes.Count]; for (int i = 0; i < Scopes.Count; i++) ScopeNames[i] = Scopes[i].Name;
        }
        /// <summary>Selects named execution boundaries for lower-overhead optional instrumentation.</summary>
        private static bool Boundary(MethodInfo method)
        {
            string n = method.Name;
            if (VistaRun(method)) return true;
            return n.StartsWith("Before", StringComparison.Ordinal) || n.StartsWith("After", StringComparison.Ordinal) || n.StartsWith("On", StringComparison.Ordinal)
                || n.StartsWith("Build", StringComparison.Ordinal) || n.StartsWith("Load", StringComparison.Ordinal) || n.StartsWith("Save", StringComparison.Ordinal)
                || n.StartsWith("Draw", StringComparison.Ordinal) || n.StartsWith("Refresh", StringComparison.Ordinal)
                || n == "Tick" || n == "LateTick" || n == "FixedTick" || n == "Frame" || n == "Update" || n == "LateUpdate" || n == "FixedUpdate"
                || n == "Path" || n == "TryNext" || n == "Walk" || n == "SteerDir" || n == "Rescan" || n == "Step" || n == "Prepare" || n == "Applied"
                || n == "Finalized" || n == "Waypoint" || n == "Creating" || n == "Created" || n == "Hearing" || n == "Assign" || n == "Look"
                || n == "Noise" || n == "FoughtFor" || n == "Enter" || n == "SpawnEvent" || n == "Glide" || n == "Shift" || n == "Read" || n == "Write"
                || n == "GetAsync" || n == "Init" || n == "Shutdown" || n == "RunPrefix" || n == "RunPostfix" || n == "Start" || n == "Stop"
                || n == "MoveNext" || n == "Generate" || n == "Apply" || n == "ProcessEvent" || n == "EnterState" || n.StartsWith("SendEvent", StringComparison.Ordinal);
        }
        private static bool VistaRun(MethodBase method)
        { return method.Name == "Run" && method.DeclaringType.FullName == "ApocalpyterWorldAmp.WorldAmpDriver"; }
        /// <summary>Discovers optional managed base-game/FSM/terrain/save and Vista candidates only on explicit request.</summary>
        internal static void DiscoverNative()
        {
            if (Scopes.Exists(s => s.Name.StartsWith("Game.", StringComparison.Ordinal))) return;
            var groups = new Dictionary<string, Scope>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                bool vista = name == "ApocalpyterWorldAmp";
                if (!vista && name != "Assembly-CSharp" && name != "PlayMaker" && name != "MapMagic" && !name.StartsWith("ES3", StringComparison.Ordinal)) continue;
                Type[] types;
                try { types = assembly.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types; } catch (Exception e) { Failures.Add(name + ": " + e.Message); continue; }
                foreach (var type in types)
                {
                    if (type == null || type.ContainsGenericParameters) continue;
                    string full = type.FullName ?? type.Name;
                    if (vista && !typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(type)) continue;
                    string group = vista ? "Mod.Vista" : name == "PlayMaker" || type.Name == "PlayMakerFSM" ? "Game.PlayMaker" : full.IndexOf("MapMagic", StringComparison.Ordinal) >= 0 ? "Game.Terrain" : full.StartsWith("ES3", StringComparison.Ordinal) ? "Game.Save" : full.StartsWith("HutongGames.PlayMaker.Actions.", StringComparison.Ordinal) ? "Game.Actions" : "Game.Scripts";
                    if (name == "PlayMaker" && type.Name != "Fsm" && type.Name != "FsmState" && type.Name != "FsmStateAction") continue;
                    Scope scope; if (!groups.TryGetValue(group, out scope)) { scope = new Scope { Name = group }; groups.Add(group, scope); }
                    try { foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (method.IsAbstract || method.IsSpecialName || method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
                        if (type.IsDefined(typeof(CompilerGeneratedAttribute), false) && method.Name != "MoveNext") continue;
                        scope.Methods.Add(method); if (Boundary(method)) scope.Eligible++;
                    } } catch (Exception e) { Failures.Add(full + ": " + e.Message); }
                }
            }
            Scopes.AddRange(groups.Values); Scopes.Sort((a,b) => string.CompareOrdinal(a.Name,b.Name));
            ScopeNames = Scopes.ConvertAll(s => s.Name).ToArray();
        }
        /// <summary>Queues uninstalled methods in the selected scope only at deep logging level.</summary>
        internal static void Enable(Scope scope, bool deep)
        {
            if (!Core.Log.L2) return;
            scope.Enabled = true;
            foreach (var method in scope.Methods) if ((deep || Boundary(method)) && !Map.ContainsKey(method) && Queued.Add(method)) Pending.Enqueue(method);
        }
        /// <summary>Installs at most one queued method probe per scheduler frame through Core patch ownership.</summary>
        internal static void InstallStep()
        {
            if (Pending.Count == 0) return;
            var method = Pending.Dequeue(); Queued.Remove(method);
            Scope scope = null; foreach (var s in Scopes) if (s.Methods.Contains(method)) { scope = s; break; }
            if (scope == null || !scope.Enabled) return;
            var probe = new Probe { Scope = scope, Name = method.DeclaringType.FullName + "." + method.ToString() };
            Map[method] = probe;
            try
            {
                if (!Patches.Patch(DebugModule.Instance, method, prefix: Patches.Method(typeof(Instrumentation), VistaRun(method) ? nameof(BeginVista) : nameof(Begin)), finalizer: Patches.Method(typeof(Instrumentation), nameof(End)))) throw new InvalidOperationException("Core rejected the diagnostic probe; see patch records.");
                Probes.Add(probe); scope.Patched++;
            }
            catch (Exception e) { Probe ignored; Map.TryRemove(method, out ignored); scope.Failed++; Failures.Add(probe.Name + ": " + e.GetBaseException().Message); }
        }
        /// <summary>Separates Vista's two main-thread feature dispatchers by their original feature label.</summary>
        private static void BeginVista(MethodBase __originalMethod, string __0, out Token __state)
        {
            Begin(__originalMethod, out __state);
            if (__state.Probe == null) return;
            try
            {
                Probe feature;
                if (!VistaFeatures.TryGetValue(__0, out feature))
                {
                    feature = new Probe { Scope = __state.Probe.Scope, Name = "WorldAmpDriver.Run: " + __0 };
                    VistaFeatures.Add(__0, feature); Probes.Add(feature);
                }
                __state.Probe = feature;
            }
            catch (Exception e) { Fault = e.GetType().Name + ": " + e.Message; Timing = false; }
        }
        /// <summary>Diagnostic Harmony prefix: returns immediately unless the selected probe is active and timing is enabled.</summary>
        private static void Begin(MethodBase __originalMethod, out Token __state)
        {
            __state = new Token();
            try
            {
            Probe probe;
            if (!Timing || !Map.TryGetValue(__originalMethod, out probe) || !probe.Scope.Enabled) return;
            if (_stack == null) _stack = new StackEntry[128];
            int depth = _depth++; if (depth < _stack.Length) _stack[depth].Children = 0;
            __state = new Token { Probe = probe, Thread = Thread.CurrentThread.ManagedThreadId, Depth = depth, Allocation = Allocations && ReadAllocation != null ? ReadAllocation() : -1, Start = Stopwatch.GetTimestamp() };
            }
            catch (Exception e) { Fault = e.GetType().Name + ": " + e.Message; Timing = false; _depth = 0; __state = new Token(); }
        }
        /// <summary>Diagnostic Harmony finalizer: records inclusive/self/thread/allocation costs and preserves the original exception.</summary>
        private static Exception End(Exception __exception, Token __state)
        {
            try
            {
            var probe = __state.Probe; if (probe == null) return __exception;
            long ticks = Stopwatch.GetTimestamp() - __state.Start;
            long self = __state.Depth < _stack.Length ? Math.Max(0, ticks - _stack[__state.Depth].Children) : 0;
            _depth = __state.Depth;
            if (_depth > 0 && _depth - 1 < _stack.Length) _stack[_depth - 1].Children += ticks;
            Interlocked.Increment(ref probe.Calls); Interlocked.Add(ref probe.Inclusive, ticks); Interlocked.Add(ref probe.Self, self);
            if (__state.Thread == MainThread) Interlocked.Add(ref probe.Main, self); else Interlocked.Add(ref probe.Worker, self);
            long old; do { old = Interlocked.Read(ref probe.Maximum); if (ticks <= old) break; } while (Interlocked.CompareExchange(ref probe.Maximum, ticks, old) != old);
            if (__state.Allocation >= 0) Interlocked.Add(ref probe.Allocated, Math.Max(0, ReadAllocation() - __state.Allocation));
            if (__exception != null) Interlocked.Increment(ref probe.Errors);
            if ((probe.Scope.Trace && (Spikes && ticks * 1000.0 / Stopwatch.Frequency >= Threshold || Lifecycle && IsLifecycle(probe.Name))) || __exception != null)
            {
                if (Interlocked.Increment(ref _eventCount) <= 2048)
                    Events.Enqueue(new TraceEvent { Method = probe.Name, Ticks = ticks, Thread = __state.Thread, Frame = Volatile.Read(ref Frame), Error = __exception == null ? null : __exception.GetType().FullName + ": " + __exception.Message });
                else { Interlocked.Decrement(ref _eventCount); Interlocked.Increment(ref Dropped); }
            }
            return __exception; // retain the original exception and native behavior
            }
            catch (Exception e) { Fault = e.GetType().Name + ": " + e.Message; Timing = false; _depth = 0; return __exception; }
        }
        /// <summary>Recognizes save/load/terrain/spawn method names for bounded deep-level lifecycle traces.</summary>
        private static bool IsLifecycle(string name)
        { return name.IndexOf("Save", StringComparison.Ordinal) >= 0 || name.IndexOf("Load", StringComparison.Ordinal) >= 0 || name.IndexOf("Shift", StringComparison.Ordinal) >= 0 || name.IndexOf("Finalized", StringComparison.Ordinal) >= 0 || name.IndexOf("Applied", StringComparison.Ordinal) >= 0 || name.IndexOf("SpawnEvent", StringComparison.Ordinal) >= 0 || name.IndexOf("WorldReady", StringComparison.Ordinal) >= 0; }
        /// <summary>Removes one bounded trace event and releases its queue accounting slot.</summary>
        internal static bool Dequeue(out TraceEvent ev) { if (!Events.TryDequeue(out ev)) return false; Interlocked.Decrement(ref _eventCount); return true; }
        /// <summary>Computes per-second method deltas using atomic counters while retaining lifetime and sample-window totals.</summary>
        internal static List<MethodRow> Snapshot(double seconds)
        {
            var rows = new List<MethodRow>(); double ms = 1000.0 / Stopwatch.Frequency;
            foreach (var p in Probes)
            {
                long calls = Interlocked.Read(ref p.Calls), inc = Interlocked.Read(ref p.Inclusive), self = Interlocked.Read(ref p.Self), errors = Interlocked.Read(ref p.Errors);
                long alloc = Interlocked.Read(ref p.Allocated), main = Interlocked.Read(ref p.Main), worker = Interlocked.Read(ref p.Worker);
                rows.Add(new MethodRow { Scope = p.Scope.Name, Name = p.Name, Calls = (calls - p.OldCalls) / seconds, Inclusive = (inc - p.OldInclusive) * ms / seconds,
                    Self = (self - p.OldSelf) * ms / seconds, Maximum = Interlocked.Read(ref p.Maximum) * ms, Errors = errors - p.OldErrors,
                    Allocated = (alloc - p.OldAllocated) / seconds, Main = (main - p.OldMain) * ms / seconds, Worker = (worker - p.OldWorker) * ms / seconds });
                p.OldCalls = calls; p.OldInclusive = inc; p.OldSelf = self; p.OldErrors = errors; p.OldAllocated = alloc; p.OldMain = main; p.OldWorker = worker;
            }
            rows.Sort((a, b) => b.Self.CompareTo(a.Self)); return rows;
        }
        /// <summary>Binds the optional runtime allocation API and validates it without requiring unsupported runtimes to provide it.</summary>
        private static Func<long> AllocationReader()
        {
            var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Static | BindingFlags.Public);
            try { if (method == null) return null; var read = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method); read(); return read; } catch { return null; }
        }
        /// <summary>Stops new probes, removes installed diagnostic patches and clears probe queues while preserving in-flight finalizer references.</summary>
        internal static void Remove()
        {
            Timing = false; Pending.Clear(); Queued.Clear();
            var finalizer = Patches.Method(typeof(Instrumentation), nameof(End));
            foreach (var pair in Map)
            {
                var prefix = Patches.Method(typeof(Instrumentation), VistaRun(pair.Key) ? nameof(BeginVista) : nameof(Begin));
                bool prefixRemoved = Patches.UnpatchMethod(DebugModule.Instance, pair.Key, prefix);
                bool finalizerRemoved = Patches.UnpatchMethod(DebugModule.Instance, pair.Key, finalizer);
                if (!prefixRemoved || !finalizerRemoved)
                {
                    Fault = "Could not remove diagnostic probe: " + pair.Value.Name;
                    Failures.Add(Fault);
                    continue;
                }
                Probe ignored; Map.TryRemove(pair.Key, out ignored);
                Probes.Remove(pair.Value);
                if (pair.Value.Scope.Patched > 0) pair.Value.Scope.Patched--;
            }
            foreach (var feature in VistaFeatures.Values) Probes.Remove(feature);
            VistaFeatures.Clear();
            foreach (var scope in Scopes) scope.Enabled = false;
            if (Map.Count == 0) Fault = null;
            // In-flight finalizers retain their Probe references safely. New hooks no longer start.

        }
    }
}
