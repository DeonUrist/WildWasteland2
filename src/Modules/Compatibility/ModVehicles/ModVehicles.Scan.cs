using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Third-party vehicle factories found by an assembly scan (types named Factory with Name + Template()); Compatibility part of ModVehicles (Wave A split of Patrols Vehicles/ModVehicles.cs).</summary>
    internal static partial class ModVehicles
    {
        private static Dictionary<string, MethodInfo> _byName;
        private static int _assemblyCount;

        /// <summary>Rescans when the loaded assembly count changed (or never scanned).</summary>
        internal static void Refresh() { if (_byName == null || _assemblyCount != AppDomain.CurrentDomain.GetAssemblies().Length) Scan(); }

        /// <summary>Finds third-party Factory types (static string Name + static GameObject Template()) outside WW and the absorbed mods; invalidates the prefab cache when the set changed.</summary>
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
            if (changed) Prefabs.Invalidate(); // refs-ok: REFACTOR-REQUEST B17-15
        }
    }
}
