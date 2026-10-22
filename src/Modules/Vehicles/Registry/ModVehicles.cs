using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// Template bodies that are not asset prefabs (Vehicles part of ModVehicles; the third-party assembly scan is the Compatibility part).
    /// Vehicles registers its own War Rig directly; other mods' runtime templates (a type "&lt;Namespace&gt;.Factory" with
    /// `public const string Name` and `public static GameObject Template()`) are found by the Compatibility scan. Prefabs resolves template
    /// bodies through <see cref="Template"/>.
    /// </summary>
    internal static class ModVehicles
    {
        private static readonly Dictionary<string, Func<GameObject>> _registered = new Dictionary<string, Func<GameObject>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Registers a runtime template body by name (invalidates the prefab index).</summary>
        internal static void Register(string name, Func<GameObject> template) { if (!string.IsNullOrEmpty(name) && template != null) { _registered[name] = template; Core.PrefabCatalog.Invalidate(); } }

        /// <summary>Removes a registered template body (its owner stopped).</summary>
        internal static void Unregister(string name) { if (!string.IsNullOrEmpty(name) && _registered.Remove(name)) Core.PrefabCatalog.Invalidate(); }

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
            return _external?.Template(name);
        }
        private static IExternalVehicleTemplates _external;
        private static int _externalGeneration=-1;
        /// <summary>Invalidates the local prefab index when the real external provider or discovered set changes.</summary>
        internal static void Refresh()
        {
            var external=ModApi.Get<IExternalVehicleTemplates>();int generation=external!=null?external.Generation:-1;
            if(!ReferenceEquals(external,_external)||generation!=_externalGeneration){_external=external;_externalGeneration=generation;Core.PrefabCatalog.Invalidate();}
        }
    }
}
