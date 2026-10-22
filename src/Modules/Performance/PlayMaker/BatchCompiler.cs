using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// Generates many tiny accessor methods in ONE in-memory assembly with Mono.Cecil (shipped with BepInEx) and loads it.
    /// Why: the game's mscorlib is stripped (every DynamicMethod constructor is a Unity.ThrowStub, Expression.Compile is the
    /// LightCompiler interpreter), and MonoMod's DynamicMethodDefinition loads one assembly per method (~1 ms each), which is
    /// fine for tens of members but not for the thousands of PlayMaker action fields FS4 needs.
    /// The generated assembly carries [IgnoresAccessChecksTo] for every referenced assembly (the mechanism MonoMod uses), so
    /// private/internal constructors and fields are accessible like with reflection.
    /// Generated shapes (same IL as FastReflect's single-method builders):
    ///   object c{i}()                     newobj ctor; ret
    ///   void   s{j}(object obj, object v) ldarg.0; castclass Decl; ldarg.1; unbox.any FieldType; stfld f; ret
    /// </summary>
    internal sealed class BatchCompiler
    {
        readonly List<ConstructorInfo> _ctors = new List<ConstructorInfo>();
        readonly List<FieldInfo> _fields = new List<FieldInfo>();
        MethodInfo[] _ctorMethods, _fieldMethods;
        static int _serial;

        internal int AddCtor(ConstructorInfo c) { _ctors.Add(c); return _ctors.Count - 1; }
        internal int AddSetter(FieldInfo f) { _fields.Add(f); return _fields.Count - 1; }
        internal int CtorCount { get { return _ctors.Count; } }
        internal int SetterCount { get { return _fields.Count; } }

        internal void Build()
        {
            string name = "WW_Batch_" + (++_serial);
            var asm = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(name, new Version(1, 0, 0, 0)),
                name, new ModuleParameters { Kind = ModuleKind.Dll, Runtime = TargetRuntime.Net_4_0 });
            var mod = asm.MainModule;
            // [assembly: UnverifiableCode]: with it Mono skips member-visibility checks (MonoMod's generator sets it too)
            asm.CustomAttributes.Add(new CustomAttribute(mod.ImportReference(typeof(System.Security.UnverifiableCodeAttribute).GetConstructor(Type.EmptyTypes))));

            // [assembly: IgnoresAccessChecksTo("X")] with MonoMod's own attribute type, loaded through MonoMod's ReflectionHelper
            // (exactly how DMDCecilGenerator makes private members reachable for Harmony patches in this game)
            var attrType = typeof(MonoMod.Utils.ReflectionHelper).Assembly.GetType("System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute", true);
            var actor = mod.ImportReference(attrType.GetConstructor(new[] { typeof(string) }));
            var names = new HashSet<string>();
            foreach (var c in _ctors) names.Add(c.DeclaringType.Assembly.GetName().Name);
            foreach (var f in _fields) { names.Add(f.DeclaringType.Assembly.GetName().Name); names.Add(f.FieldType.Assembly.GetName().Name); }
            foreach (var n in names)
            {
                var ca = new CustomAttribute(actor);
                ca.ConstructorArguments.Add(new CustomAttributeArgument(mod.TypeSystem.String, n));
                asm.CustomAttributes.Add(ca);
            }

            var holder = new TypeDefinition("", "G", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Abstract
                | Mono.Cecil.TypeAttributes.Sealed | Mono.Cecil.TypeAttributes.Class, mod.TypeSystem.Object);
            mod.Types.Add(holder);
            const Mono.Cecil.MethodAttributes MA = Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig;

            for (int i = 0; i < _ctors.Count; i++)
            {
                var m = new MethodDefinition("c" + i, MA, mod.TypeSystem.Object);
                var il = m.Body.GetILProcessor();
                il.Emit(OpCodes.Newobj, mod.ImportReference(_ctors[i]));
                il.Emit(OpCodes.Ret);
                holder.Methods.Add(m);
            }
            for (int j = 0; j < _fields.Count; j++)
            {
                var f = _fields[j];
                var m = new MethodDefinition("s" + j, MA, mod.TypeSystem.Void);
                m.Parameters.Add(new ParameterDefinition("o", Mono.Cecil.ParameterAttributes.None, mod.TypeSystem.Object));
                m.Parameters.Add(new ParameterDefinition("v", Mono.Cecil.ParameterAttributes.None, mod.TypeSystem.Object));
                var il = m.Body.GetILProcessor();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, mod.ImportReference(f.DeclaringType));
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Unbox_Any, mod.ImportReference(f.FieldType));   // castclass for reference types
                il.Emit(OpCodes.Stfld, mod.ImportReference(f));
                il.Emit(OpCodes.Ret);
                holder.Methods.Add(m);
            }

            var loaded = MonoMod.Utils.ReflectionHelper.Load(mod);
            // Mono ignores IgnoresAccessChecksTo; MonoMod's DMD post-build marks the loaded assembly corlib-internal, which makes
            // Mono skip member-visibility checks for its code (DMDGenerator._Postbuild -> Extensions.SetMonoCorlibInternal).
            MonoMod.Utils.Extensions.SetMonoCorlibInternal(loaded, true);
            var g = loaded.GetType("G", true);
            _ctorMethods = new MethodInfo[_ctors.Count];
            _fieldMethods = new MethodInfo[_fields.Count];
            for (int i = 0; i < _ctors.Count; i++) _ctorMethods[i] = g.GetMethod("c" + i, BindingFlags.Public | BindingFlags.Static);
            for (int j = 0; j < _fields.Count; j++) _fieldMethods[j] = g.GetMethod("s" + j, BindingFlags.Public | BindingFlags.Static);
        }

        internal Func<object> Ctor(int i) { return (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), _ctorMethods[i]); }
        internal Action<object, object> Setter(int j) { return (Action<object, object>)Delegate.CreateDelegate(typeof(Action<object, object>), _fieldMethods[j]); }
    }
}
