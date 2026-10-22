using System;
using System.Linq;
using System.Reflection;
using ES3Internal;
using HarmonyLib;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    internal static class MotorcycleIntegration
    {
        internal const string Body = "Motorcycle";
        // WW: the ES3 prefab lookup is the shared Core.Hooks.GetPrefab dispatcher (keyed by id); MotorcycleMod.Factory.Template is a
        // module patch (third-party, optional)
        internal static void Install(Core.IModule owner)
        {
            Core.Hooks.GetPrefab.Resolver(owner, ResolvePrefab);
            // The optional standalone mod owns this same save identity. Fix its chassis too.
            var external = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "MotorcycleMod");
            var factory = external != null ? external.GetType("MotorcycleMod.Factory") : null;
            var method = factory != null ? factory.GetMethod("Template", BindingFlags.Static | BindingFlags.Public) : null;
            if (method != null) Core.Patches.Patch(owner, method, postfix: AccessTools.Method(typeof(MotorcycleIntegration), nameof(PrepareRearAccess)));
        }
        internal static GameObject Template()
        {
            // If the original mod is present, share its prefab/settings and existing save identity.
            var external = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "MotorcycleMod");
            var factory = external != null ? external.GetType("MotorcycleMod.Factory") : null;
            var method = factory != null ? factory.GetMethod("Template", BindingFlags.Static | BindingFlags.Public) : null;
            if (method != null) return (GameObject)method.Invoke(null, null);
            return Motorcycles.Factory.Template();
        }
        private static void PrepareRearAccess(GameObject __result)
        {
            if (__result == null) return;
            TrimRearCollision(__result);
            if (__result.GetComponent<MotorcycleRearAccess>() == null) __result.AddComponent<MotorcycleRearAccess>();
        }
        internal static void TrimRearCollision(GameObject root)
        {
            var rear = root.transform.Find("hinge_wheel_M");
            var body = root.transform.Find("parts/model/colliders_bike");
            if (rear == null || body == null) return;
            // The inherited Crossbreed fender boxes extend behind its 28 cm motorcycle tyre.
            // Work in chassis coordinates, including each box's rotation, while the prefab is inactive.
            float edge = root.transform.InverseTransformPoint(rear.position).z - .28f;
            foreach (var box in body.GetComponentsInChildren<BoxCollider>(true))
            {
                float minZ = float.MaxValue;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var corner = box.center + Vector3.Scale(box.size * .5f, new Vector3(x, y, z));
                    minZ = Mathf.Min(minZ, root.transform.InverseTransformPoint(box.transform.TransformPoint(corner)).z);
                }
                if (minZ < edge) box.enabled = false;
            }
        }
        private static ES3Prefab ResolvePrefab(long id)
        {
            if (id != Motorcycles.Factory.PrefabId) return null;
            return Template().GetComponent<ES3Prefab>();
        }
        internal static PlayMakerFSM FindFsm(GameObject go, string name)
        {
            return go == null ? null : go.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        internal static bool IsBody(string name) { return string.Equals(name, Body, StringComparison.OrdinalIgnoreCase); }
    }
    // Easy Save can restore old collider-enabled values after instantiating the corrected prefab.
    internal sealed class MotorcycleRearAccess : MonoBehaviour
    {
        private void OnEnable() { MotorcycleIntegration.TrimRearCollision(gameObject); }
        private void Start() { MotorcycleIntegration.TrimRearCollision(gameObject); }
    }
}
