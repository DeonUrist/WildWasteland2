using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// FS4 (plan §4, research A §1.6 / R9): every Instantiate of an object with FSMs deserializes all their actions
    /// (PlayMakerFSM.OnAfterDeserialize -> Fsm.InitData -> ActionData.LoadActions -> CreateAction -> LoadActionField per field,
    /// ActionData.cs:631-1116) with FieldInfo.SetValue per field and Activator.CreateInstance per nested object.
    /// A prefix replaces the private ActionData.LoadActionField with an exact copy (<see cref="ActionDataPort"/>) whose only
    /// differences are <see cref="SetField"/> and <see cref="Create"/>: generated setters/constructors from ONE background-built
    /// assembly (<see cref="BatchCompiler"/>; this runtime has no Reflection.Emit), used only when reflection would neither
    /// convert, default nor throw; reflection otherwise, before the batch is ready, and whenever FS4 is off.
    /// 0.1.26c transpiled the method in place instead, which broke field loading in game; nothing is transpiled now.
    /// Thread-safe: OnAfterDeserialize can run on Unity's loading thread; caches are locked.
    /// </summary>
    public sealed class FastActionLoadFeature : Feature
    {
        public override string PlanId { get { return "FS4"; } }
        public override string Key { get { return "Fast action loading"; } }
        public override string Title { get { return "Faster FSM loading on spawn"; } }
        public override string Category { get { return Categories.PlayMaker; } }
        public override int Order { get { return 14; } }
        public override string Help { get { return "Spawning objects with FSMs (vehicles, items, camps) creates their actions with compiled constructors and field setters instead of reflection. Same values."; } }

        internal static bool On;
        public static long Created, FieldsSet, Fallbacks;

        protected override void OnEnable(FeatureMode mode)
        {
            // 0.1.26c transpiled ActionData.LoadActionField in place, which broke field loading in game ("ActionData: Missing
            // LoadActionField for type: System.Boolean"). Now a prefix runs an exact copy of the method (ActionDataPort).
            ActionDataPort.Check();
            if (!EnsurePatched(typeof(Patch))) throw new Exception("ActionData.LoadActionField prefix failed");
            StartBatch();
            On = true;
        }
        protected override void OnDisable() { On = false; }
        static bool _logged;
        public override void OnSceneLoaded()
        {
            if (_logged || _batchState < 2) return;
            _logged = true;
            if (_batchState == 2) WildWasteland.Core.Log.Always("[Performance] FS4: compiled " + _batchCtors + " constructors and " + _batchSetters + " field setters in " + _batchMs + " ms (background)");
            else WildWasteland.Core.Log.Warn("[Performance] FS4: batch compile failed, reflection stays (" + _batchError + ")");
        }
        public override void DrawStats(ToolLayout l)
        {
            l.Value("Compiled accessors", _batchState == 2 ? _batchCtors + " constructors, " + _batchSetters + " field setters (" + _batchMs + " ms, background)" : _batchState == 1 ? "compiling..." : _batchState == 3 ? "failed: " + _batchError : "-");
            l.Value("Actions created / fields set / reflection fallbacks", Created + " / " + FieldsSet + " / " + Fallbacks);
        }

        // ---------------------------------------------------------------- one batch for every action type
        // The game's runtime cannot emit methods (stripped mscorlib); one MonoMod DMD per member would load thousands of
        // assemblies. Instead every PlayMaker action type, its fields and the field types LoadActionField instantiates are
        // compiled into ONE assembly (BatchCompiler) on a background thread; until it is ready (and for types it does not
        // know, e.g. actions of mods loaded later) the original reflection calls run, so results never differ.
        static volatile int _batchState;   // 0 none, 1 compiling, 2 ready, 3 failed
        static string _batchError; static long _batchMs; static int _batchCtors, _batchSetters;
        static BatchCompiler _batch;
        static readonly Dictionary<Type, int> _ctorIndex = new Dictionary<Type, int>();
        static readonly Dictionary<IntPtr, int> _fieldIndex = new Dictionary<IntPtr, int>();

        static void StartBatch()
        {
            if (_batchState != 0) return;
            _batchState = 1;
            var asms = AppDomain.CurrentDomain.GetAssemblies();   // main thread: snapshot of the loaded assemblies
            var t = new System.Threading.Thread(() => BuildBatch(asms)) { IsBackground = true, Name = "WW FS4 compiler", Priority = System.Threading.ThreadPriority.BelowNormal };
            t.Start();
        }

        static void BuildBatch(System.Reflection.Assembly[] asms)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var b = new BatchCompiler();
                var ctorIdx = new Dictionary<Type, int>(); var fieldIdx = new Dictionary<IntPtr, int>();
                var seen = new HashSet<Type>(); var queue = new Queue<Type>();
                var actionBase = typeof(FsmStateAction);
                foreach (var a in asms)
                {
                    Type[] types;
                    try { types = a.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { types = e.Types; }
                    catch (Exception) { continue; }
                    if (types == null) continue;
                    foreach (var t in types)
                    {
                        try { if (t != null && !t.IsAbstract && actionBase.IsAssignableFrom(t) && seen.Add(t)) queue.Enqueue(t); }
                        catch (Exception) { }   // a type whose dependencies do not load: never instantiated by PlayMaker either
                    }
                }
                while (queue.Count > 0)
                {
                    var t = queue.Dequeue();
                    try { AddType(b, t, ctorIdx, fieldIdx, seen, queue); }
                    catch (Exception) { }   // left to reflection
                }
                b.Build();
                lock (_ctors) { foreach (var kv in ctorIdx) _ctorIndex[kv.Key] = kv.Value; }
                lock (_setters) { foreach (var kv in fieldIdx) _fieldIndex[kv.Key] = kv.Value; }
                _batch = b; _batchCtors = b.CtorCount; _batchSetters = b.SetterCount; _batchMs = sw.ElapsedMilliseconds;
                _batchState = 2;
            }
            catch (Exception e)
            {
                _batchError = e.GetType().Name + ": " + e.Message; _batchState = 3;   // shown on the Performance tab (no logging off the main thread)
            }
        }

        static void AddType(BatchCompiler b, Type t, Dictionary<Type, int> ctorIdx, Dictionary<IntPtr, int> fieldIdx, HashSet<Type> seen, Queue<Type> queue)
        {
            if (Ctorable(t)) { var c = t.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null); if (c != null) ctorIdx[t] = b.AddCtor(c); }
            if (t.IsValueType) return;   // FastReflect.FieldSetter excludes struct-declared fields too
            for (var bt = t; bt != null && bt != typeof(object); bt = bt.BaseType)
            {
                foreach (var f in bt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!FastReflect.CanSet(f) || f.DeclaringType.IsValueType) continue;
                    var ft = f.FieldType;
                    if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(Nullable<>)) continue;
                    var key = f.FieldHandle.Value;
                    if (!fieldIdx.ContainsKey(key)) fieldIdx[key] = b.AddSetter(f);
                    // types LoadActionField instantiates for this field (the field type, array element types)
                    var et = ft; while (et.IsArray) et = et.GetElementType();
                    if (Ctorable(et) && !typeof(UnityEngine.Object).IsAssignableFrom(et) && seen.Add(et)) queue.Enqueue(et);
                }
            }
        }

        static bool Ctorable(Type t)
        {
            return t != null && t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters && !t.IsArray && !t.IsCOMObject
                && !typeof(Delegate).IsAssignableFrom(t) && t != typeof(string)
                && t.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null) != null;
        }

        // ---------------------------------------------------------------- constructors

        static readonly Dictionary<Type, Func<object>> _ctors = new Dictionary<Type, Func<object>>();

        /// <summary>Drop-in for Activator.CreateInstance(Type).</summary>
        public static object Create(Type type)
        {
            if (!On || type == null) return Activator.CreateInstance(type);
            Func<object> c;
            // ActionData.LoadActions also runs from PlayMakerFSM.OnAfterDeserialize, which Unity may call on the loading thread
            lock (_ctors)
            {
                if (!_ctors.TryGetValue(type, out c))
                {
                    if (_batchState != 2) { Fallbacks++; return Activator.CreateInstance(type); }   // not compiled yet: reflection
                    int i;
                    c = _ctorIndex.TryGetValue(type, out i) ? _batch.Ctor(i) : null;
                    _ctors[type] = c;
                }
            }
            if (c == null) { Fallbacks++; return Activator.CreateInstance(type); }
            Created++;
            try { return c(); }
            catch (Exception e) { throw new TargetInvocationException(e); }
        }

        // ---------------------------------------------------------------- field setters

        sealed class Setter
        {
            public Action<object, object> Set; public Type Decl, FieldType; public bool ValueType; public Type OkObj, OkVal;
        }
        static readonly Dictionary<IntPtr, Setter> _setters = new Dictionary<IntPtr, Setter>();
        static readonly Setter NoSetter = new Setter();

        /// <summary>Drop-in for FieldInfo.SetValue(object obj, object value) (same stack shape: instance, obj, value).</summary>
        public static void SetField(FieldInfo field, object obj, object value)
        {
            if (!On || field == null) { field.SetValue(obj, value); return; }
            IntPtr key = field.FieldHandle.Value;
            Setter s;
            lock (_setters)   // see Create: may run on the loading thread
            {
                if (!_setters.TryGetValue(key, out s))
                {
                    if (_batchState != 2) { Fallbacks++; field.SetValue(obj, value); return; }   // not compiled yet: reflection
                    int j;
                    var set = _fieldIndex.TryGetValue(key, out j) ? _batch.Setter(j) : null;
                    s = set == null ? NoSetter : new Setter { Set = set, Decl = field.DeclaringType, FieldType = field.FieldType, ValueType = field.FieldType.IsValueType };
                    _setters[key] = s;
                }
            }
            if (s.Set == null || !Exact(s, obj, value)) { Fallbacks++; field.SetValue(obj, value); return; }
            try { s.Set(obj, value); FieldsSet++; }
            catch (Exception)
            {
                // castclass/unbox.any/stfld cannot fail after Exact(): an exception here means the generated method itself did not
                // load (type load / JIT); drop it and let reflection do this and every later set of the field
                s.Set = null; Fallbacks++; field.SetValue(obj, value);
            }
        }

        static bool Exact(Setter s, object obj, object value)
        {
            if (obj == null) return false;
            var ot = obj.GetType();
            if (!ReferenceEquals(ot, s.OkObj))
            {
                if (!s.Decl.IsAssignableFrom(ot)) return false;
                s.OkObj = ot;
            }
            if (value == null) return !s.ValueType;              // reflection writes default(T) for null into a struct field
            var vt = value.GetType();
            if (ReferenceEquals(vt, s.FieldType) || ReferenceEquals(vt, s.OkVal)) return true;
            if (s.ValueType || !s.FieldType.IsAssignableFrom(vt)) return false;   // conversions: reflection's job
            s.OkVal = vt;
            return true;
        }

        // ---------------------------------------------------------------- patch

        [HarmonyPatch]
        static class Patch
        {
            // Always replaces the original once installed (the copy uses plain reflection while FS4 is off).
            [HarmonyPatch(typeof(ActionData), "LoadActionField"), HarmonyPrefix]
            static bool Pre(ActionData __instance, Fsm fsm, object obj, FieldInfo field, int paramIndex)
            {
                ActionDataPort.Load(__instance, fsm, obj, field, paramIndex);
                return false;
            }
        }
    }
}
