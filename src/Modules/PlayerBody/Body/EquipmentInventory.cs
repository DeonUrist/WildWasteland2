using System;
using System.Linq.Expressions;
using System.Reflection;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    // Read-only logical custody: an extra-slot weapon may physically occupy a
    // vanilla holder while that holder's original item is parked elsewhere.
    internal sealed class EquipmentInventory
    {
        public readonly GameObject[] Items = new GameObject[6];
        // (2.2.6) read every frame: the names and the invoke argument made once
        private static readonly string[] SlotNames = { "Slot 1", "Slot 2", "Slot 3", "Slot 4", "Slot 5", "Slot 6" };
        private static readonly string[] PocketNames = { "Apocapocket_ExtraSlots/Slot 1", "Apocapocket_ExtraSlots/Slot 2", "Apocapocket_ExtraSlots/Slot 3", "Apocapocket_ExtraSlots/Slot 4", "Apocapocket_ExtraSlots/Slot 5", "Apocapocket_ExtraSlots/Slot 6" };
        private static readonly string[] InvNames = { "Apocainventory_ExtraSlots/Slot 1", "Apocainventory_ExtraSlots/Slot 2", "Apocainventory_ExtraSlots/Slot 3", "Apocainventory_ExtraSlots/Slot 4", "Apocainventory_ExtraSlots/Slot 5", "Apocainventory_ExtraSlots/Slot 6" };
        public int Drawn = -1;
        private Transform _camera, _weapons;
        private PlayMakerFSM _weaponFsm;
        private static bool _resolved;   // (WW) resolved once per game session (was once per body)
        // (WW) the Apocapocket / Apocainventory bridge is read every frame: the members are compiled once into typed delegates (no
        // FieldInfo.GetValue / MethodInfo.Invoke, no boxing per frame); a member that can't be compiled falls back to reflection
        private static Func<object> _instance, _enabled;
        private static Func<object, object> _slots, _operation, _save;
        private static Func<object, bool> _ready, _loading, _normalised;
        private static Func<object, int, GameObject> _content;
        private static Func<object, int, bool> _isDrawn;
        private static Func<object, int> _handOrigin;
        private static Func<object, string> _handName;
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                foreach (string ns in new[] { "Apocapocket", "Apocainventory" })
                {
                    Type runner;
                    try { runner = asm.GetType(ns + ".Runner", false); } catch (Exception) { continue; }
                    if (runner == null) continue;
                    var slots = runner.GetField("Slots", Flags);
                    var content = slots != null && slots.FieldType.IsArray ? slots.FieldType.GetElementType().GetField("Content", Flags) : null;
                    var instance = runner.GetField("Instance", Flags);
                    var ready = runner.GetProperty("Ready", Flags);
                    if (content == null || instance == null || ready == null) continue;
                    try
                    {
                        _instance = Bridge.Static(instance);
                        _slots = Bridge.Obj(slots);
                        _content = Bridge.Element(slots.FieldType, content);
                        _ready = Bridge.Bool(ready);
                        var op = runner.GetField("CurrentOp", Flags); _operation = op != null ? Bridge.Obj(op) : null;
                        var save = runner.GetField("Save", Flags); _save = save != null ? Bridge.Obj(save) : null;
                        var loading = save != null ? save.FieldType.GetField("Loading", Flags) : null;
                        var normalised = save != null ? save.FieldType.GetField("Normalised", Flags) : null;
                        _loading = loading != null ? Bridge.Bool(loading) : null;
                        _normalised = normalised != null ? Bridge.Bool(normalised) : null;
                        var isDrawn = runner.GetMethod("IsDrawn", Flags);
                        _isDrawn = isDrawn != null ? Bridge.IntToBool(isDrawn) : null;
                        var ho = runner.GetField("_handOrigin", Flags); var hn = runner.GetField("_handName", Flags);
                        _handOrigin = ho != null && hn != null ? Bridge.Int(ho) : null;
                        _handName = ho != null && hn != null ? Bridge.Str(hn) : null;
                        var plugin = asm.GetType(ns + ".Plugin", false);
                        var en = plugin != null ? plugin.GetField("Enabled", Flags) : null;
                        _enabled = en != null ? Bridge.Static(en) : null;
                        PlayerBodyModule.Verbose("Equipment: logical inventory bridge to " + ns);
                        return;
                    }
                    catch (Exception e) { PlayerBodyModule.Warn("Equipment: inventory bridge to " + ns + " failed (" + e.Message + "), using the vanilla slots"); _instance = null; return; }
                }
        }

        // (WW perf) the vanilla Weapons FSM's state (its state's name: the same string object until it changes), a cheap per-frame change signal
        public string WeaponState { get { return _weaponFsm != null ? _weaponFsm.ActiveStateName : null; } }
        // False preserves previous mount assignments during transactions and saves.
        public bool Read()
        {
            if (_camera != Game.PlayerCamera)
            {
                _camera = Game.PlayerCamera;
                _weapons = null;
                _weaponFsm = null;
                Array.Clear(Items, 0, Items.Length); Drawn = -1;
            }
            if (_weapons == null && _camera != null)
            {
                _weapons = _camera.Find("Weapons") ?? _camera.Find("HandItemUse/Weapons");
                _weaponFsm = null;
                if (_weapons != null) foreach (var f in _weapons.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Weapons") _weaponFsm = f;
            }
            Resolve();
            if (_instance != null)
            {
                var enabled = _enabled != null ? _enabled() as ConfigEntry<bool> : null;
                var run = _instance();
                if (run != null && (enabled == null || enabled.Value))
                {
                    if (!_ready(run)) return false;
                    var save = _save != null ? _save(run) : null;
                    if (_operation != null && _operation(run) != null
                        || save != null && (_loading != null && _loading(save) || _normalised != null && _normalised(save))) return false;
                    var slots = _slots(run) as Array;
                    Array.Clear(Items, 0, Items.Length); Drawn = -1;
                    for (int i = 0; slots != null && i < Mathf.Min(Items.Length, slots.Length); i++)
                    {
                        Items[i] = _content(slots, i);
                        if (_isDrawn != null && _isDrawn(run, i)) Drawn = i;
                    }
                    if (_handOrigin != null && _handName != null && _camera != null)
                    {
                        int origin = _handOrigin(run);
                        var hand = FirstItem(_camera.Find("Hand"));
                        if (origin >= 0 && origin < Items.Length && Items[origin] == null && hand != null && hand.name == _handName(run))
                        { Items[origin] = hand; Drawn = origin; }
                    }
                    return true;
                }
            }
            Array.Clear(Items, 0, Items.Length); Drawn = -1;
            for (int i = 0; i < Items.Length; i++)
            {
                var holder = _weapons != null ? _weapons.Find(SlotNames[i]) : null;
                if (holder == null && i >= 3 && _camera != null)
                    holder = _camera.Find(PocketNames[i]) ?? _camera.Find(InvNames[i]);
                Items[i] = FirstItem(holder);
                if (_weaponFsm != null && _weaponFsm.ActiveStateName == SlotNames[i]) Drawn = i;
            }
            return true;
        }

        // typed accessors for members known only by name at run time (System.Linq.Expressions, compiled once)
        private static class Bridge
        {
            static Expression Target(ParameterExpression o, MemberInfo m, bool isStatic) { return isStatic ? null : Expression.Convert(o, m.DeclaringType); }
            internal static Func<object> Static(FieldInfo f)
            {
                try { return Expression.Lambda<Func<object>>(Expression.Convert(Expression.Field(null, f), typeof(object))).Compile(); }
                catch (Exception) { return () => f.GetValue(null); }
            }
            internal static Func<object, object> Obj(FieldInfo f)
            {
                try { var o = Expression.Parameter(typeof(object), "o"); return Expression.Lambda<Func<object, object>>(Expression.Convert(Expression.Field(Target(o, f, f.IsStatic), f), typeof(object)), o).Compile(); }
                catch (Exception) { return x => f.GetValue(x); }
            }
            internal static Func<object, bool> Bool(FieldInfo f)
            {
                try { var o = Expression.Parameter(typeof(object), "o"); return Expression.Lambda<Func<object, bool>>(Expression.Convert(Expression.Field(Target(o, f, f.IsStatic), f), typeof(bool)), o).Compile(); }
                catch (Exception) { return x => (bool)f.GetValue(x); }
            }
            internal static Func<object, bool> Bool(PropertyInfo p)
            {
                var g = p.GetGetMethod(true);
                try { var o = Expression.Parameter(typeof(object), "o"); return Expression.Lambda<Func<object, bool>>(Expression.Convert(Expression.Property(Target(o, p, g.IsStatic), p), typeof(bool)), o).Compile(); }
                catch (Exception) { return x => (bool)p.GetValue(x, null); }
            }
            internal static Func<object, int> Int(FieldInfo f)
            {
                try { var o = Expression.Parameter(typeof(object), "o"); return Expression.Lambda<Func<object, int>>(Expression.Convert(Expression.Field(Target(o, f, f.IsStatic), f), typeof(int)), o).Compile(); }
                catch (Exception) { return x => (int)f.GetValue(x); }
            }
            internal static Func<object, string> Str(FieldInfo f)
            {
                try { var o = Expression.Parameter(typeof(object), "o"); return Expression.Lambda<Func<object, string>>(Expression.TypeAs(Expression.Field(Target(o, f, f.IsStatic), f), typeof(string)), o).Compile(); }
                catch (Exception) { return x => f.GetValue(x) as string; }
            }
            // slots[i].Content (the element may be a class or a struct: read in place, no boxing)
            internal static Func<object, int, GameObject> Element(Type arrayType, FieldInfo content)
            {
                try
                {
                    var a = Expression.Parameter(typeof(object), "a"); var i = Expression.Parameter(typeof(int), "i");
                    Expression item = Expression.ArrayIndex(Expression.Convert(a, arrayType), i);
                    if (!arrayType.GetElementType().IsValueType) item = Expression.Condition(Expression.Equal(item, Expression.Constant(null, arrayType.GetElementType())),
                        Expression.Constant(null, content.FieldType), Expression.Field(item, content));
                    else item = Expression.Field(item, content);
                    return Expression.Lambda<Func<object, int, GameObject>>(Expression.TypeAs(item, typeof(GameObject)), a, i).Compile();
                }
                catch (Exception)
                {
                    return (arr, idx) => { var slot = ((Array)arr).GetValue(idx); return slot != null ? content.GetValue(slot) as GameObject : null; };
                }
            }
            internal static Func<object, int, bool> IntToBool(MethodInfo m)
            {
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(int) && m.ReturnType == typeof(bool))
                {
                    try
                    {
                        var o = Expression.Parameter(typeof(object), "o"); var i = Expression.Parameter(typeof(int), "i");
                        return Expression.Lambda<Func<object, int, bool>>(Expression.Call(m.IsStatic ? null : Expression.Convert(o, m.DeclaringType), m, i), o, i).Compile();
                    }
                    catch (Exception) { }
                }
                var arg = new object[1];
                return (x, idx) => { arg[0] = idx; return (bool)m.Invoke(x, arg); };
            }
        }

        public static GameObject FirstItem(Transform holder)
        {
            if (holder == null) return null;
            for (int i = 0; i < holder.childCount; i++)
            {
                var child = holder.GetChild(i);
                if (child.GetComponent<Rigidbody>() != null || child.GetComponentInChildren<MeshRenderer>(true) != null) return child.gameObject;
            }
            return null;
        }
    }
}
