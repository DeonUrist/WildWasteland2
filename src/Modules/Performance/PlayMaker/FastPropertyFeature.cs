using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core.UI;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// FS2 (plan §4, research A §1.5 / R8): SetProperty (3848 uses, 238 every frame) / GetProperty (294, 108 every frame) go
    /// through FsmProperty.SetValue/GetValue (PlayMaker FsmProperty.cs): up to 15 Type.IsAssignableFrom tests to pick the
    /// parameter, then ReflectionUtils.Set/GetMemberValue(MemberInfo[]) (ReflectionUtils.cs) = reflective get per path level,
    /// reflective set (boxing + object[] per call; ~0.8 µs per PropertyInfo.SetValue on Mono).
    /// These prefixes do exactly the same with:
    /// - the branch tests evaluated once per PropertyType (same predicates, same order; the per-call "!XParameter.IsNone"
    ///   checks of SetValue are still evaluated per call, in order),
    /// - compiled getters/setters per member (<see cref="FastReflect"/>; property accessor exceptions wrapped in
    ///   TargetInvocationException like reflection, typed float/int/bool/vector/color paths without boxing),
    /// - ReflectionUtils.SetMemberValue(MemberInfo[]) replicated step by step: getters for levels 0..n-2 (parent/target),
    ///   then, if the target's RUNTIME type is a value type, set the last member on the boxed target and write the box back
    ///   to its parent member (one level, like SetBoxedMemberValue), else set the last member directly.
    /// Fallback to the vanilla method (before any side effect) for: array branches, members that cannot be compiled
    /// (static, indexers, init-only, missing accessor), PropertyType null. A null intermediate value or a value that is not
    /// exactly of the member type is handed to the same ReflectionUtils call vanilla would make at that point (same exception
    /// or conversion). CheckForReinitialize() runs first exactly like vanilla (calling it twice is idempotent).
    /// Shadow: vanilla runs; the compiled chain re-reads the member afterwards and is compared with the parameter (Get) or
    /// with a reflection read (Set).
    /// </summary>
    public sealed class FastPropertyFeature : Feature
    {
        public override string PlanId { get { return "FS2"; } }
        public override string Key { get { return "Compiled properties"; } }
        public override string Title { get { return "Compiled Get/Set Property"; } }
        public override string Category { get { return Categories.PlayMaker; } }
        public override int Order { get { return 12; } }
        public override bool SupportsShadow { get { return true; } }
        public override string Help { get { return "Get/Set Property actions (vehicle input, wheels, rigidbodies) use compiled accessors instead of reflection. Same values, no garbage."; } }

        internal static bool On, Shadow;
        static FastPropertyFeature _inst;
        public static long Sets, Gets, Fallbacks;

        static readonly Func<FsmProperty, Object> TargetCached = FastReflect.Getter<FsmProperty, Object>(typeof(FsmProperty), "targetObjectCached");
        static readonly Func<FsmProperty, MemberInfo[]> MemberInfos = FastReflect.Getter<FsmProperty, MemberInfo[]>(typeof(FsmProperty), "memberInfo");

        protected override void OnEnable(FeatureMode mode)
        {
            if (!EnsurePatched(typeof(Patch))) throw new Exception("FsmProperty patches failed");
            _inst = this;
            Shadow = mode == FeatureMode.Shadow; On = mode == FeatureMode.On;
        }
        protected override void OnDisable() { On = Shadow = false; _entries.Clear(); _shadowFor = null; }
        public override void OnSceneLoaded() { _entries.Clear(); }
        public override void DrawStats(ToolLayout l)
        {
            l.Value("Sets / gets / vanilla fallbacks", Sets + " / " + Gets + " / " + Fallbacks);
            l.Value("Compiled members / paths", _accs.Count + " / " + _chains.Count);
            l.Value("Compile queue (FS2+FS3)", CompileQueue.Stats());
        }

        // ---------------------------------------------------------------- compiled members and paths

        sealed class Acc
        {
            public MemberInfo M; public Type Type; public Type Decl;
            public Func<object, object> Get; public Action<object, object> Set;
            public Delegate GetT, SetT;   // Func<object,T> / Action<object,T> with T == Type, for the common value types
            public volatile bool Ready;   // delegates published by CompileQueue (main thread, spread over frames)
        }

        sealed class Chain
        {
            public MemberInfo[] Members; public Acc[] Levels;
            public bool GetOk, SetOk, MaybeStruct;
        }

        static readonly Dictionary<long, Acc> _accs = new Dictionary<long, Acc>();
        static readonly Dictionary<string, Chain> _chains = new Dictionary<string, Chain>();

        static long MemberKey(MemberInfo m)
        {
            return ((long)m.DeclaringType.TypeHandle.Value.ToInt64() * 31L) ^ ((long)m.MetadataToken << 1) ^ (m is PropertyInfo ? 1L : 0L);
        }

        static Acc GetAcc(MemberInfo m)
        {
            long k = MemberKey(m);
            Acc a;
            if (_accs.TryGetValue(k, out a) && a.M.DeclaringType == m.DeclaringType && a.M.MetadataToken == m.MetadataToken) return a;
            a = new Acc { M = m, Type = FastReflect.MemberType(m), Decl = m.DeclaringType };
            _accs[k] = a;
            var acc = a;
            CompileQueue.Enqueue(_inst != null ? (WildWasteland.Core.IModule)_inst.Module : null, b => AddBuilds(b, acc), () => acc.Ready = true);
            return a;
        }

        /// <summary>Queues every accessor of one member into the current background batch (results land after the build).</summary>
        static void AddBuilds(FastReflect.Batch b, Acc a)
        {
            var m = a.M;
            b.Add(() => FastReflect.BoxedGetter(m), d => a.Get = d);
            b.Add(() => FastReflect.BoxedSetter(m), d => a.Set = d);
            var t = a.Type;
            if (t == typeof(float)) Typed<float>(b, a);
            else if (t == typeof(int)) Typed<int>(b, a);
            else if (t == typeof(bool)) Typed<bool>(b, a);
            else if (t == typeof(Vector3)) Typed<Vector3>(b, a);
            else if (t == typeof(Vector2)) Typed<Vector2>(b, a);
            else if (t == typeof(Quaternion)) Typed<Quaternion>(b, a);
            else if (t == typeof(Color)) Typed<Color>(b, a);
            else if (t == typeof(Rect)) Typed<Rect>(b, a);
        }
        static void Typed<T>(FastReflect.Batch b, Acc a)
        {
            var m = a.M;
            b.Add(() => FastReflect.TypedGetter<T>(m), d => a.GetT = d);
            b.Add(() => FastReflect.TypedSetter<T>(m), d => a.SetT = d);
        }

        /// <summary>A declared member type whose values can never be boxed structs at runtime.</summary>
        static bool AlwaysReference(Type t)
        {
            return t.IsClass && t != typeof(object) && t != typeof(ValueType) && t != typeof(Enum);
        }

        static Chain GetChain(MemberInfo[] members, out bool pending)
        {
            pending = false;
            if (members.Length == 0) return null;
            // structural key (MemberInfo instances are recreated by every FsmProperty.Init)
            var sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < members.Length; i++)
            {
                var m = members[i];
                if (m == null) return null;
                sb.Append(m.DeclaringType.TypeHandle.Value.ToInt64()).Append(':').Append(m.MetadataToken).Append('|');
            }
            string key = sb.ToString();
            Chain c;
            if (_chains.TryGetValue(key, out c)) return c;
            // members still compiling: vanilla for now, ask again once CompileQueue built something
            for (int i = 0; i < members.Length; i++)
            {
                if (!(members[i] is FieldInfo) && !(members[i] is PropertyInfo)) break;
                if (!GetAcc(members[i]).Ready) pending = true;
            }
            if (pending) return null;
            c = new Chain { Members = members, Levels = new Acc[members.Length] };
            bool get = true, set = true, maybeStruct = false;
            for (int i = 0; i < members.Length; i++)
            {
                if (!(members[i] is FieldInfo) && !(members[i] is PropertyInfo)) { get = set = false; break; }
                var a = GetAcc(members[i]);
                c.Levels[i] = a;
                if (a.Get == null) { get = false; if (i < members.Length - 1) set = false; }
                if (i < members.Length - 1 && !AlwaysReference(a.Type)) maybeStruct = true;
            }
            if (set && members.Length > 0 && c.Levels[members.Length - 1].Set == null) set = false;
            // struct write-back needs the setter of the level holding the struct (n-2)
            if (set && maybeStruct && members.Length >= 2 && c.Levels[members.Length - 2].Set == null) set = false;
            c.GetOk = get; c.SetOk = set; c.MaybeStruct = maybeStruct;
            _chains[key] = c;
            return c;
        }

        // ---------------------------------------------------------------- branch tables (per PropertyType)

        sealed class Branches { public bool[] SetMatch = new bool[15]; public int GetIdx = -1; }
        static readonly Dictionary<Type, Branches> _branches = new Dictionary<Type, Branches>();

        // Set order (FsmProperty.SetValue): 0 bool 1 int 2 float 3 string 4 V2 5 V3 6 Rect 7 Quat 8 GameObject 9 Material 10 Texture
        // 11 Color 12 Object subclass 13 array 14 enum. Get order (GetValue): same 0..11, then enum, array, Object subclass.
        const int BEnum = 14, BArray = 13, BObject = 12;

        static Branches GetBranches(Type pt)
        {
            Branches b;
            if (_branches.TryGetValue(pt, out b)) return b;
            b = new Branches();
            var m = b.SetMatch;
            m[0] = pt.IsAssignableFrom(typeof(bool));
            m[1] = pt.IsAssignableFrom(typeof(int));
            m[2] = pt.IsAssignableFrom(typeof(float));
            m[3] = pt.IsAssignableFrom(typeof(string));
            m[4] = pt.IsAssignableFrom(typeof(Vector2));
            m[5] = pt.IsAssignableFrom(typeof(Vector3));
            m[6] = pt.IsAssignableFrom(typeof(Rect));
            m[7] = pt.IsAssignableFrom(typeof(Quaternion));
            m[8] = ReferenceEquals(pt, typeof(GameObject));
            m[9] = ReferenceEquals(pt, typeof(Material));
            m[10] = ReferenceEquals(pt, typeof(Texture));
            m[11] = ReferenceEquals(pt, typeof(Color));
            m[BObject] = pt.IsSubclassOf(typeof(Object));
            m[BArray] = pt.IsArray;
            m[BEnum] = pt.IsEnum;
            for (int i = 0; i <= 11; i++) if (m[i]) { b.GetIdx = i; break; }
            if (b.GetIdx < 0) b.GetIdx = m[BEnum] ? BEnum : m[BArray] ? BArray : m[BObject] ? BObject : -1;
            _branches[pt] = b;
            return b;
        }

        /// <summary>FsmProperty.SetValue's if/else chain: first branch whose type test matches and whose parameter is not None.</summary>
        static int SetBranch(Branches b, FsmProperty p)
        {
            var m = b.SetMatch;
            if (m[0] && !p.BoolParameter.IsNone) return 0;
            if (m[1] && !p.IntParameter.IsNone) return 1;
            if (m[2] && !p.FloatParameter.IsNone) return 2;
            if (m[3] && !p.StringParameter.IsNone) return 3;
            if (m[4] && !p.Vector2Parameter.IsNone) return 4;
            if (m[5] && !p.Vector3Parameter.IsNone) return 5;
            if (m[6] && !p.RectParamater.IsNone) return 6;
            if (m[7] && !p.QuaternionParameter.IsNone) return 7;
            if (m[8] && !p.GameObjectParameter.IsNone) return 8;
            if (m[9] && !p.MaterialParameter.IsNone) return 9;
            if (m[10] && !p.TextureParameter.IsNone) return 10;
            if (m[11] && !p.ColorParameter.IsNone) return 11;
            if (m[BObject] && !p.ObjectParameter.IsNone) return BObject;
            if (m[BArray] && !p.ArrayParameter.IsNone) return BArray;
            if (m[BEnum] && !p.EnumParameter.IsNone) return BEnum;
            return -1;
        }

        // ---------------------------------------------------------------- per FsmProperty entry

        sealed class Entry { public MemberInfo[] Members; public Type PT; public Chain Chain; public Branches Br; public Type RootOk; public bool Pending; public int Ver; }
        static readonly Dictionary<FsmProperty, Entry> _entries = new Dictionary<FsmProperty, Entry>(RefComparer<FsmProperty>.Instance);

        static Entry GetEntry(FsmProperty p, MemberInfo[] members, Type pt)
        {
            Entry e;
            if (_entries.TryGetValue(p, out e) && ReferenceEquals(e.Members, members) && ReferenceEquals(e.PT, pt))
            {
                if (e.Pending && e.Ver != CompileQueue.Version) { e.Ver = CompileQueue.Version; e.Chain = GetChain(members, out e.Pending); }
                return e;
            }
            if (e == null)
            {
                if (_entries.Count > 8192) _entries.Clear();
                e = new Entry(); _entries[p] = e;
            }
            e.Members = members; e.PT = pt; e.Ver = CompileQueue.Version; e.Chain = GetChain(members, out e.Pending); e.Br = GetBranches(pt); e.RootOk = null;
            return e;
        }

        /// <summary>The root object is an instance of the first member's declaring type (always true after Init; checked once per type).</summary>
        static bool RootOk(Entry e, object root)
        {
            var t = root.GetType();
            if (ReferenceEquals(t, e.RootOk)) return true;
            if (!e.Chain.Levels[0].Decl.IsAssignableFrom(t)) return false;
            e.RootOk = t;
            return true;
        }

        // ---------------------------------------------------------------- chain walk (ReflectionUtils semantics)

        static object GetLevel(Chain c, int i, object t)
        {
            // vanilla: GetMemberValue(member, target) -> on a null target the reflection call throws; make exactly that call
            if (t == null) return ReflectionUtils.GetMemberValue(c.Members[i], t);
            return c.Levels[i].Get(t);
        }

        static void BoxedSet(Chain c, int i, object target, object value)
        {
            var a = c.Levels[i];
            bool exact = value == null ? !a.Type.IsValueType : a.Type.IsInstanceOfType(value);
            if (!exact || target == null || !a.Decl.IsInstanceOfType(target)) { ReflectionUtils.SetMemberValue(c.Members[i], target, value); return; }
            a.Set(target, value);
        }

        static object GetBoxed(Chain c, object root)
        {
            object t = root;
            int n = c.Levels.Length;
            for (int i = 0; i < n; i++) t = GetLevel(c, i, t);
            return t;
        }

        static T GetTyped<T>(Chain c, object root)
        {
            object t = root;
            int n = c.Levels.Length;
            for (int i = 0; i < n - 1; i++) t = GetLevel(c, i, t);
            var g = c.Levels[n - 1].GetT as Func<object, T>;
            if (g != null && t != null) return g(t);
            return (T)GetLevel(c, n - 1, t);
        }

        /// <summary>ReflectionUtils.SetMemberValue(MemberInfo[], target, value) step by step.</summary>
        static void SetTyped<T>(Chain c, object root, T value)
        {
            object t = root, parent = null;
            int ti = -1, n = c.Levels.Length;
            for (int i = 0; i < n - 1; i++) { parent = t; ti = i; t = GetLevel(c, i, t); }
            if (t == null) throw new NullReferenceException();   // vanilla: target.GetType() on null
            if (c.MaybeStruct && t.GetType().IsValueType)
            {
                BoxedSet(c, n - 1, t, value);     // SetBoxedMemberValue: last member on the boxed copy ...
                BoxedSet(c, ti, parent, t);       // ... then the box back into its parent member
                return;
            }
            var s = c.Levels[n - 1].SetT as Action<object, T>;
            if (s != null) s(t, value);
            else BoxedSet(c, n - 1, t, value);
        }

        static void SetObject(Chain c, object root, object value)
        {
            object t = root, parent = null;
            int ti = -1, n = c.Levels.Length;
            for (int i = 0; i < n - 1; i++) { parent = t; ti = i; t = GetLevel(c, i, t); }
            if (t == null) throw new NullReferenceException();
            if (c.MaybeStruct && t.GetType().IsValueType) { BoxedSet(c, n - 1, t, value); BoxedSet(c, ti, parent, t); return; }
            BoxedSet(c, n - 1, t, value);
        }

        // ---------------------------------------------------------------- the replacements

        static bool DoSet(FsmProperty p, Entry e, object target)
        {
            int b = SetBranch(e.Br, p);
            if (b < 0) return true;                               // no branch: vanilla does nothing either
            if (b == BArray || !e.Chain.SetOk) return false;      // vanilla path
            var c = e.Chain;
            switch (b)
            {
                case 0: SetTyped(c, target, p.BoolParameter.Value); break;
                case 1: SetTyped(c, target, p.IntParameter.Value); break;
                case 2: SetTyped(c, target, p.FloatParameter.Value); break;
                case 3: SetObject(c, target, p.StringParameter.Value); break;
                case 4: SetTyped(c, target, p.Vector2Parameter.Value); break;
                case 5: SetTyped(c, target, p.Vector3Parameter.Value); break;
                case 6: SetTyped(c, target, p.RectParamater.Value); break;
                case 7: SetTyped(c, target, p.QuaternionParameter.Value); break;
                case 8: SetObject(c, target, p.GameObjectParameter.Value); break;
                case 9: SetObject(c, target, p.MaterialParameter.Value); break;
                case 10: SetObject(c, target, p.TextureParameter.Value); break;
                case 11: SetTyped(c, target, p.ColorParameter.Value); break;
                case BObject:
                    if (p.ObjectParameter.Value == null) SetObject(c, target, null);
                    else SetObject(c, target, p.ObjectParameter.Value);
                    break;
                case BEnum: SetObject(c, target, p.EnumParameter.Value); break;
            }
            return true;
        }

        static bool DoGet(FsmProperty p, Entry e, object target)
        {
            int b = e.Br.GetIdx;
            if (b < 0) return true;
            if (b == BArray || !e.Chain.GetOk) return false;
            var c = e.Chain;
            switch (b)
            {
                case 0: p.BoolParameter.Value = GetTyped<bool>(c, target); break;
                case 1: p.IntParameter.Value = GetTyped<int>(c, target); break;
                case 2: p.FloatParameter.Value = GetTyped<float>(c, target); break;
                case 3: p.StringParameter.Value = (string)GetBoxed(c, target); break;
                case 4: p.Vector2Parameter.Value = GetTyped<Vector2>(c, target); break;
                case 5: p.Vector3Parameter.Value = GetTyped<Vector3>(c, target); break;
                case 6: p.RectParamater.Value = GetTyped<Rect>(c, target); break;
                case 7: p.QuaternionParameter.Value = GetTyped<Quaternion>(c, target); break;
                case 8: p.GameObjectParameter.Value = (GameObject)GetBoxed(c, target); break;
                case 9: p.MaterialParameter.Value = (Material)GetBoxed(c, target); break;
                case 10: p.TextureParameter.Value = (Texture)GetBoxed(c, target); break;
                case 11: p.ColorParameter.Value = GetTyped<Color>(c, target); break;
                case BEnum: p.EnumParameter.Value = (Enum)GetBoxed(c, target); break;
                case BObject: p.ObjectParameter.Value = (Object)GetBoxed(c, target); break;
            }
            return true;
        }

        /// <summary>Common head: CheckForReinitialize + vanilla's early return. Returns the entry, or null with run = whether
        /// the original must run.</summary>
        static Entry Head(FsmProperty p, out object target, out bool runOriginal)
        {
            p.CheckForReinitialize();
            var t = TargetCached(p);
            var members = MemberInfos(p);
            target = t;
            if (t == null || members == null) { runOriginal = Shadow; return null; }   // vanilla returns right here
            var pt = p.PropertyType;
            if ((object)pt == null) { runOriginal = true; return null; }
            var e = GetEntry(p, members, pt);
            if (e.Chain == null || !RootOk(e, t)) { runOriginal = true; return null; }
            runOriginal = false;
            return e;
        }

        // ---------------------------------------------------------------- shadow

        static FsmProperty _shadowFor; static Entry _shadowEntry; static object _shadowTarget;

        static void ShadowCompareGet(FsmProperty p, Entry e, object target)
        {
            int b = e.Br.GetIdx;
            if (b < 0 || b == BArray || !e.Chain.GetOk) return;
            object mine = GetBoxed(e.Chain, target), vanilla;
            switch (b)
            {
                case 0: vanilla = p.BoolParameter.Value; break;
                case 1: vanilla = p.IntParameter.Value; break;
                case 2: vanilla = p.FloatParameter.Value; break;
                case 3: vanilla = p.StringParameter.Value; break;
                case 4: vanilla = p.Vector2Parameter.Value; break;
                case 5: vanilla = p.Vector3Parameter.Value; break;
                case 6: vanilla = p.RectParamater.Value; break;
                case 7: vanilla = p.QuaternionParameter.Value; break;
                case 8: vanilla = p.GameObjectParameter.Value; break;
                case 9: vanilla = p.MaterialParameter.Value; break;
                case 10: vanilla = p.TextureParameter.Value; break;
                case 11: vanilla = p.ColorParameter.Value; break;
                case BEnum: vanilla = p.EnumParameter.Value; break;
                default: vanilla = p.ObjectParameter.Value; break;
            }
            if (!Equals(mine, vanilla) && _inst != null) _inst.Mismatch("GetProperty " + p.TargetTypeName + "." + p.PropertyName + ": vanilla " + vanilla + ", compiled " + mine);
        }

        static void ShadowCompareSet(FsmProperty p, Entry e, object target)
        {
            if (!e.Chain.GetOk || SetBranch(e.Br, p) < 0) return;
            object mine = GetBoxed(e.Chain, target);
            object vanilla = ReflectionUtils.GetMemberValue(e.Chain.Members, target);
            if (!Equals(mine, vanilla) && _inst != null) _inst.Mismatch("SetProperty " + p.TargetTypeName + "." + p.PropertyName + ": reflection reads " + vanilla + ", compiled reads " + mine);
        }

        [HarmonyPatch]
        static class Patch
        {
            [HarmonyPatch(typeof(FsmProperty), "SetValue"), HarmonyPrefix]
            static bool SetPre(FsmProperty __instance)
            {
                if (!On && !Shadow) return true;
                object target; bool run;
                var e = Head(__instance, out target, out run);
                if (e == null) { if (run && On) Fallbacks++; return run; }
                if (Shadow) { _shadowFor = __instance; _shadowEntry = e; _shadowTarget = target; return true; }
                Sets++;
                if (!DoSet(__instance, e, target)) { Fallbacks++; return true; }
                return false;
            }

            [HarmonyPatch(typeof(FsmProperty), "SetValue"), HarmonyPostfix]
            static void SetPost(FsmProperty __instance)
            {
                if (!Shadow || !ReferenceEquals(_shadowFor, __instance)) return;
                _shadowFor = null;
                try { ShadowCompareSet(__instance, _shadowEntry, _shadowTarget); } catch (Exception) { }
            }

            [HarmonyPatch(typeof(FsmProperty), "GetValue"), HarmonyPrefix]
            static bool GetPre(FsmProperty __instance)
            {
                if (!On && !Shadow) return true;
                object target; bool run;
                var e = Head(__instance, out target, out run);
                if (e == null) { if (run && On) Fallbacks++; return run; }
                if (Shadow) { _shadowFor = __instance; _shadowEntry = e; _shadowTarget = target; return true; }
                Gets++;
                if (!DoGet(__instance, e, target)) { Fallbacks++; return true; }
                return false;
            }

            [HarmonyPatch(typeof(FsmProperty), "GetValue"), HarmonyPostfix]
            static void GetPost(FsmProperty __instance)
            {
                if (!Shadow || !ReferenceEquals(_shadowFor, __instance)) return;
                _shadowFor = null;
                try { ShadowCompareGet(__instance, _shadowEntry, _shadowTarget); } catch (Exception) { }
            }
        }
    }
}
