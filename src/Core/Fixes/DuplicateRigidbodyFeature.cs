using System;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
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
    public sealed class DuplicateRigidbodyFeature : Feature
    {
        public override string PlanId { get { return "CT9"; } }
        public override string Key { get { return "Duplicate Rigidbody"; } }
        public override string Title { get { return "No duplicate Rigidbody adds"; } }
        public override string Category { get { return Categories.Content; } }
        public override int Order { get { return 9; } }
        public override string Help { get { return "Car parts no longer try to add a second Rigidbody on every spawn (same result, no error spam)."; } }

        internal static bool On;
        internal static long Skipped;
        static readonly System.Reflection.FieldInfo _added = AccessTools.Field(typeof(AddComponent), "addedComponent");   // SetValue: only on a skipped add

        protected override void OnEnable(FeatureMode mode) { if (!EnsurePatched(typeof(Patch))) throw new Exception("AddComponent.OnEnter patch failed"); On = true; }
        protected override void OnDisable() { On = false; }
        public override void DrawStats(ToolLayout l) { l.Value("Duplicate adds skipped", Skipped.ToString()); }

        [HarmonyPatch]
        static class Patch
        {
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
