using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>MotorcycleMod (optional standalone) bridge of the motorcycle chassis: its Factory.Template is used when present and postfixed so its chassis gets the rear collider trim; Compatibility part (Wave A split of Patrols Vehicles/MotorcycleIntegration.cs).</summary>
    internal static partial class MotorcycleIntegration
    {
        /// <summary>MotorcycleMod.Factory.Template when the MotorcycleMod assembly is loaded, else null (assembly scan on every call, as before).</summary>
        private static MethodInfo MotorcycleModTemplate()
        {
            var external = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "MotorcycleMod");
            var factory = external != null ? external.GetType("MotorcycleMod.Factory") : null;
            return factory != null ? factory.GetMethod("Template", BindingFlags.Static | BindingFlags.Public) : null;
        }

        /// <summary>The optional standalone mod owns this same save identity: postfix its Factory.Template so its chassis is fixed too.</summary>
        private static void PatchMotorcycleMod(Core.IModule owner)
        {
            var method = MotorcycleModTemplate();
            if (method != null) Core.Patches.Patch(owner, method, postfix: AccessTools.Method(typeof(MotorcycleIntegration), nameof(PrepareRearAccess)));
        }

        /// <summary>Postfix of MotorcycleMod.Factory.Template: trims the rear colliders and adds the MotorcycleRearAccess component to its chassis.</summary>
        private static void PrepareRearAccess(GameObject __result)
        {
            if (__result == null) return;
            TrimRearCollision(__result);
            if (__result.GetComponent<MotorcycleRearAccess>() == null) __result.AddComponent<MotorcycleRearAccess>();
        }
    }
}
