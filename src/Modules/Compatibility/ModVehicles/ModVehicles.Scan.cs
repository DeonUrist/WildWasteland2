using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Third-party vehicle factories found by an assembly scan (types named Factory with Name + Template()); Compatibility part of ModVehicles (Wave A split of Patrols Vehicles/ModVehicles.cs).</summary>
    internal sealed class ExternalVehicleTemplates : Core.IExternalVehicleTemplates
    {
        private int _generation;
        /// <summary>Refreshes external discoveries and exposes a numeric invalidation token.</summary>
        public int Generation { get { Refresh();return _generation; } }
        /// <summary>Invokes the external factory only when a caller requests its exact registered name.</summary>
        public GameObject Template(string name)
        {
            Refresh();MethodInfo method;if(string.IsNullOrEmpty(name)||!_byName.TryGetValue(name.Trim(),out method))return null;
            try{return method.Invoke(null,null) as GameObject;}catch(Exception e){Core.Log.Warn(CompatibilityModule.Instance,"External vehicle "+name+": "+(e.InnerException??e).Message);return null;}
        }
        private Dictionary<string, MethodInfo> _byName;
        private int _assemblyCount;

        /// <summary>Rescans when the loaded assembly count changed (or never scanned).</summary>
        private void Refresh() { if (_byName == null || _assemblyCount != AppDomain.CurrentDomain.GetAssemblies().Length) Scan(); }

        /// <summary>Finds third-party Factory types (static string Name + static GameObject Template()) outside WW and the absorbed mods; invalidates the prefab cache when the set changed.</summary>
        private void Scan()
        {
            var previous = _byName;
            _byName = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            _assemblyCount = assemblies.Length;
            var self = typeof(ExternalVehicleTemplates).Assembly;
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
                    if (string.IsNullOrEmpty(n) || _byName.ContainsKey(n)) continue;
                    _byName[n] = template;
                    if (Core.Log.L1) Core.Log.Main(CompatibilityModule.Instance, "Mod vehicle available for templates: " + n + " (" + an + ")");
                }
            }
            bool changed = previous == null ? _byName.Count > 0 : previous.Count != _byName.Count;
            if (!changed && previous != null)
                foreach (var pair in _byName)
                { MethodInfo old; if (!previous.TryGetValue(pair.Key, out old) || old != pair.Value) { changed = true; break; } }
            if (changed) _generation++;
        }
    }
}
