using System;
using System.Reflection;
using System.Reflection.Emit;
using MonoMod.Utils;
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
                var enabled = _plugin.GetType().GetField("Enabled", BindingFlags.NonPublic | BindingFlags.Static);
                var light = sky.GetMethod("Light", BindingFlags.Public | BindingFlags.Static);
                var dist = at.ReturnType.GetMethod("Dist");
                _light = Sampler(at, dist, light, enabled);
                if (Log.L1) Log.Main(CompatibilityModule.Instance, "CaveDarkness ambient field connected");
            }
            catch (Exception e) { Failed(e); }
        }

        /// <summary>Generates p => Enabled.Value ? (Caves.At(p) is { } c ? Sky.Light(0f, c.Dist(p)) : -1) : -1 (one DynamicMethod; replaces an
        /// interpreted Expression.Compile, README 8 #7). Throws when the optional surface has other shapes.</summary>
        private static Func<Vector3, float> Sampler(MethodInfo at, MethodInfo dist, MethodInfo light, FieldInfo enabled)
        {
            var lp = light != null ? light.GetParameters() : null;
            if (at == null || dist == null || light == null || enabled == null || at.ReturnType.IsValueType || light.ReturnType != typeof(float)
                || lp.Length != 2 || lp[0].ParameterType != typeof(float) || lp[1].ParameterType != dist.ReturnType || dist.GetParameters().Length != 1
                || dist.GetParameters()[0].ParameterType != typeof(Vector3) || at.GetParameters().Length != 1 || at.GetParameters()[0].ParameterType != typeof(Vector3))
                throw new MissingMemberException("unsupported CaveDarkness field surface");
            var value = enabled.FieldType.GetProperty("Value");
            if (value == null || value.PropertyType != typeof(bool)) throw new MissingMemberException("CaveDarkness Enabled entry");
            using (var dm = new DynamicMethodDefinition("WW_CaveAmbient", typeof(float), new[] { typeof(Vector3) }))
            {
                var il = dm.GetILGenerator(); var off = il.DefineLabel(); var c = il.DeclareLocal(at.ReturnType);
                il.Emit(OpCodes.Ldsfld, enabled); il.Emit(OpCodes.Callvirt, value.GetGetMethod()); il.Emit(OpCodes.Brfalse, off);
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, at); il.Emit(OpCodes.Stloc, c);
                il.Emit(OpCodes.Ldloc, c); il.Emit(OpCodes.Brfalse, off);
                il.Emit(OpCodes.Ldc_R4, 0f); il.Emit(OpCodes.Ldloc, c); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Callvirt, dist);
                il.Emit(OpCodes.Call, light); il.Emit(OpCodes.Ret);
                il.MarkLabel(off); il.Emit(OpCodes.Ldc_R4, -1f); il.Emit(OpCodes.Ret);
                return (Func<Vector3, float>)Delegate.CreateDelegate(typeof(Func<Vector3, float>), dm.Generate());
            }
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
            Log.Warn(CompatibilityModule.Instance, "CaveDarkness field unavailable; native geometry fallback retained: " + e.Message);
        }
    }
}
