using System;
using System.Reflection;

namespace WildWasteland.Core
{
    /// <summary>
    /// Former optional-plugin accessor generator, merged into Core <see cref="Access"/> (W0-B, verbatim). Kept as a forwarder
    /// while Quests, QuestsCompat and CleanHUD still call it (their Wave A/B owners switch to Access; Wave C deletes this file).
    /// </summary>
    internal static class OptionalAccess
    {
        /// <summary>Forwards to <see cref="Access.Read{T}(FieldInfo)"/> (compiled field getter).</summary>
        [Obsolete("refactor: use Core Access.Read")]
        internal static Func<object, T> Read<T>(FieldInfo field) { return Access.Read<T>(field); }

        /// <summary>Forwards to <see cref="Access.Call{T}(MethodInfo, Type, Type[])"/> (compiled call with the given signature).</summary>
        [Obsolete("refactor: use Core Access.Call")]
        internal static T Call<T>(MethodInfo target, Type result, params Type[] arguments) where T : class { return Access.Call<T>(target, result, arguments); }
    }
}
