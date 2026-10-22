using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using MonoMod.Utils;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Apocapocket (com.denis.apocalypter.apocapocket) slot reader serving the HUD and the <see cref="IInventorySlots"/> provider: the
    /// mod's Runner state (unlocked count, selection, slot contents and kinds, the item parked in the hand) is read through generated
    /// accessors; the HUD also borrows the mod's icons, hides its extra card widgets (<see cref="CaptureUi"/>) and labels its keys.
    /// </summary>
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
        private static IModule _owner;
        private static readonly List<Graphic> Graphics = new List<Graphic>();
        /// <summary>Increments whenever the mod rebuilds its slot UI (the HUD re-captures its widgets).</summary>
        internal static int UiRevision;
        /// <summary>The mod's own Enabled switch.</summary>
        internal static bool Enabled { get { var e = _enabled as ConfigEntry<bool>; return e != null && e.Value; } }
        /// <summary>True when the bridge resolved the mod.</summary>
        internal static bool Ready { get { return _instance != null; } }
        /// <summary>Slot contents of the last <see cref="Read"/>.</summary>
        internal readonly GameObject[] Items = new GameObject[6];
        /// <summary>True for slots holding an ordinary (non-weapon) item.</summary>
        internal readonly bool[] Ordinary = new bool[6];
        /// <summary>Unlocked slot count (3..6) and selected slot (-1 = none).</summary>
        internal int Count = 3, Selected = -1;
        /// <summary>True while the mod is enabled and its runner exists.</summary>
        internal bool Active;

        /// <summary>Resolves the mod's Runner/ExtraSlots/Icons members and postfixes ExtraSlots.BuildUi under <paramref name="owner"/>.</summary>
        internal static void Install(IModule owner)
        {
            Clear(); _owner = owner;
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
                _held = Access.Call<Func<object, GameObject>>(AccessTools.Method(runner, "HeldItem"), typeof(GameObject), typeof(object));
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
                Log.Main(owner, "Apocapocket inventory bridge ready (" + info.Metadata.Version + ")");
            }
            catch (Exception e) { Clear(); Log.Warn(owner, "Apocapocket bridge unavailable; using native slots: " + e.Message); }
        }
        /// <summary>Forgets every accessor (native slots are used until the next Install).</summary>
        internal static void Clear() { _enabled = null; _instance = null; _icon = null; _shifts = null; Graphics.Clear(); UiRevision = 0; }
        /// <summary>Postfix of ExtraSlots.BuildUi (when the mod rebuilds its widgets): bumps <see cref="UiRevision"/>.</summary>
        private static void AfterBuildUi() { if (_instance != null && _uiIcons(null)[0] != null) UiRevision++; }

        /// <summary>Generated getter of a field or property (instance or static) converted to <typeparamref name="T"/>.</summary>
        private static Func<object, T> Getter<T>(Type type, string name) { return Access.Get<T>(type, name); }

        /// <summary>Generated reader of array[i].field (struct or class element).</summary>
        private static Func<object, int, T> Element<T>(Type array, string name)
        {
            var element = array.GetElementType(); var field = AccessTools.Field(element, name);
            using (var dm = new DynamicMethodDefinition("WW_Pocket_" + name, typeof(T), new[] { typeof(object), typeof(int) }))
            {
                var il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, array); il.Emit(OpCodes.Ldarg_1);
                if (element.IsValueType) il.Emit(OpCodes.Ldelema, element); else il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Ldfld, field);
                if (field.FieldType.IsEnum && typeof(T) == typeof(int)) { }
                else if (field.FieldType.IsValueType && !typeof(T).IsValueType) il.Emit(OpCodes.Box, field.FieldType);
                else if (!field.FieldType.IsValueType && field.FieldType != typeof(T)) il.Emit(OpCodes.Castclass, typeof(T));
                il.Emit(OpCodes.Ret);
                return (Func<object, int, T>)Delegate.CreateDelegate(typeof(Func<object, int, T>), dm.Generate());
            }
        }

        /// <summary>Reads the mod's runner into <see cref="Items"/>/<see cref="Ordinary"/>/<see cref="Count"/>/<see cref="Selected"/> (HUD: every 0.5 s).</summary>
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
            catch (Exception e) { Clear(); Active = false; Count = 3; Selected = -1; Log.Warn(_owner, "Apocapocket inventory read failed; using native slots: " + e.Message); }
        }
        // held.name allocates; the comparison is reused while the held object and hand name stay the same (re-read every 0.5 s).
        private GameObject _heldObject; private string _heldHand; private bool _heldMatch; private float _heldCheck;
        /// <summary>True when the item in the hand is the one parked from the origin slot (name compare cached for 0.5 s).</summary>
        private bool HeldMatches(GameObject held, string hand)
        {
            float now = Time.unscaledTime;
            if (!ReferenceEquals(held, _heldObject) || !ReferenceEquals(hand, _heldHand) || now >= _heldCheck)
            { _heldObject = held; _heldHand = hand; _heldMatch = held.name == hand; _heldCheck = now + .5f; }
            return _heldMatch;
        }
        /// <summary>The mod's icon of an item (borrowed).</summary>
        internal static Texture ItemIcon(GameObject item) { return _icon != null && item != null ? _icon(item) : null; }

        /// <summary>HUD widget capture (once on attachment and after native widget creation, never per frame): hands the mod's extra
        /// card graphics to the HUD bindings and undoes the mod's shifts of HUD widgets.</summary>
        internal static void CaptureUi(Canvas canvas, Transform hud, List<Graphic> into) // refs-ok: REFACTOR-REQUEST B17-9
        {
            if (_instance == null || canvas == null) return;
            Graphics.Clear(); canvas.GetComponentsInChildren<Graphic>(true, Graphics);
            foreach (var g in Graphics)
                for (var t = g.transform; t != null && t != canvas.transform; t = t.parent)
                    if (t.name.EndsWith("_apocapocket", StringComparison.Ordinal) || t.name == "Apocapocket.CardExtension") { into.Add(g); break; }
            Graphics.Clear();
            var shifts = _shifts(null) as IList;
            if (shifts != null && hud != null) for (int i = shifts.Count - 1; i >= 0; i--)
            { var t = _shiftTransform(shifts[i]); if (t != null && (t == hud || t.IsChildOf(hud))) { t.localPosition = _shiftOriginal(shifts[i]); shifts.RemoveAt(i); } }
        }
        /// <summary>"ITEM n" for the native "Weapon n" key actions (n = 1..3) while the mod is enabled, else null.</summary>
        internal static string ActionLabel(string name)
        { return Enabled && name != null && name.Length == 8 && name.StartsWith("Weapon ", StringComparison.Ordinal) && name[7] >= '1' && name[7] <= '3' ? "ITEM " + name[7] : null; }
    }
}
