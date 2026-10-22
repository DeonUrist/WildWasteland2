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
    internal static partial class ModVehicles
    {
        private static readonly Dictionary<string, Func<GameObject>> _registered = new Dictionary<string, Func<GameObject>>(StringComparer.OrdinalIgnoreCase);

        internal static void Register(string name, Func<GameObject> template) { if (!string.IsNullOrEmpty(name) && template != null) { _registered[name] = template; Prefabs.Invalidate(); } }
        internal static void Clear() { _registered.Clear(); _byName = null; _assemblyCount = 0; }

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

    }
}
