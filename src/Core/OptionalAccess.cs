using System;
using System.Reflection;
using System.Reflection.Emit;
using MonoMod.Utils;

namespace WildWasteland.Core
{
    // Optional-plugin accessors use Cecil: this game's Expression.Compile is interpreted.
    internal static class OptionalAccess
    {
        internal static Func<object, T> Read<T>(FieldInfo field)
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
        internal static T Call<T>(MethodInfo target, Type result, params Type[] arguments) where T : class
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
    }
}
