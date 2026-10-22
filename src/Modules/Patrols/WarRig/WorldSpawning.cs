using System;
using System.Collections.Generic;
using ES3Internal;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    internal static class WorldSpawning
    {
        internal static readonly List<GameObject> NaturalRigs = new List<GameObject>();
        internal static float NaturalNoticeUntil;
        private static Bounds clearance;
        private static Vector3 modelOffset;
        private static bool clearanceReady;

        internal static void ResetForScene() { NaturalRigs.Clear(); NaturalNoticeUntil = 0f; clearanceReady = false; }

        // Sample the entire tanker from above: an upward ray alone can miss a roof
        // when its origin is already inside rock. Use current world coordinates.
        internal static bool CanSpawnAt(ref Vector3 position, Quaternion rotation)
        {
            if (!clearanceReady)
            {
                clearance = Factory.Model().Bounds;
                foreach (var hull in Factory.Hulls()) clearance.Encapsulate(hull.bounds);
                clearance.center *= S.Scale;
                clearance.size = clearance.size * S.Scale + Vector3.one * .2f;
                modelOffset = Layout.Off;
                clearanceReady = true;
            }
            var modelRotation = rotation * Layout.Rot;
            var origin = position + rotation * modelOffset;
            int mask = (1 << 0) | (1 << 11) | (1 << 14);
            foreach (var terrain in Terrain.activeTerrains)
                if (terrain != null) mask |= 1 << terrain.gameObject.layer;
            float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
            for (int z = 0; z <= 10; z++)
                for (int x = -1; x <= 1; x++)
                {
                    var sample = origin + modelRotation * new Vector3(
                        Mathf.Lerp(clearance.min.x, clearance.max.x, (x + 1) * .5f), 0f,
                        Mathf.Lerp(clearance.min.z, clearance.max.z, z * .1f));
                    RaycastHit hit;
                    if (!Physics.Raycast(sample + Vector3.up * 256f, Vector3.down, out hit,
                        512f, mask, QueryTriggerInteraction.Ignore) || !(hit.collider is TerrainCollider))
                        return Reject("no exposed terrain across the full footprint");
                    lowest = Mathf.Min(lowest, hit.point.y); highest = Mathf.Max(highest, hit.point.y);
                }
            if (highest - lowest > 1.5f) return Reject("terrain varies by more than 1.5 m across the trailer");
            // Ground the full envelope before testing it, rather than treating the wagon's pivot as our ground clearance.
            var extents = clearance.extents;
            float bottom = (modelRotation * clearance.center).y -
                Mathf.Abs((modelRotation * Vector3.right).y) * extents.x -
                Mathf.Abs((modelRotation * Vector3.up).y) * extents.y -
                Mathf.Abs((modelRotation * Vector3.forward).y) * extents.z;
            position.y = highest - (rotation * modelOffset).y - bottom + .1f;
            origin = position + rotation * modelOffset;
            if (Physics.CheckBox(origin + modelRotation * clearance.center, extents,
                modelRotation, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                return Reject("full trailer clearance blocked");
            return true;
        }

        private static bool Reject(string reason)
        { if (Core.Log.Verbose) Core.Log.Info("[Patrols] War Rig placement rejected: " + reason); return false; }

        internal static bool IsTemplate(GameObject go)
        {
            var prefab = go != null ? go.GetComponent<ES3Prefab>() : null;
            return prefab != null && prefab.prefabId == Factory.PrefabId;
        }

    }
}
