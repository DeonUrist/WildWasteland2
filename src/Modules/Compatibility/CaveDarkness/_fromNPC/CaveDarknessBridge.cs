using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    // Read the same ambient multiplier CaveDarkness uses for moving objects; no dependency on its assembly.
    internal static class CaveDarknessBridge
    {
        private static object _plugin;
        private static Func<Vector3, float> _light;
        private static float _nextFind;
        private static bool _failed;

        internal static void Reset() { _plugin = null; _light = null; _nextFind = 0f; _failed = false; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Refresh()
        {
            if (_light != null || _failed || Time.unscaledTime < _nextFind) return;
            _nextFind = Time.unscaledTime + 5f;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.denis.apocalypter.cavedarkness", out info) || ReferenceEquals(info.Instance, null)) return;
            try
            {
                _plugin = info.Instance;
                var asm = _plugin.GetType().Assembly;
                var caves = asm.GetType("CaveDarkness.Caves");
                var sky = asm.GetType("CaveDarkness.Sky");
                var at = caves.GetMethod("At", BindingFlags.Public | BindingFlags.Static);
                var p = Expression.Parameter(typeof(Vector3), "p");
                var c = Expression.Variable(at.ReturnType, "c");
                var enabled = Expression.Field(null, _plugin.GetType().GetField("Enabled", BindingFlags.NonPublic | BindingFlags.Static));
                var sample = Expression.Call(sky.GetMethod("Light", BindingFlags.Public | BindingFlags.Static), Expression.Constant(0f), Expression.Call(c, at.ReturnType.GetMethod("Dist"), p));
                var read = Expression.Block(new[] { c }, Expression.Assign(c, Expression.Call(at, p)),
                    Expression.Condition(Expression.Equal(c, Expression.Constant(null, at.ReturnType)), Expression.Constant(-1f), sample));
                _light = Expression.Lambda<Func<Vector3, float>>(Expression.Condition(Expression.Property(enabled, "Value"), read, Expression.Constant(-1f)), p).Compile();
                NpcModule.Log.LogInfo("Spider senses: CaveDarkness ambient field connected");
            }
            catch (Exception e) { Failed(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static float At(Vector3 p)
        {
            if (_light == null) return -1f;
            try { return Mathf.Clamp(_light(p), -1f, 1f); }
            catch (Exception e) { Failed(e); return -1f; }
        }

        private static void Failed(Exception e)
        {
            _failed = true; _plugin = null; _light = null;
            NpcModule.Warn("Spider senses: CaveDarkness field unavailable; using cave geometry: " + e.Message);
        }
    }
}
