using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Reads the optional native moving-object ambient field through one captured numeric delegate.</summary>
    internal sealed class CaveDarknessBridge : ICaveAmbient
    {
        private object _plugin;
        private Func<Vector3, float> _light;
        private float _nextFind;
        private bool _failed;


        /// <summary>Resolves the optional cave field on maintenance ticks; a missing mod retries and an incompatible surface fails once.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Refresh()
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
                if (Log.L1) Log.Main(null, "[Compatibility] CaveDarkness ambient field connected");
            }
            catch (Exception e) { Failed(e); }
        }

        /// <summary>Samples only the compiled field delegate, returning -1 for unavailable or failed optional sources.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public float At(Vector3 p)
        {
            if (_light == null) return -1f;
            try { float value = _light(p); return value < 0f || float.IsNaN(value) || float.IsInfinity(value) ? -1f : Mathf.Clamp01(value); }
            catch (Exception e) { Failed(e); return -1f; }
        }

        /// <summary>Releases failed optional native references and prevents repetitive failing resolution.</summary>
        private void Failed(Exception e)
        {
            _failed = true; _plugin = null; _light = null;
            Log.Warn("[Compatibility] CaveDarkness field unavailable; native geometry fallback retained: " + e.Message);
        }
    }
}
