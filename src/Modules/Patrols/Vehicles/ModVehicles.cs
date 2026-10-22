using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // Vehicles and trailers added by other mods (War Rig, ...) are runtime templates, not asset prefabs, so the asset
    // scans don't see them. Convention: the mod has a type "<Namespace>.Factory" with `public const string Name` (the prefab
    // name its instances are cloned as, e.g. "WarRig") and `public static GameObject Template()`.
    // WW: the module's own War Rig registers directly (Register); the reflection scan stays for third-party assemblies and skips the
    // WildWasteland assembly (its own Factory types: the motorcycle chassis and War Rig) and the standalone mods it replaces.
    internal static class ModVehicles
    {
        private static readonly Dictionary<string, Func<GameObject>> _registered = new Dictionary<string, Func<GameObject>>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, MethodInfo> _byName;
        private static int _assemblyCount;

        internal static void Register(string name, Func<GameObject> template) { if (!string.IsNullOrEmpty(name) && template != null) { _registered[name] = template; Prefabs.Invalidate(); } }
        internal static void Clear() { _registered.Clear(); _byName = null; _assemblyCount = 0; }
        internal static void Refresh() { if (_byName == null || _assemblyCount != AppDomain.CurrentDomain.GetAssemblies().Length) Scan(); }

        internal static GameObject Template(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Func<GameObject> own;
            if (_registered.TryGetValue(name.Trim(), out own))
            {
                try { return own(); }
                catch (Exception e) { Core.Log.Warn("[Patrols] " + "Mod vehicle " + name + ": " + e.Message); return null; }
            }
            Refresh();
            MethodInfo m;
            if (!_byName.TryGetValue(name.Trim(), out m)) return null;
            try { return m.Invoke(null, null) as GameObject; }
            catch (Exception e) { Core.Log.Warn("[Patrols] " + "Mod vehicle " + name + ": " + (e.InnerException ?? e).Message); return null; }
        }

        private static void Scan()
        {
            var previous = _byName;
            _byName = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            _assemblyCount = assemblies.Length;
            var self = typeof(ModVehicles).Assembly;
            foreach (var asm in assemblies)
            {
                if (asm == self) continue;
                string an = asm.GetName().Name;
                if (an == "WildWasteland" || an == "Apocapatrol" || an == "WarRig" || an == "MotorcycleMod" || an.StartsWith("Unity") || an.StartsWith("System") || an == "mscorlib" || an == "Assembly-CSharp") continue;
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types; } catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.Name != "Factory") continue;
                    var nameField = t.GetField("Name", BindingFlags.Public | BindingFlags.Static);
                    var template = t.GetMethod("Template", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                    if (nameField == null || template == null || template.ReturnType != typeof(GameObject)) continue;
                    var n = nameField.GetValue(null) as string;
                    if (string.IsNullOrEmpty(n) || _byName.ContainsKey(n) || _registered.ContainsKey(n)) continue;
                    _byName[n] = template;
                    Core.Log.Info("[Patrols] " + "Mod vehicle available for templates: " + n + " (" + an + ")");
                }
            }
            bool changed = previous == null ? _byName.Count > 0 : previous.Count != _byName.Count;
            if (!changed && previous != null)
                foreach (var pair in _byName)
                { MethodInfo old; if (!previous.TryGetValue(pair.Key, out old) || old != pair.Value) { changed = true; break; } }
            if (changed) Prefabs.Invalidate();
        }
    }
}
