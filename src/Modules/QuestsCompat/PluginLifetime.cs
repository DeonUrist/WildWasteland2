using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using HarmonyLib;

namespace WildWasteland.Modules.QuestsCompat
{
    internal static class PluginLifetime
    {
        // The game can destroy BepInEx components on scene load. Their managed instances remain in PluginInfos,
        // and the scheduler retains their methods, just as WW's Core runner retains its plugin configuration.
        private static bool Equal(UnityEngine.Object a, UnityEngine.Object b)
        {
            if (a is BaseUnityPlugin || b is BaseUnityPlugin) return ReferenceEquals(a, b);
            return a == b;
        }
        private static bool NotEqual(UnityEngine.Object a, UnityEngine.Object b) { return !Equal(a, b); }
        internal static void Install(Core.IModule owner, Type type, string method)
        {
            if (!Core.Patches.Patch(owner, AccessTools.Method(type, method), transpiler: AccessTools.Method(typeof(PluginLifetime), "Transpile")))
                throw new InvalidOperationException("Plugin lifetime hook failed: " + method);
        }
        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> code)
        {
            foreach (var c in code)
            {
                var method = c.operand as MethodInfo;
                if (method != null && method.DeclaringType == typeof(UnityEngine.Object))
                {
                    if (method.Name == "op_Equality") { c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(PluginLifetime), "Equal"); }
                    else if (method.Name == "op_Inequality") { c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(PluginLifetime), "NotEqual"); }
                }
                yield return c;
            }
        }
    }
}
