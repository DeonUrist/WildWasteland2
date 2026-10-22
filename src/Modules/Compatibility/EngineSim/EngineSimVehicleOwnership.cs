using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Provides Engine Sim ownership using component identities resolved once at bridge activation.</summary>
    internal sealed class EngineSimVehicleOwnership : IVehicleOwnerQuery
    {
        private readonly HashSet<Type> _types = new HashSet<Type>();
        private readonly List<MonoBehaviour> _components = new List<MonoBehaviour>();

        /// <summary>Resolves the installed mod's vehicle component types without a version restriction.</summary>
        internal static EngineSimVehicleOwnership Create()
        {
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.burner.apocalypter.enginesim", out info) || ReferenceEquals(info.Instance, null)) return null;
            Type[] candidates;
            try { candidates = info.Instance.GetType().Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException error) { candidates = error.Types; }
            var query = new EngineSimVehicleOwnership();
            for (int i = 0; i < candidates.Length; i++)
            {
                var type = candidates[i];
                if (type != null && type.Namespace != null && type.Namespace.StartsWith("EngineSim", StringComparison.Ordinal)
                    && typeof(MonoBehaviour).IsAssignableFrom(type)) query._types.Add(type);
            }
            return query._types.Count == 0 ? null : query;
        }

        /// <summary>Checks the queried vehicle hierarchy against cached component types, including inactive components.</summary>
        public bool Owns(GameObject vehicle)
        {
            if (vehicle == null) return false;
            _components.Clear();
            vehicle.GetComponentsInChildren(true, _components);
            bool owned = false;
            for (int i = 0; i < _components.Count; i++)
            {
                var component = _components[i];
                if (component != null && _types.Contains(component.GetType())) { owned = true; break; }
            }
            _components.Clear();
            return owned;
        }
    }
}
