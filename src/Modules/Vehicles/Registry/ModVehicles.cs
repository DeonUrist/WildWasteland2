using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// Template bodies that are not asset prefabs (Vehicles part of ModVehicles; the third-party assembly scan is the Compatibility part).
    /// Vehicles registers its own War Rig directly; other mods' runtime templates (a type "&lt;Namespace&gt;.Factory" with
    /// `public const string Name` and `public static GameObject Template()`) are found by the Compatibility scan. Prefabs resolves template
    /// bodies through <see cref="Template"/>.
    /// </summary>
    internal static partial class ModVehicles
    {
        private static readonly Dictionary<string, Func<GameObject>> _registered = new Dictionary<string, Func<GameObject>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Registers a runtime template body by name (invalidates the prefab index).</summary>
        internal static void Register(string name, Func<GameObject> template) { if (!string.IsNullOrEmpty(name) && template != null) { _registered[name] = template; Prefabs.Invalidate(); } }   // refs-ok: REFACTOR-REQUEST B12-3 (Patrols prefab index)

        /// <summary>Removes a registered template body (its owner stopped).</summary>
        internal static void Unregister(string name) { if (!string.IsNullOrEmpty(name) && _registered.Remove(name)) Prefabs.Invalidate(); }   // refs-ok: REFACTOR-REQUEST B12-3 (Patrols prefab index)

        /// <summary>Old Patrols shutdown call: forgets only the third-party scan; registrations belong to their owners (Vehicles keeps the War Rig while it runs).</summary>
        [Obsolete("refactor: owners Unregister their own bodies; this only resets the third-party scan")]
        internal static void Clear() { _byName = null; _assemblyCount = 0; }

        /// <summary>The runtime template of a body name: own registrations first, then the third-party scan; null when unknown or failing.</summary>
        internal static GameObject Template(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Func<GameObject> own;
            if (_registered.TryGetValue(name.Trim(), out own))
            {
                try { return own(); }
                catch (Exception e) { Log.Warn(VehiclesModule.Instance, "Mod vehicle " + name + ": " + e.Message); return null; }
            }
            Refresh();
            MethodInfo m;
            if (!_byName.TryGetValue(name.Trim(), out m)) return null;
            try { return m.Invoke(null, null) as GameObject; }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "Mod vehicle " + name + ": " + (e.InnerException ?? e).Message); return null; }
        }
    }
}
