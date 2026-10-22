using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// Compiled (DynamicMethod) replacements for the reflection calls PlayMaker makes per action/frame (FS2 FsmProperty,
    /// FS3 CallMethod, FS4 action loading). Each builder returns null when the member cannot be compiled with exactly the
    /// reflection semantics (static, indexed, init-only, generic, by-ref ...); callers then keep using reflection.
    /// Semantics kept from Mono reflection:
    /// - property accessors and invoked methods wrap any exception in TargetInvocationException (PropertyInfo.Get/SetValue,
    ///   MethodBase.Invoke do that); plain field access does not;
    /// - a member of a struct reached through a boxed value is read/written IN the box (unbox = pointer into the box), the
    ///   same as FieldInfo/PropertyInfo.SetValue on a boxed struct;
    /// - virtual properties/methods are dispatched virtually (callvirt), as reflection does.
    /// Argument/value type checks (reflection converts or throws) are done by the callers, which fall back to reflection when
    /// a value is not exactly an instance of the member type. No Unity types here (unit-testable on plain mono).
    /// </summary>
    internal static class FastReflect
    {
        static readonly ConstructorInfo TieCtor = typeof(TargetInvocationException).GetConstructor(new[] { typeof(Exception) });

        // The game's mscorlib is stripped: every System.Reflection.Emit.DynamicMethod constructor is a Unity.ThrowStub
        // (PlatformNotSupportedException), and Expression.Compile is the slow LightCompiler interpreter. MonoMod's
        // DynamicMethodDefinition (what HarmonyX uses to build patches in this game) generates through Cecil and loads the
        // result, so it works; each Generate() loads one small assembly, hence the per-feature compile budgets.
        internal sealed class Dm
        {
            readonly MonoMod.Utils.DynamicMethodDefinition _d;
            readonly Mono.Cecil.MethodDefinition _def;   // batch mode: a method of the current Batch's assembly
            readonly ILGenerator _il;
            internal Dm(string name, Type ret, Type[] args)
            {
                var b = _batch;
                if (b != null) { _def = b.Define(name, ret, args); _il = new MonoMod.Utils.Cil.CecilILGenerator(_def.Body.GetILProcessor()).GetProxy(); }
                else _d = new MonoMod.Utils.DynamicMethodDefinition(name, ret, args);
            }
            internal ILGenerator GetILGenerator() { return _il ?? _d.GetILGenerator(); }
            /// <summary>Single mode: generates now (one assembly). Batch mode: returns null; the delegate is handed to the sink
            /// of the current <see cref="Batch.Add{T}"/> after <see cref="Batch.Build"/>.</summary>
            internal Delegate CreateDelegate(Type t)
            {
                if (_def != null) { _batch.Register(_def, t); return null; }
                var mi = _d.Generate();
                Compiled++;
                return Delegate.CreateDelegate(t, mi);
            }
        }
        /// <summary>Methods generated one by one so far (each is a loaded assembly).</summary>
        internal static int Compiled;

        /// <summary>Upper bound of one-by-one generated methods; past it the builders return null (callers keep reflection).
        /// Batches are not limited (one assembly each).</summary>
        internal const int Budget = 400;
        static Dm New(string name, Type ret, Type[] args)
        {
            if (_batch == null && Compiled >= Budget) throw new InvalidOperationException("FastReflect budget used");
            return new Dm("WW_" + name, ret, args);
        }

        // ---------------------------------------------------------------- batches
        [ThreadStatic] static Batch _batch;

        /// <summary>
        /// Many builders -> ONE in-memory assembly. Usage (any thread): var b = new Batch(); b.Add(() => BoxedGetter(m),
        /// d => acc.Get = d); ... b.Build(); - every sink is called once (with null when that member could not be
        /// compiled or the build failed). The builders emit through MonoMod's CecilILGenerator (same IL as single mode).
        /// </summary>
        internal sealed class Batch
        {
            readonly Mono.Cecil.AssemblyDefinition _asm;
            readonly Mono.Cecil.ModuleDefinition _mod;
            readonly Mono.Cecil.TypeDefinition _holder;
            readonly List<Mono.Cecil.MethodDefinition> _defs = new List<Mono.Cecil.MethodDefinition>();
            readonly List<Type> _types = new List<Type>();
            readonly List<Action<Delegate>> _sinks = new List<Action<Delegate>>();
            readonly List<Action<Delegate>> _all = new List<Action<Delegate>>();
            Action<Delegate> _sink;
            Mono.Cecil.MethodDefinition _open;   // defined but not (yet) registered
            static int _serial;
            internal int Count { get { return _defs.Count; } }

            internal Batch()
            {
                string name = "WW_FastReflect_" + System.Threading.Interlocked.Increment(ref _serial);
                _asm = Mono.Cecil.AssemblyDefinition.CreateAssembly(new Mono.Cecil.AssemblyNameDefinition(name, new Version(1, 0, 0, 0)), name,
                    new Mono.Cecil.ModuleParameters { Kind = Mono.Cecil.ModuleKind.Dll, Runtime = Mono.Cecil.TargetRuntime.Net_4_0 });
                _mod = _asm.MainModule;
                _asm.CustomAttributes.Add(new Mono.Cecil.CustomAttribute(_mod.ImportReference(typeof(System.Security.UnverifiableCodeAttribute).GetConstructor(Type.EmptyTypes))));
                _holder = new Mono.Cecil.TypeDefinition("", "G", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Abstract
                    | Mono.Cecil.TypeAttributes.Sealed | Mono.Cecil.TypeAttributes.Class, _mod.TypeSystem.Object);
                _mod.Types.Add(_holder);
            }

            internal Mono.Cecil.MethodDefinition Define(string name, Type ret, Type[] args)
            {
                var m = new Mono.Cecil.MethodDefinition(name + "_" + _defs.Count, Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static
                    | Mono.Cecil.MethodAttributes.HideBySig, _mod.ImportReference(ret));
                for (int i = 0; i < args.Length; i++) m.Parameters.Add(new Mono.Cecil.ParameterDefinition("a" + i, Mono.Cecil.ParameterAttributes.None, _mod.ImportReference(args[i])));
                _holder.Methods.Add(m);   // CecilILGenerator imports through the method's module: attach before emitting
                _open = m;
                return m;
            }

            internal void Register(Mono.Cecil.MethodDefinition def, Type delegateType)
            {
                _open = null; _defs.Add(def); _types.Add(delegateType); _sinks.Add(_sink);
                _sink = null;   // one method per Add
            }

            /// <summary>Runs <paramref name="build"/> in batch mode; <paramref name="assign"/> receives the delegate after Build.</summary>
            internal void Add<T>(Func<T> build, Action<T> assign) where T : class
            {
                Action<Delegate> sink = d => assign(d as T);
                var prev = _batch;
                _batch = this; _sink = sink;
                try { build(); }
                catch (Exception) { }
                finally
                {
                    _batch = prev;
                    if (_open != null) { _holder.Methods.Remove(_open); _open = null; }   // builder gave up mid-way
                    if (_sink != null) { _sink = null; _all.Add(sink); }   // builder returned null before emitting: sink gets null
                }
            }

            /// <summary>Writes and loads the assembly, then calls every sink. Never throws.</summary>
            internal void Build()
            {
                Delegate[] made = new Delegate[_defs.Count];
                if (_defs.Count > 0)
                {
                    try
                    {
                        var loaded = MonoMod.Utils.ReflectionHelper.Load(_mod);
                        MonoMod.Utils.Extensions.SetMonoCorlibInternal(loaded, true);   // like DMD: skip visibility checks
                        var g = loaded.GetType("G", true);
                        for (int i = 0; i < _defs.Count; i++)
                        {
                            try { made[i] = Delegate.CreateDelegate(_types[i], g.GetMethod(_defs[i].Name, BindingFlags.Public | BindingFlags.Static)); }
                            catch (Exception) { made[i] = null; }
                        }
                    }
                    catch (Exception) { }
                }
                for (int i = 0; i < _sinks.Count; i++) { try { if (_sinks[i] != null) _sinks[i](made[i]); } catch (Exception) { } }
                for (int i = 0; i < _all.Count; i++) { try { _all[i](null); } catch (Exception) { } }
            }
        }

        static bool Bad(Type t) { return t == null || t.IsByRef || t.IsPointer || t.ContainsGenericParameters; }

        /// <summary>Can this field/property be read through a compiled getter with reflection semantics?</summary>
        internal static bool CanGet(MemberInfo m)
        {
            var f = m as FieldInfo;
            if (f != null) return !f.IsStatic && !f.IsLiteral && !Bad(f.DeclaringType) && !Bad(f.FieldType);
            var p = m as PropertyInfo;
            if (p == null || Bad(p.DeclaringType) || Bad(p.PropertyType) || p.GetIndexParameters().Length != 0) return false;
            var g = p.GetGetMethod(true);
            return g != null && !g.IsStatic && !g.ContainsGenericParameters;
        }

        /// <summary>Can this field/property be written through a compiled setter with reflection semantics?</summary>
        internal static bool CanSet(MemberInfo m)
        {
            var f = m as FieldInfo;
            if (f != null) return !f.IsStatic && !f.IsLiteral && !f.IsInitOnly && !Bad(f.DeclaringType) && !Bad(f.FieldType);
            var p = m as PropertyInfo;
            if (p == null || Bad(p.DeclaringType) || Bad(p.PropertyType) || p.GetIndexParameters().Length != 0) return false;
            var s = p.GetSetMethod(true);
            return s != null && !s.IsStatic && !s.ContainsGenericParameters;
        }

        internal static Type MemberType(MemberInfo m)
        {
            var f = m as FieldInfo;
            return f != null ? f.FieldType : ((PropertyInfo)m).PropertyType;
        }

        static void LoadTarget(ILGenerator il, Type decl)
        {
            il.Emit(OpCodes.Ldarg_0);
            if (decl.IsValueType) il.Emit(OpCodes.Unbox, decl);   // pointer into the box (writes land in the box)
            else il.Emit(OpCodes.Castclass, decl);
        }

        static void CallAccessor(ILGenerator il, Type decl, MethodInfo acc)
        {
            il.Emit(decl.IsValueType || !acc.IsVirtual ? OpCodes.Call : OpCodes.Callvirt, acc);
        }

        static void BeginWrap(ILGenerator il, bool wrap) { if (wrap) il.BeginExceptionBlock(); }
        static void EndWrap(ILGenerator il, bool wrap)
        {
            if (!wrap) return;
            il.BeginCatchBlock(typeof(Exception));
            il.Emit(OpCodes.Newobj, TieCtor);
            il.Emit(OpCodes.Throw);
            il.EndExceptionBlock();
        }

        /// <summary>object -> member value (boxed when the member is a value type). Null if not compilable.</summary>
        internal static Func<object, object> BoxedGetter(MemberInfo m)
        {
            if (!CanGet(m)) return null;
            try
            {
                Type decl = m.DeclaringType, mt = MemberType(m);
                bool prop = m is PropertyInfo;
                var dm = New("get_" + m.Name, typeof(object), new[] { typeof(object) });
                var il = dm.GetILGenerator();
                var res = il.DeclareLocal(typeof(object));
                BeginWrap(il, prop);
                LoadTarget(il, decl);
                if (prop) CallAccessor(il, decl, ((PropertyInfo)m).GetGetMethod(true)); else il.Emit(OpCodes.Ldfld, (FieldInfo)m);
                if (mt.IsValueType) il.Emit(OpCodes.Box, mt);
                il.Emit(OpCodes.Stloc, res);
                EndWrap(il, prop);
                il.Emit(OpCodes.Ldloc, res);
                il.Emit(OpCodes.Ret);
                return (Func<object, object>)dm.CreateDelegate(typeof(Func<object, object>));
            }
            catch (Exception) { return null; }
        }

        /// <summary>object -> T, only when the member type is exactly T (no boxing). Null otherwise.</summary>
        internal static Func<object, T> TypedGetter<T>(MemberInfo m)
        {
            if (!CanGet(m) || MemberType(m) != typeof(T)) return null;
            try
            {
                Type decl = m.DeclaringType;
                bool prop = m is PropertyInfo;
                var dm = New("getT_" + m.Name, typeof(T), new[] { typeof(object) });
                var il = dm.GetILGenerator();
                var res = il.DeclareLocal(typeof(T));
                BeginWrap(il, prop);
                LoadTarget(il, decl);
                if (prop) CallAccessor(il, decl, ((PropertyInfo)m).GetGetMethod(true)); else il.Emit(OpCodes.Ldfld, (FieldInfo)m);
                il.Emit(OpCodes.Stloc, res);
                EndWrap(il, prop);
                il.Emit(OpCodes.Ldloc, res);
                il.Emit(OpCodes.Ret);
                return (Func<object, T>)dm.CreateDelegate(typeof(Func<object, T>));
            }
            catch (Exception) { return null; }
        }

        /// <summary>(object target, object value): value is unboxed/cast to the member type (callers check the type first).</summary>
        internal static Action<object, object> BoxedSetter(MemberInfo m)
        {
            if (!CanSet(m)) return null;
            try
            {
                Type decl = m.DeclaringType, mt = MemberType(m);
                bool prop = m is PropertyInfo;
                var dm = New("set_" + m.Name, typeof(void), new[] { typeof(object), typeof(object) });
                var il = dm.GetILGenerator();
                BeginWrap(il, prop);
                LoadTarget(il, decl);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Unbox_Any, mt);   // castclass for reference types
                if (prop) CallAccessor(il, decl, ((PropertyInfo)m).GetSetMethod(true)); else il.Emit(OpCodes.Stfld, (FieldInfo)m);
                EndWrap(il, prop);
                il.Emit(OpCodes.Ret);
                return (Action<object, object>)dm.CreateDelegate(typeof(Action<object, object>));
            }
            catch (Exception) { return null; }
        }

        /// <summary>(object target, T value), only when the member type is exactly T. Null otherwise.</summary>
        internal static Action<object, T> TypedSetter<T>(MemberInfo m)
        {
            if (!CanSet(m) || MemberType(m) != typeof(T)) return null;
            try
            {
                Type decl = m.DeclaringType;
                bool prop = m is PropertyInfo;
                var dm = New("setT_" + m.Name, typeof(void), new[] { typeof(object), typeof(T) });
                var il = dm.GetILGenerator();
                BeginWrap(il, prop);
                LoadTarget(il, decl);
                il.Emit(OpCodes.Ldarg_1);
                if (prop) CallAccessor(il, decl, ((PropertyInfo)m).GetSetMethod(true)); else il.Emit(OpCodes.Stfld, (FieldInfo)m);
                EndWrap(il, prop);
                il.Emit(OpCodes.Ret);
                return (Action<object, T>)dm.CreateDelegate(typeof(Action<object, T>));
            }
            catch (Exception) { return null; }
        }

        /// <summary>Public parameterless constructor of a non-abstract class (what Activator.CreateInstance(Type) calls).
        /// Exceptions are NOT wrapped here (the caller wraps them like Activator does).</summary>
        internal static Func<object> Constructor(Type t)
        {
            if (t == null || !t.IsClass || t.IsAbstract || t.ContainsGenericParameters || t.IsArray || t.IsCOMObject
                || typeof(Delegate).IsAssignableFrom(t) || t == typeof(string)) return null;
            var ctor = t.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (ctor == null) return null;
            try
            {
                var dm = New("new_" + t.Name, typeof(object), Type.EmptyTypes);
                var il = dm.GetILGenerator();
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Ret);
                return (Func<object>)dm.CreateDelegate(typeof(Func<object>));
            }
            catch (Exception) { return null; }
        }

        /// <summary>(target, args) -> boxed result or null for void; exceptions wrapped in TargetInvocationException like
        /// MethodBase.Invoke. Null when the method needs reflection's special handling (by-ref/out, generic, varargs,
        /// struct instance method).</summary>
        internal static Func<object, object[], object> Invoker(MethodInfo mi)
        {
            if (mi == null || mi.ContainsGenericParameters || mi.CallingConvention == CallingConventions.VarArgs) return null;
            var decl = mi.DeclaringType;
            if (decl == null || (!mi.IsStatic && decl.IsValueType) || decl.ContainsGenericParameters) return null;
            if (mi.ReturnType.IsByRef || mi.ReturnType.IsPointer) return null;
            var ps = mi.GetParameters();
            for (int i = 0; i < ps.Length; i++) if (Bad(ps[i].ParameterType)) return null;
            try
            {
                var dm = New("call_" + mi.Name, typeof(object), new[] { typeof(object), typeof(object[]) });
                var il = dm.GetILGenerator();
                var res = il.DeclareLocal(typeof(object));
                il.BeginExceptionBlock();
                if (!mi.IsStatic) { il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, decl); }
                for (int i = 0; i < ps.Length; i++)
                {
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldelem_Ref);
                    il.Emit(OpCodes.Unbox_Any, ps[i].ParameterType);
                }
                il.Emit(!mi.IsStatic && mi.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, mi);
                if (mi.ReturnType == typeof(void)) il.Emit(OpCodes.Ldnull);
                else if (mi.ReturnType.IsValueType) il.Emit(OpCodes.Box, mi.ReturnType);
                il.Emit(OpCodes.Stloc, res);
                il.BeginCatchBlock(typeof(Exception));
                il.Emit(OpCodes.Newobj, TieCtor);
                il.Emit(OpCodes.Throw);
                il.EndExceptionBlock();
                il.Emit(OpCodes.Ldloc, res);
                il.Emit(OpCodes.Ret);
                return (Func<object, object[], object>)dm.CreateDelegate(typeof(Func<object, object[], object>));
            }
            catch (Exception) { return null; }
        }

        /// <summary>Typed reader of a (private) instance or static field: mcs cannot consume Harmony's ref-returning FieldRefs as
        /// values, so the module reads private PlayMaker fields through these. Throws when the field does not exist.</summary>
        internal static Func<TOwner, TValue> Getter<TOwner, TValue>(Type type, string field)
        {
            var f = type.GetField(field, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null || !typeof(TValue).IsAssignableFrom(f.FieldType)) throw new MissingFieldException(type.FullName, field);
            var dm = New("rd_" + field, typeof(TValue), new[] { typeof(TOwner) });
            var il = dm.GetILGenerator();
            if (f.IsStatic) il.Emit(OpCodes.Ldsfld, f);
            else
            {
                il.Emit(OpCodes.Ldarg_0);
                if (typeof(TOwner) != f.DeclaringType) il.Emit(OpCodes.Castclass, f.DeclaringType);
                il.Emit(OpCodes.Ldfld, f);
            }
            if (f.FieldType.IsValueType && !typeof(TValue).IsValueType) il.Emit(OpCodes.Box, f.FieldType);
            il.Emit(OpCodes.Ret);
            return (Func<TOwner, TValue>)dm.CreateDelegate(typeof(Func<TOwner, TValue>));
        }

        /// <summary>Typed writer of a (private) instance or static field (see <see cref="Getter{TOwner,TValue}"/>).</summary>
        internal static Action<TOwner, TValue> Setter<TOwner, TValue>(Type type, string field)
        {
            var f = type.GetField(field, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null || f.FieldType != typeof(TValue)) throw new MissingFieldException(type.FullName, field);
            var dm = New("wr_" + field, typeof(void), new[] { typeof(TOwner), typeof(TValue) });
            var il = dm.GetILGenerator();
            if (f.IsStatic) { il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Stsfld, f); }
            else
            {
                il.Emit(OpCodes.Ldarg_0);
                if (typeof(TOwner) != f.DeclaringType) il.Emit(OpCodes.Castclass, f.DeclaringType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Stfld, f);
            }
            il.Emit(OpCodes.Ret);
            return (Action<TOwner, TValue>)dm.CreateDelegate(typeof(Action<TOwner, TValue>));
        }

        /// <summary>Plain field setter for FS4 (fields only; no exception wrapping, like FieldInfo.SetValue).</summary>
        internal static Action<object, object> FieldSetter(FieldInfo f)
        {
            if (f == null || !CanSet(f) || f.DeclaringType.IsValueType) return null;
            if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(Nullable<>)) return null;
            return BoxedSetter(f);
        }
    }
}
