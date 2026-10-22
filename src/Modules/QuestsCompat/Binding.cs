using System;
using HarmonyLib;
using WildWasteland.Core;

namespace WildWasteland.Modules.QuestsCompat
{
    internal static class Binding
    {
        internal static Func<object, T> Get<T>(Type t, string name)
        {
            var field = AccessTools.Field(t, name);
            if (field != null) return OptionalAccess.Read<T>(field);
            return OptionalAccess.Call<Func<object, T>>(AccessTools.Property(t, name).GetGetMethod(true), typeof(T), typeof(object));
        }
        internal static Action<object> Call(Type t, string name)
        {
            return OptionalAccess.Call<Action<object>>(AccessTools.Method(t, name), typeof(void), typeof(object));
        }
    }
}
