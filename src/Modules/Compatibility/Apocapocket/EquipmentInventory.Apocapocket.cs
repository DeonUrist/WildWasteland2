using System;
using BepInEx.Configuration;
using System.Reflection;
using System.Reflection.Emit;
using MonoMod.Utils;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Reads optional logical inventory custody through accessors owned entirely by Compatibility.</summary>
    internal sealed class LogicalEquipmentInventory : ILogicalEquipmentInventory
    {
        private Transform _camera;
        /// <summary>Reads the external logical snapshot or its existing physical-holder fallback.</summary>
        public bool Read(GameObject[] Items, ref int Drawn)
        {
            if (_camera != PlayerState.PlayerCamera)
            {
                _camera = PlayerState.PlayerCamera;
                Array.Clear(Items, 0, Items.Length); Drawn = -1;
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
                        var hand = Core.PlayerInventory.FirstItem(_camera.Find("Hand"));
                        if (origin >= 0 && origin < Items.Length && Items[origin] == null && hand != null && hand.name == _handName(run))
                        { Items[origin] = hand; Drawn = origin; }
                    }
                    return true;
                }
            }
            Core.PlayerInventory.Read(Items, out Drawn);
            for (int i = 3; i < Items.Length; i++)
                if (Items[i] == null && _camera != null) Items[i] = Core.PlayerInventory.FirstItem(_camera.Find(PocketNames[i]) ?? _camera.Find(InvNames[i]));
            return true;
        }
        /// <summary>Invalidates external delegates when the optional bridge stops or restarts.</summary>
        internal static void Clear()
        {
            _resolved=false;_instance=_enabled=null;_slots=_operation=_save=null;_ready=_loading=_normalised=null;
            _content=null;_isDrawn=null;_handOrigin=null;_handName=null;
        }
        private static readonly string[] PocketNames = { "Apocapocket_ExtraSlots/Slot 1", "Apocapocket_ExtraSlots/Slot 2", "Apocapocket_ExtraSlots/Slot 3", "Apocapocket_ExtraSlots/Slot 4", "Apocapocket_ExtraSlots/Slot 5", "Apocapocket_ExtraSlots/Slot 6" };
        private static readonly string[] InvNames = { "Apocainventory_ExtraSlots/Slot 1", "Apocainventory_ExtraSlots/Slot 2", "Apocainventory_ExtraSlots/Slot 3", "Apocainventory_ExtraSlots/Slot 4", "Apocainventory_ExtraSlots/Slot 5", "Apocainventory_ExtraSlots/Slot 6" };
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

        /// <summary>Finds the Apocapocket or Apocainventory Runner once per game session and generates the slot accessors.</summary>
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
                        if (Log.L1) Log.Main(Modules.Compatibility.ApocapocketBridge.Scope, "Equipment: logical inventory bridge to " + ns);
                        return;
                    }
                    catch (Exception e) { Log.Warn(Modules.Compatibility.ApocapocketBridge.Scope, "Equipment: inventory bridge to " + ns + " failed (" + e.Message + "), using the vanilla slots"); _instance = null; return; }
                }
        }


        /// <summary>Typed accessors for members known only by name at run time, generated once through Core <see cref="Access"/> (no
        /// FieldInfo.GetValue / MethodInfo.Invoke, no boxing per frame); a member that cannot be generated falls back to reflection.</summary>
        private static class Bridge
        {
            /// <summary>Static field as object.</summary>
            internal static Func<object> Static(FieldInfo f)
            {
                try { var get = Access.Read<object>(f); return () => get(null); }
                catch (Exception) { return () => f.GetValue(null); }
            }
            /// <summary>Instance or static field as object.</summary>
            internal static Func<object, object> Obj(FieldInfo f)
            {
                try { return Access.Read<object>(f); }
                catch (Exception) { return x => f.GetValue(x); }
            }
            /// <summary>Bool field.</summary>
            internal static Func<object, bool> Bool(FieldInfo f)
            {
                try { return Access.Read<bool>(f); }
                catch (Exception) { return x => (bool)f.GetValue(x); }
            }
            /// <summary>Bool property (instance or static).</summary>
            internal static Func<object, bool> Bool(PropertyInfo p)
            {
                try { return Access.Get<bool>(p.DeclaringType, p.Name); }
                catch (Exception) { return x => (bool)p.GetValue(x, null); }
            }
            /// <summary>Int field.</summary>
            internal static Func<object, int> Int(FieldInfo f)
            {
                try { return Access.Read<int>(f); }
                catch (Exception) { return x => (int)f.GetValue(x); }
            }
            /// <summary>String field.</summary>
            internal static Func<object, string> Str(FieldInfo f)
            {
                try { return Access.Read<string>(f); }
                catch (Exception) { return x => f.GetValue(x) as string; }
            }
            /// <summary>slots[i].Content (the element may be a class or a struct: read in place, no boxing; null element = null).</summary>
            internal static Func<object, int, GameObject> Element(Type arrayType, FieldInfo content)
            {
                try
                {
                    var element = arrayType.GetElementType();
                    using (var dm = new DynamicMethodDefinition("WW_PocketSlot_" + content.Name, typeof(GameObject), new[] { typeof(object), typeof(int) }))
                    {
                        var il = dm.GetILGenerator();
                        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, arrayType); il.Emit(OpCodes.Ldarg_1);
                        if (element.IsValueType) { il.Emit(OpCodes.Ldelema, element); il.Emit(OpCodes.Ldfld, content); }
                        else
                        {
                            var present = il.DefineLabel();
                            il.Emit(OpCodes.Ldelem_Ref); il.Emit(OpCodes.Dup); il.Emit(OpCodes.Brtrue_S, present);
                            il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Ret);
                            il.MarkLabel(present); il.Emit(OpCodes.Ldfld, content);
                        }
                        if (content.FieldType.IsValueType) il.Emit(OpCodes.Box, content.FieldType);
                        il.Emit(OpCodes.Isinst, typeof(GameObject)); il.Emit(OpCodes.Ret);
                        return (Func<object, int, GameObject>)Delegate.CreateDelegate(typeof(Func<object, int, GameObject>), dm.Generate());
                    }
                }
                catch (Exception)
                {
                    return (arr, idx) => { var slot = ((Array)arr).GetValue(idx); return slot != null ? content.GetValue(slot) as GameObject : null; };
                }
            }
            /// <summary>bool Method(int) (instance or static).</summary>
            internal static Func<object, int, bool> IntToBool(MethodInfo m)
            {
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(int) && m.ReturnType == typeof(bool))
                {
                    try { return Access.Call<Func<object, int, bool>>(m, typeof(bool), typeof(object), typeof(int)); }
                    catch (Exception) { }
                }
                var arg = new object[1];
                return (x, idx) => { arg[0] = idx; return (bool)m.Invoke(x, arg); };
            }
        }
    }
}
