using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// FS3 (plan §4, research A §3 #25: CallMethod 107 uses). CallMethod.DoMethodCall (CallMethod.cs) calls
    /// cachedMethodInfo.Invoke(target, parametersArray) every time (reflection invoke + argument checks). A transpiler
    /// replaces exactly those two MethodBase.Invoke(object, object[]) calls with <see cref="Invoke"/>; everything else in the
    /// method (cache checks, parameter preparation through FsmVar, storeResult) is the original IL.
    /// <see cref="Invoke"/> = MethodBase.Invoke semantics: compiled delegate per MethodInfo (callvirt for virtual methods,
    /// unboxed arguments, boxed result, exceptions wrapped in TargetInvocationException); it falls back to the real
    /// MethodBase.Invoke whenever reflection would convert or reject something (argument count, null for a value-type
    /// parameter, argument not exactly an instance of the parameter type, target null or of another type, by-ref/out or
    /// generic methods), so conversions and exceptions stay reflection's own. Switch off = the helper calls MethodBase.Invoke.
    /// </summary>
    public sealed class FastCallMethodFeature : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "FS3"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Compiled CallMethod"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Compiled Call Method actions"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.PlayMaker; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 13; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Call Method actions call the target method through a compiled delegate instead of reflection. Same calls, same results."; } }

        internal static bool On;
        public static long Compiled, Reflected;

        sealed class Inv
        {
            public volatile Func<object, object[], object> Call;   // published last, by Access.Queue (spread over frames)
            public Type[] Params; public bool[] ValueParam; public bool Static; public Type Decl; public Type OkTarget;
        }
        static readonly Dictionary<MethodBase, Inv> _invokers = new Dictionary<MethodBase, Inv>(RefComparer<MethodBase>.Instance);
        static readonly Inv NoInvoker = new Inv();

        /// <summary>Activates this feature-owned replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            if (!EnsurePatched(typeof(Patch))) throw new Exception("CallMethod transpiler failed");
            Module_ = Module;
            On = true;
        }
        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable() { On = false; }
        /// <summary>Draws this feature-owned current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l) { l.Value("Compiled calls / reflection fallbacks", Compiled + " / " + Reflected + " (methods " + _invokers.Count + ")"); }

        static Inv Get(MethodBase m)
        {
            Inv inv;
            if (_invokers.TryGetValue(m, out inv)) return inv;
            var mi = m as MethodInfo;
            inv = NoInvoker;
            if (mi != null)
            {
                var ps = mi.GetParameters();
                inv = new Inv { Params = new Type[ps.Length], ValueParam = new bool[ps.Length], Static = mi.IsStatic, Decl = mi.DeclaringType };
                for (int i = 0; i < ps.Length; i++)
                {
                    var t = ps[i].ParameterType;
                    inv.Params[i] = t; inv.ValueParam[i] = t.IsValueType;
                    // Nullable<T>: reflection accepts boxed T and null; let reflection handle those
                    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) { inv = NoInvoker; break; }
                }
                if (inv != NoInvoker)
                {
                    // compiled later (Access.Queue); Call stays null -> reflection until then
                    var target = inv;
                    Access.Queue.Enqueue(Module_, b => b.Add(() => Access.Invoker(mi), d => target.Call = d), null);
                }
            }
            if (_invokers.Count > 4096) _invokers.Clear();
            _invokers[m] = inv;
            return inv;
        }
        static WildWasteland.Core.IModule Module_;

        /// <summary>Drop-in for MethodBase.Invoke(object obj, object[] parameters) (same stack shape: instance, obj, args).</summary>
        public static object Invoke(MethodBase method, object obj, object[] parameters)
        {
            if (!On || method == null) return method.Invoke(obj, parameters);
            var inv = Get(method);
            if (inv.Call == null || !ArgsExact(inv, obj, parameters)) { Reflected++; return method.Invoke(obj, parameters); }
            Compiled++;
            return inv.Call(obj, parameters);
        }

        static bool ArgsExact(Inv inv, object obj, object[] args)
        {
            if (!inv.Static)
            {
                if (obj == null) return false;
                var ot = obj.GetType();
                if (!ReferenceEquals(ot, inv.OkTarget))
                {
                    if (!inv.Decl.IsAssignableFrom(ot)) return false;
                    inv.OkTarget = ot;
                }
            }
            int n = inv.Params.Length;
            int have = args == null ? 0 : args.Length;
            if (have != n) return false;                       // reflection: TargetParameterCountException
            for (int i = 0; i < n; i++)
            {
                var a = args[i];
                if (a == null) { if (inv.ValueParam[i]) return false; continue; }   // reflection passes default(T)
                var at = a.GetType();
                if (!ReferenceEquals(at, inv.Params[i]) && !inv.Params[i].IsAssignableFrom(at)) return false;   // reflection may convert
                if (inv.ValueParam[i] && !ReferenceEquals(at, inv.Params[i])) return false;                      // e.g. boxed enum vs underlying
            }
            return true;
        }

        [HarmonyPatch]
        static class Patch
        {
            [HarmonyPatch(typeof(CallMethod), "DoMethodCall"), HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var invoke = AccessTools.Method(typeof(MethodBase), "Invoke", new[] { typeof(object), typeof(object[]) });
                var mine = AccessTools.Method(typeof(FastCallMethodFeature), "Invoke");
                int replaced = 0;
                foreach (var ci in instructions)
                {
                    if ((ci.opcode == System.Reflection.Emit.OpCodes.Callvirt || ci.opcode == System.Reflection.Emit.OpCodes.Call) && ci.operand is MethodInfo && (MethodInfo)ci.operand == invoke)
                    {
                        ci.opcode = System.Reflection.Emit.OpCodes.Call; ci.operand = mine; replaced++;
                    }
                    yield return ci;
                }
                if (replaced != 2) WildWasteland.Core.Log.Warn(OptimizationRuntime.Instance, "FS3: replaced " + replaced + " MethodBase.Invoke calls in CallMethod.DoMethodCall (expected 2)");
            }
        }
    }
}
