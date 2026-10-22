using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// PM10 (plan §4, research A §3 #14: ConvertFloatToString 225 uses / 191 everyFrame, ConvertIntToString 8; HUD FSMs).
    /// Vanilla (ConvertFloatToString.cs DoConvertFloatToString): stringVariable.Value = format.IsNone || format empty ?
    /// floatVariable.Value.ToString() : floatVariable.Value.ToString(format.Value) — a new string every frame.
    /// The replacement evaluates exactly the same, but when the value (bit pattern: -0/NaN payloads differ), the chosen format
    /// string (ordinal equality) and the current NumberFormatInfo (culture) are the same as at this action's previous call,
    /// it stores the previous string instance instead of formatting again. The FsmString is still assigned every call
    /// (another action may have written it in between). Only identity of the string object differs (same characters).
    /// Other per-frame string actions (StringAppend, BuildString) build on their own output and cannot be reused exactly.
    /// </summary>
    public sealed class StringConvertFeature : Feature
    {
        public override string PlanId { get { return "PM10"; } }
        public override string Key { get { return "Number to text"; } }
        public override string Title { get { return "Reuse unchanged number strings"; } }
        public override string Category { get { return Categories.PlayMaker; } }
        public override int Order { get { return 10; } }
        public override bool SupportsShadow { get { return true; } }
        public override string Help { get { return "HUD FSMs format the same number into a new string every frame; the previous string is reused while the number is unchanged. Same text, less garbage."; } }

        internal static bool On, Shadow;
        static StringConvertFeature _inst;
        public static long Calls, Reused;

        [StructLayout(LayoutKind.Explicit)]
        struct Bits { [FieldOffset(0)] public float F; [FieldOffset(0)] public int I; }

        sealed class Entry { public int Value; public string Format; public NumberFormatInfo Nfi; public string Result; }
        static readonly Dictionary<FsmStateAction, Entry> _cache = new Dictionary<FsmStateAction, Entry>(RefComparer<FsmStateAction>.Instance);

        protected override void OnEnable(FeatureMode mode)
        {
            if (!EnsurePatched(typeof(Patch))) throw new Exception("ConvertFloatToString / ConvertIntToString patch failed");
            _inst = this; _cache.Clear();
            Shadow = mode == FeatureMode.Shadow; On = mode == FeatureMode.On;
        }
        protected override void OnDisable() { On = Shadow = false; _cache.Clear(); }
        public override void OnSceneLoaded() { _cache.Clear(); }
        public override void DrawStats(ToolLayout l) { l.Value("Conversions / reused", Calls + " / " + Reused + " (cached actions " + _cache.Count + ")"); }

        /// <summary>The cached string when (value, format, culture) match the previous call of this action, else null.</summary>
        static string Lookup(FsmStateAction a, int value, string format, NumberFormatInfo nfi, out Entry e)
        {
            if (!_cache.TryGetValue(a, out e))
            {
                if (_cache.Count > 4096) _cache.Clear();   // actions of destroyed objects: bounded
                e = new Entry();
                _cache[a] = e;
                return null;
            }
            if (e.Result != null && e.Value == value && ReferenceEquals(e.Nfi, nfi) && string.Equals(e.Format, format)) return e.Result;
            return null;
        }

        static void Store(Entry e, int value, string format, NumberFormatInfo nfi, string result)
        {
            e.Value = value; e.Format = format; e.Nfi = nfi; e.Result = result;
        }

        [HarmonyPatch]
        static class Patch
        {
            [HarmonyPatch(typeof(ConvertFloatToString), "DoConvertFloatToString"), HarmonyPrefix]
            static bool FloatPre(ConvertFloatToString __instance)
            {
                if (!On && !Shadow) return true;
                Calls++;
                var a = __instance;
                // same evaluation order as vanilla: format first, then the float
                string fmt = a.format.IsNone || string.IsNullOrEmpty(a.format.Value) ? null : a.format.Value;
                float v = a.floatVariable.Value;
                var nfi = NumberFormatInfo.CurrentInfo;
                Bits b = default(Bits); b.F = v;
                Entry e;
                string s = Lookup(a, b.I, fmt, nfi, out e);
                if (Shadow)
                {
                    string fresh = fmt == null ? v.ToString() : v.ToString(fmt);
                    if (s != null && !string.Equals(s, fresh) && _inst != null) _inst.Mismatch("ConvertFloatToString '" + s + "' vs '" + fresh + "'");
                    Store(e, b.I, fmt, nfi, fresh);
                    return true;
                }
                if (s == null) { s = fmt == null ? v.ToString() : v.ToString(fmt); Store(e, b.I, fmt, nfi, s); }
                else Reused++;
                a.stringVariable.Value = s;
                return false;
            }

            [HarmonyPatch(typeof(ConvertIntToString), "DoConvertIntToString"), HarmonyPrefix]
            static bool IntPre(ConvertIntToString __instance)
            {
                if (!On && !Shadow) return true;
                Calls++;
                var a = __instance;
                string fmt = a.format.IsNone || string.IsNullOrEmpty(a.format.Value) ? null : a.format.Value;
                int v = a.intVariable.Value;
                var nfi = NumberFormatInfo.CurrentInfo;
                Entry e;
                string s = Lookup(a, v, fmt, nfi, out e);
                if (Shadow)
                {
                    string fresh = fmt == null ? v.ToString() : v.ToString(fmt);
                    if (s != null && !string.Equals(s, fresh) && _inst != null) _inst.Mismatch("ConvertIntToString '" + s + "' vs '" + fresh + "'");
                    Store(e, v, fmt, nfi, fresh);
                    return true;
                }
                if (s == null) { s = fmt == null ? v.ToString() : v.ToString(fmt); Store(e, v, fmt, nfi, s); }
                else Reused++;
                a.stringVariable.Value = s;
                return false;
            }
        }
    }
}
