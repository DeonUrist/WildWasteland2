using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MonoMod.Utils;

namespace WildWasteland.Core
{
    /// <summary>
    /// The one accessor generator of the mod (README 4.1 "Access"; AGENTS 7.3: the game's DynamicMethod is stripped and
    /// Expression.Compile is interpreted, so everything is generated through MonoMod DynamicMethodDefinition or batched Cecil).
    /// Facade over the former NativeAccess (Call/Get by name), OptionalAccess (Read(FieldInfo)/Call(MethodInfo, ...)),
    /// Performance FastReflect (BoxedGetter/TypedGetter/BoxedSetter/TypedSetter/Constructor/Invoker/Getter/Setter/FieldSetter,
    /// <see cref="Batch"/>), BatchCompiler (<see cref="FieldBatch"/>) and CompileQueue (<see cref="Queue"/>).
    /// Generate once (Init or first use), never per call; the delegates themselves are allocation-free.
    /// </summary>
    public static partial class Access
    {
        
        /// <summary>
        /// NativeAccess.Call: a typed delegate T for method name of type (AccessTools lookup by parameter types). For an
        /// instance method the delegate's first parameter is the target (object or the type); object-typed arguments are
        /// unboxed/cast. Generated once through MonoMod DynamicMethodDefinition (no budget). Throws when missing or incompatible.
        /// </summary>
        public static T Call<T>(Type type, string name, params Type[] parameters) where T : class
        {
            var method = AccessTools.Method(type, name, parameters);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            var invoke = typeof(T).GetMethod("Invoke");
            var args = invoke.GetParameters(); var types = new Type[args.Length];
            for (int i = 0; i < args.Length; i++) types[i] = args[i].ParameterType;
            var signature = method.GetParameters(); int instance = method.IsStatic ? 0 : 1;
            if (args.Length != signature.Length + instance || !Compatible(method.ReturnType, invoke.ReturnType)) throw new InvalidOperationException("Incompatible native method: " + type.FullName + "." + name);
            for (int i = 0; i < signature.Length; i++)
                if (types[i + instance] != typeof(object) && !Compatible(types[i + instance], signature[i].ParameterType)) throw new InvalidOperationException("Incompatible native argument: " + type.FullName + "." + name);
            using (var dm = new DynamicMethodDefinition("WW_Native_" + name, invoke.ReturnType, types))
            {
                var il = dm.GetILGenerator(); int offset = method.IsStatic ? 0 : 1;
                if (!method.IsStatic) { il.Emit(OpCodes.Ldarg_0); il.Emit(type.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, type); }
                var target = method.GetParameters();
                for (int i = 0; i < target.Length; i++)
                {
                    il.Emit(OpCodes.Ldarg, i + offset);
                    if (types[i + offset] == typeof(object) && target[i].ParameterType != typeof(object))
                        il.Emit(target[i].ParameterType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, target[i].ParameterType);
                }
                il.Emit(method.IsStatic ? OpCodes.Call : OpCodes.Callvirt, method);
                if (method.ReturnType != invoke.ReturnType && method.ReturnType.IsValueType && invoke.ReturnType == typeof(object)) il.Emit(OpCodes.Box, method.ReturnType);
                il.Emit(OpCodes.Ret);
                return Delegate.CreateDelegate(typeof(T), dm.Generate()) as T;
            }
        }
        /// <summary>Assignable, or an enum and its underlying type.</summary>
        static bool Compatible(Type from, Type to)
        { return from == to || to.IsAssignableFrom(from) || (from.IsEnum && Enum.GetUnderlyingType(from) == to) || (to.IsEnum && Enum.GetUnderlyingType(to) == from); }

        /// <summary>
        /// NativeAccess.Get: compiled getter of field or property name (instance or static; boxes value types when T is a
        /// reference type). Throws when the member is missing or its type is incompatible with T.
        /// </summary>
        public static Func<object, T> Get<T>(Type type, string name)
        {
            var field = AccessTools.Field(type, name);
            if (field == null)
            {
                var property = AccessTools.Property(type, name);
                if (property == null) throw new MissingMemberException(type.FullName, name);
                var getter = property.GetGetMethod(true);
                if (!getter.IsStatic) return Call<Func<object, T>>(type, getter.Name, Type.EmptyTypes);
                var get = Call<Func<T>>(type, getter.Name, Type.EmptyTypes);
                return ignored => get();
            }
            if (!Compatible(field.FieldType, typeof(T))) throw new InvalidOperationException("Incompatible native field: " + type.FullName + "." + name);
            using (var dm = new DynamicMethodDefinition("WW_Native_get_" + name, typeof(T), new[] { typeof(object) }))
            {
                var il = dm.GetILGenerator();
                if (!field.IsStatic) { il.Emit(OpCodes.Ldarg_0); il.Emit(field.DeclaringType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, field.DeclaringType); }
                il.Emit(field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, field);
                if (field.FieldType.IsValueType && !typeof(T).IsValueType) il.Emit(OpCodes.Box, field.FieldType);
                il.Emit(OpCodes.Ret);
                return (Func<object, T>)Delegate.CreateDelegate(typeof(Func<object, T>), dm.Generate());
            }
        }

        /// <summary>Core.Access.Read: compiled getter of field (castclass to the declaring type; castclass to T when it differs and T is not object).</summary>
        public static Func<object, T> Read<T>(FieldInfo field)
        {
            using (var method = new DynamicMethodDefinition("WW_Read_" + field.Name, typeof(T), new[] { typeof(object) }))
            {
                var il = method.GetILGenerator();
                if (!field.IsStatic) { il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, field.DeclaringType); }
                il.Emit(field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, field);
                if (field.FieldType.IsValueType && !typeof(T).IsValueType) il.Emit(OpCodes.Box, field.FieldType);
                if (field.FieldType != typeof(T) && typeof(T) != typeof(object)) il.Emit(OpCodes.Castclass, typeof(T));
                il.Emit(OpCodes.Ret);
                return (Func<object, T>)Delegate.CreateDelegate(typeof(Func<object, T>), method.Generate());
            }
        }
        /// <summary>
        /// Core.Access.Call: delegate T calling target with the given generated signature (result, arguments; arguments[0]
        /// is the target for instance methods and is cast to the declaring type; the rest are passed through unchanged).
        /// </summary>
        public static T Call<T>(MethodInfo target, Type result, params Type[] arguments) where T : class
        {
            using (var method = new DynamicMethodDefinition("WW_Call_" + target.Name, result, arguments))
            {
                var il = method.GetILGenerator();
                if (!target.IsStatic) { il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, target.DeclaringType); }
                for (int i = 1; i < arguments.Length; i++) il.Emit(OpCodes.Ldarg, (short)i);
                il.Emit(target.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, target); il.Emit(OpCodes.Ret);
                return Delegate.CreateDelegate(typeof(T), method.Generate()) as T;
            }
        }

        /// <summary>Compiled getter of instance field or property name (any visibility) as Func&lt;object, T&gt;, or null when missing; never throws.</summary>
        public static Func<object, T> TryGet<T>(Type type, string name)
        {
            try { return type != null ? Get<T>(type, name) : null; }
            catch (Exception) { return null; }
        }

        /// <summary>Like <see cref="Call{T}(Type, string, Type[])"/> but returns null instead of throwing.</summary>
        public static T TryCall<T>(Type type, string name, params Type[] parameters) where T : class
        {
            try { return type != null ? Call<T>(type, name, parameters) : null; }
            catch (Exception) { return null; }
        }
    }
}
