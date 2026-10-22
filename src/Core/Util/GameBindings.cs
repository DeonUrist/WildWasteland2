using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using InsaneSystems.InputManager;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// The game's rebindable actions (InsaneSystems InputStorage) by name. Superset of PlayerBody GameBindings with a cache:
    /// the old Find walked InputStorage.Keys on every call (Down() runs per frame); here a name -> KeyAction map is kept per
    /// InputStorage instance and key-list size, rebuilt when either changes and cleared on scene load, so per-frame lookups do
    /// not allocate. Name matching is case-insensitive like the original.
    /// </summary>
    public static class GameBindings
    {
        static readonly string[] IgnitionNames = { "Ignition", "Start Engine", "Start/Stop Engine" };
        static readonly Dictionary<string, KeyAction> _byName = new Dictionary<string, KeyAction>(StringComparer.OrdinalIgnoreCase);
        static InputStorage _storage;
        static object _keys;
        static int _count = -1;

        /// <summary>The action named name (case-insensitive), or null when the storage is not loaded or has no such action.</summary>
        public static KeyAction Find(string name)
        {
            if (name == null) return null;
            try
            {
                var storage = InputStorage.Singleton;
                if (storage == null || storage.Keys == null) { if (_storage != null) Clear(); return null; }
                var keys = storage.Keys;
                if (!ReferenceEquals(storage, _storage) || !ReferenceEquals(keys, _keys) || keys.Count != _count) Rebuild(storage);
                KeyAction a;
                if (_byName.TryGetValue(name, out a))
                {
                    if (a != null && string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) return a;
                    Rebuild(storage);   // renamed in place: rebuild once
                    return _byName.TryGetValue(name, out a) ? a : null;
                }
            }
            catch { }
            return null;
        }

        /// <summary>Re-indexes storage.Keys (first action wins for duplicate names, as the old linear search).</summary>
        static void Rebuild(InputStorage storage)
        {
            _byName.Clear();
            _storage = storage; _keys = storage.Keys; _count = storage.Keys.Count;
            foreach (var action in storage.Keys)
                if (action != null && action.Name != null && !_byName.ContainsKey(action.Name)) _byName[action.Name] = action;
        }

        /// <summary>Drops the cache (scene load, or when another mod rebuilds the key list in place).</summary>
        public static void Clear() { _byName.Clear(); _storage = null; _keys = null; _count = -1; }

        /// <summary>Input.GetKeyDown(key) unless None (NoInlining: keeps the legacy Input call out of the caller's body, as the original).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool KeyDown(KeyCode key) { return key != KeyCode.None && Input.GetKeyDown(key); }

        /// <summary>Input.GetKey(key) unless None.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool KeyHeld(KeyCode key) { return key != KeyCode.None && Input.GetKey(key); }

        /// <summary>The action's main or alternative key went down this frame; fallback key when the action does not exist.</summary>
        public static bool Down(string name, KeyCode fallback)
        {
            var action = Find(name);
            return action != null ? KeyDown(action.Key) || KeyDown(action.AlternativeKey) : KeyDown(fallback);
        }

        /// <summary>The action's main or alternative key is held; fallback key when the action does not exist.</summary>
        public static bool Held(string name, KeyCode fallback)
        {
            var action = Find(name);
            return action != null ? KeyHeld(action.Key) || KeyHeld(action.AlternativeKey) : KeyHeld(fallback);
        }

        /// <summary>Display label "Main / Alt" of the action (or of fallback). Allocates: UI only.</summary>
        public static string Label(string name, KeyCode fallback)
        {
            var action = Find(name);
            if (action == null) return KeyName(fallback);
            string main = KeyName(action.Key), alt = KeyName(action.AlternativeKey);
            return string.IsNullOrEmpty(main) ? alt : string.IsNullOrEmpty(alt) || main == alt ? main : main + " / " + alt;
        }

        /// <summary>The name of the game's ignition action ("Ignition", "Start Engine" or "Start/Stop Engine"; default "Ignition").</summary>
        public static string IgnitionAction
        {
            get
            {
                foreach (var name in IgnitionNames) if (Find(name) != null) return name;
                return "Ignition";
            }
        }

        /// <summary>Short key label: "" for None, "+" for Equals/Plus, "-" for Minus, digits without "Alpha".</summary>
        public static string KeyName(KeyCode key)
        {
            if (key == KeyCode.None) return "";
            if (key == KeyCode.Equals || key == KeyCode.Plus) return "+";
            if (key == KeyCode.Minus) return "-";
            string name = key.ToString();
            return name.StartsWith("Alpha", StringComparison.Ordinal) ? name.Substring(5) : name;
        }
    }
}
