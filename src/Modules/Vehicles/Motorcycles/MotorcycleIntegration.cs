using System;
using System.Reflection;
using ES3Internal;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Motorcycle chassis integration (Vehicles part; the MotorcycleMod bridge is the Compatibility part): ES3 prefab resolver, template, rear collider trim, body name.</summary>
    internal static class MotorcycleIntegration
    {
        internal const string Body = "Motorcycle";

        /// <summary>Old Patrols entry point; the Vehicles module installs the resolver itself (<see cref="Hook"/>).</summary>
        [Obsolete("refactor: the Vehicles module installs the motorcycle resolver (MotorcycleIntegration.Hook)")]

        /// <summary>Installs the ES3 prefab-id resolver (shared Core.Hooks.GetPrefab dispatcher) and the optional MotorcycleMod template patch.</summary>
        internal static void Hook(IModule owner)
        {
            Hooks.GetPrefab.Resolver(owner, ResolvePrefab);
        }

        /// <summary>
        /// The motorcycle chassis template: the standalone MotorcycleMod's when it is loaded (shared prefab and save identity), else ours.
        /// README 8 #5: the MotorcycleMod lookup (an assembly scan) runs once per session instead of on every call.
        /// </summary>
        internal static GameObject Template()
        {
            var external=ModApi.Get<IExternalMotorcycle>()?.Template();
            if(external!=null)return external;
            return Motorcycles.Factory.Template();
        }

        /// <summary>Disables the inherited Crossbreed fender boxes that reach behind the 28 cm motorcycle tyre (chassis coordinates, inactive prefab).</summary>
        internal static void TrimRearCollision(GameObject root)
        {
            var rear = root.transform.Find("hinge_wheel_M");
            var body = root.transform.Find("parts/model/colliders_bike");
            if (rear == null || body == null) return;
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

        /// <summary>GetPrefab resolver: the motorcycle template's ES3Prefab for prefab id 735710202610050001, otherwise null.</summary>
        private static ES3Prefab ResolvePrefab(long id)
        {
            if (id != Motorcycles.Factory.PrefabId) return null;
            return Template().GetComponent<ES3Prefab>();
        }

        /// <summary>First PlayMaker FSM of that name on the object (case-insensitive), or null.</summary>
        internal static PlayMakerFSM FindFsm(GameObject go, string name)
        {
            if (go == null) return null;
            foreach (var f in go.GetComponents<PlayMakerFSM>()) if (f.FsmName.Equals(name, StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }

        /// <summary>True for the motorcycle body name of templates ("Motorcycle", any case).</summary>
        internal static bool IsBody(string name) { return string.Equals(name, Body, StringComparison.OrdinalIgnoreCase); }
    }
}
