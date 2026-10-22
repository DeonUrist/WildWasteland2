using System;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core.UI;

namespace WildWasteland.Core
{
    /// <summary>
    /// CT9a (plan §7, research B §3.8): CheckTag "vehPartRemoved" (Sources fsm/prefabs/bumper_10.md:L27-L34) and LockPhysics
    /// "pickup" AddComponent a Rigidbody on parts that usually still have one: Unity refuses ("can't add component ... already
    /// contains"), AddComponent.DoAddComponent stores null and logs "Can't add component" (AddComponent.cs), so every part spawn /
    /// re-activation writes two errors with stack traces. Prefix on AddComponent.OnEnter: when the requested type is exactly
    /// "UnityEngine.Rigidbody" and the target already has one, do what vanilla ends up doing without the native call and the logs:
    /// storeComponent = null, addedComponent = null, Finish(). The existing Rigidbody is deliberately NOT returned: vanilla's
    /// following SetProperty mass=50 / CCD=Continuous act on that null variable (no-ops), returning the body would change mass and CCD.
    /// No WW module patches AddComponent (grep src/, 2026-10-15).
    /// </summary>
    internal static class DuplicateRigidbodyFix
    {
        internal static bool On;
        internal static long Skipped;
        static readonly System.Reflection.FieldInfo _added = AccessTools.Field(typeof(AddComponent), "addedComponent");   // SetValue: only on a skipped add

        /// <summary>Installs the exact duplicate-add guard under the optimization owner once.</summary>
        internal static bool Install(Optimization.OptimizationRuntime owner)
        {
            return owner.EnsurePatched(Patches.Method(typeof(AddComponent), "OnEnter"), Patches.Method(typeof(Patch), "Pre"), null, null, null);
        }
        /// <summary>Enables the guard after installation succeeds.</summary>
        internal static void Enable() { On = true; }
        /// <summary>Restores the original AddComponent behavior while leaving the shared patch inert.</summary>
        internal static void Remove() { On = false; }

        [HarmonyPatch]
        static class Patch
        {
            /// <summary>AddComponent.OnEnter prefix (every AddComponent action enter): length-gated "UnityEngine.Rigidbody" check; when the target already has one, finish like vanilla's failed add without the native call and error logs.</summary>
            [HarmonyPatch(typeof(AddComponent), "OnEnter"), HarmonyPrefix]
            static bool Pre(AddComponent __instance)
            {
                if (!On) return true;
                var c = __instance.component;
                if (c == null) return true;
                string n = c.Value;
                if (n == null || n.Length != 21 || n != "UnityEngine.Rigidbody") return true;
                var go = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
                if (go == null || go.GetComponent<Rigidbody>() == null) return true;
                // vanilla: AddComponent returns null -> addedComponent = null, storeComponent.Value = null, LogError, Finish
                if (_added != null) _added.SetValue(__instance, null);
                if (__instance.storeComponent != null) __instance.storeComponent.Value = null;
                Skipped++;
                __instance.Finish();
                return false;
            }
        }
    }
}

namespace WildWasteland.Core.Optimization
{
    /// <summary>Optimization setting adapter for Core's exact duplicate-Rigidbody fix.</summary>
    public sealed class DuplicateRigidbodyFeature : Feature
    {
        /// <summary>Existing plan identifier.</summary>
        public override string PlanId { get { return "CT9"; } }
        /// <summary>Existing setting key.</summary>
        public override string Key { get { return "Duplicate Rigidbody"; } }
        /// <summary>Feature display title.</summary>
        public override string Title { get { return "No duplicate Rigidbody adds"; } }
        /// <summary>Original content category.</summary>
        public override string Category { get { return Categories.Content; } }
        /// <summary>Original feature order.</summary>
        public override int Order { get { return 9; } }
        /// <summary>Explains the exact failure-path preservation.</summary>
        public override string Help { get { return "Car parts no longer try to add a second Rigidbody on every spawn (same result, no error spam)."; } }
        /// <summary>Installs the Core fix once and activates its guard.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            if (!DuplicateRigidbodyFix.Install(Module)) throw new Exception("AddComponent.OnEnter patch failed");
            DuplicateRigidbodyFix.Enable();
        }
        /// <summary>Returns the fix to its inert original-behavior path.</summary>
        protected override void OnDisable() { DuplicateRigidbodyFix.Remove(); }
        /// <summary>Shows skipped duplicate adds from the Core fix counter.</summary>
        public override void DrawStats(ToolLayout layout) { layout.Value("Duplicate adds skipped", DuplicateRigidbodyFix.Skipped.ToString()); }
    }
}
