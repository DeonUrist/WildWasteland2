using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Owns optional external motorcycle detection and the external factory postfix.</summary>
    internal sealed class ExternalMotorcycle : Core.IExternalMotorcycle
    {
        private readonly MethodInfo _template;
        /// <summary>Resolves the shared external factory once for this Compatibility lifetime.</summary>
        internal ExternalMotorcycle(Core.IModule owner)
        {
            var external=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="MotorcycleMod");
            var factory=external?.GetType("MotorcycleMod.Factory");
            _template=factory?.GetMethod("Template",BindingFlags.Static|BindingFlags.Public);
            if(_template!=null)Core.Patches.Patch(owner,_template,postfix:AccessTools.Method(typeof(ExternalMotorcycle),nameof(Prepared)));
        }
        /// <summary>Returns the external template when present, preserving its prefab/save identity.</summary>
        public GameObject Template() { return _template!=null?(GameObject)_template.Invoke(null,null):null; }
        /// <summary>Passes real external results to the current Vehicles preparation provider.</summary>
        private static void Prepared(GameObject __result) { Core.ModApi.Get<Core.IMotorcyclePreparation>()?.Prepare(__result); }
    }
}
