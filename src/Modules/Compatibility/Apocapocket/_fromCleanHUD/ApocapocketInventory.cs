using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
{
    internal sealed class ApocapocketInventory
    {
        private const string Guid = "com.denis.apocalypter.apocapocket";
        private static Func<object, object> _instance, _slots, _operation, _save, _shifts;
        private static Func<object, bool> _ready, _busy;
        private static Func<object, int> _selected, _unlocked, _origin;
        private static Func<object, string> _handName;
        private static Func<object, GameObject> _held;
        private static Func<object, int, GameObject> _content;
        private static Func<object, int, int> _kind;
        private static Func<object, Transform> _shiftTransform;
        private static Func<object, Vector3> _shiftOriginal;
        private static Func<object, RawImage[]> _uiIcons;
        private static Func<GameObject, Texture> _icon;
        private static object _enabled;
        private static readonly List<Graphic> Graphics = new List<Graphic>();
        internal static int UiRevision;
        internal static bool Enabled { get { var e = _enabled as ConfigEntry<bool>; return e != null && e.Value; } }
        internal readonly GameObject[] Items = new GameObject[6];
        internal readonly bool[] Ordinary = new bool[6];
        internal int Count = 3, Selected = -1;
        internal bool Active;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(CleanHudModule owner)
        {
            Clear();
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out info) || ReferenceEquals(info.Instance, null)) return;
            try
            {
                var asm = info.Instance.GetType().Assembly;
                var runner = asm.GetType("Apocapocket.Runner", true); var extra = asm.GetType("Apocapocket.ExtraSlots", true);
                _instance = Getter<object>(runner, "Instance"); _slots = Getter<object>(runner, "Slots");
                _operation = Getter<object>(runner, "CurrentOp"); _save = Getter<object>(runner, "Save");
                _ready = Getter<bool>(runner, "Ready"); _busy = Getter<bool>(AccessTools.Field(runner, "Save").FieldType, "Busy");
                _selected = Getter<int>(runner, "Selected"); _unlocked = Getter<int>(runner, "Unlocked");
                _origin = Getter<int>(runner, "_handOrigin"); _handName = Getter<string>(runner, "_handName");
                var r = Expression.Parameter(typeof(object), "r");
                _held = Expression.Lambda<Func<object, GameObject>>(Expression.Call(Expression.Convert(r, runner), AccessTools.Method(runner, "HeldItem")), r).Compile();
                var array = AccessTools.Field(runner, "Slots").FieldType;
                _content = Element<GameObject>(array, "Content"); _kind = Element<int>(array, "Kind");
                if (Convert.ToInt32(Enum.Parse(AccessTools.Field(array.GetElementType(), "Kind").FieldType, "Item")) != 1) throw new NotSupportedException("Unknown inventory kinds");
                _icon = (Func<GameObject, Texture>)Delegate.CreateDelegate(typeof(Func<GameObject, Texture>), AccessTools.Method(asm.GetType("Apocapocket.Icons", true), "IconFor"));
                _shifts = Getter<object>(extra, "_shifted");
                var shift = AccessTools.Field(extra, "_shifted").FieldType.GetGenericArguments()[0];
                _shiftTransform = Getter<Transform>(shift, "T"); _shiftOriginal = Getter<Vector3>(shift, "Orig");
                _uiIcons = Getter<RawImage[]>(extra, "_icons");
                if (!Core.Patches.Patch(owner, AccessTools.Method(extra, "BuildUi"), postfix: AccessTools.Method(typeof(ApocapocketInventory), "AfterBuildUi"))) throw new InvalidOperationException("UI hook unavailable");
                _enabled = AccessTools.Field(info.Instance.GetType(), "Enabled").GetValue(null);
                CleanHudModule.Log("Apocapocket inventory bridge ready (" + info.Metadata.Version + ")");
            }
            catch (Exception e) { Clear(); CleanHudModule.Warn("Apocapocket bridge unavailable; using native slots: " + e.Message); }
        }
        internal static void Clear() { _enabled = null; _instance = null; _icon = null; _shifts = null; Graphics.Clear(); UiRevision = 0; }
        private static void AfterBuildUi() { if (_instance != null && _uiIcons(null)[0] != null) UiRevision++; }

        private static Func<object, T> Getter<T>(Type type, string name)
        {
            var o = Expression.Parameter(typeof(object), "o"); var f = AccessTools.Field(type, name);
            Expression member = f != null ? (Expression)Expression.Field(f.IsStatic ? null : Expression.Convert(o, type), f) : Expression.Property(Expression.Convert(o, type), AccessTools.Property(type, name));
            return Expression.Lambda<Func<object, T>>(Expression.Convert(member, typeof(T)), o).Compile();
        }
        private static Func<object, int, T> Element<T>(Type array, string name)
        {
            var a = Expression.Parameter(typeof(object), "a"); var i = Expression.Parameter(typeof(int), "i");
            var element = Expression.ArrayIndex(Expression.Convert(a, array), i);
            return Expression.Lambda<Func<object, int, T>>(Expression.Convert(Expression.Field(element, AccessTools.Field(array.GetElementType(), name)), typeof(T)), a, i).Compile();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Read()
        {
            try
            {
                var run = Enabled && _instance != null ? _instance(null) : null;
                Active = run != null && (run as UnityEngine.Object) != null;
                if (!Active) { Count = 3; Selected = -1; Array.Clear(Items, 0, 6); Array.Clear(Ordinary, 0, 6); return; }
                if (!_ready(run) || _operation(run) != null || _busy(_save(run))) return;
                var slots = _slots(run) as Array; if (slots == null || slots.Length < 6) return;
                Count = Mathf.Clamp(_unlocked(run), 3, 6); Selected = _selected(run);
                if (Selected < 0 || Selected >= Count) Selected = -1;
                for (int i = 0; i < 6; i++) { Items[i] = _content(slots, i); Ordinary[i] = _kind(slots, i) == 1; }
                int origin = _origin(run); var held = origin >= 0 && origin < 6 ? _held(run) : null;
                if (held != null && Items[origin] == null && HeldMatches(held, _handName(run))) { Items[origin] = held; Ordinary[origin] = true; }
            }
            catch (Exception e) { Clear(); Active = false; Count = 3; Selected = -1; CleanHudModule.Warn("Apocapocket inventory read failed; using native slots: " + e.Message); }
        }
        // held.name allocates; the comparison is reused while the held object and hand name stay the same (re-read every 0.5 s).
        private GameObject _heldObject; private string _heldHand; private bool _heldMatch; private float _heldCheck;
        private bool HeldMatches(GameObject held, string hand)
        {
            float now = Time.unscaledTime;
            if (!ReferenceEquals(held, _heldObject) || !ReferenceEquals(hand, _heldHand) || now >= _heldCheck)
            { _heldObject = held; _heldHand = hand; _heldMatch = held.name == hand; _heldCheck = now + .5f; }
            return _heldMatch;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Texture ItemIcon(GameObject item) { return _icon != null && item != null ? _icon(item) : null; }

        // Once on attachment and after native widget creation, never a per-frame hierarchy scan.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CaptureUi(Bindings src, Transform hud)
        {
            if (_instance == null || src.Canvas == null) return;
            Graphics.Clear(); src.Canvas.GetComponentsInChildren<Graphic>(true, Graphics);
            foreach (var g in Graphics)
                for (var t = g.transform; t != null && t != src.Canvas.transform; t = t.parent)
                    if (t.name.EndsWith("_apocapocket", StringComparison.Ordinal) || t.name == "Apocapocket.CardExtension") { src.CaptureExtra(g); break; }
            Graphics.Clear();
            var shifts = _shifts(null) as IList;
            if (shifts != null && hud != null) for (int i = shifts.Count - 1; i >= 0; i--)
            { var t = _shiftTransform(shifts[i]); if (t != null && (t == hud || t.IsChildOf(hud))) { t.localPosition = _shiftOriginal(shifts[i]); shifts.RemoveAt(i); } }
        }
        internal static string ActionLabel(string name)
        { return Enabled && name != null && name.Length == 8 && name.StartsWith("Weapon ", StringComparison.Ordinal) && name[7] >= '1' && name[7] <= '3' ? "ITEM " + name[7] : null; }
    }
}
