using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using WildWasteland.Core;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Navigation side of custom POI dressing: the Nav.Make prefix that holds a custom POI back until its static props exist
    /// ("structure not ready" gate) and the Exit-marker waypoint hint used by the Nav.Next bridge.
    /// Wave A (A6): partial split of Towns/PoiDressing.cs (NPCAI part, B08: INavApi gate + exit-hint provider).
    /// </summary>
    internal sealed partial class PoiDressing
    {
        private static bool BeforeMap(Transform root)
        {
            if (_current == null || !Custom(root)) return true;
            var site = _current.Attach(root); return site == null || site.Ready;
        }
        internal static bool Exit(Vector3 position, Vector3 goal, out Vector3 waypoint)
        {
            waypoint = goal; if (_current == null) return false;
            float best = float.MaxValue;
            foreach (var site in _current.Sites)
            {
                if (site.Exits.Length == 0 || !site.Ready || site.Root == null || site.Dressing == null || !site.Dressing.activeInHierarchy) continue;
                var local = site.Root.InverseTransformPoint(position); var target = site.Root.InverseTransformPoint(goal);
                if (!site.Bounds.Contains(local) || site.Bounds.Contains(target)) continue;
                foreach (var marker in site.Exits)
                {
                    var direction = Quaternion.Euler(marker.Rotation) * Vector3.forward;
                    var point = marker.Position;
                    if (Vector3.Dot(local - point, direction) >= .5f) { if (Vector3.Distance(local, point) < 3) return false; continue; }
                    var world = site.Root.TransformPoint(point);
                    float cost = Vector3.Distance(position, world) + Vector3.Distance(world, goal);
                    if (cost >= best) continue;
                    best = cost; waypoint = site.Root.TransformPoint(point + direction * (Vector3.Distance(local, point) < 1 ? 2 : 0));
                }
            }
            return best < float.MaxValue;
        }
    }

    // Wave A (A6): moved with the partial above (README file-map: DELETE once B08 provides INavApi.ForgetStructure(root)).
    // Rare explicit authoring transaction only. Reflection is never used by prop spawning or normal frame ticks.
    internal static class PoiDressNavigation
    {
        private static readonly Type Nav = typeof(Services).Assembly.GetType("WildWasteland.Modules.NPC.Nav", true);
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object Field(string name) { return Nav.GetField(name, Static).GetValue(null); }
        internal static void Forget(Transform root)
        {
            var rows = (IList)Field("_structures"); var known = (HashSet<int>)Field("_known");
            var floor = (HashSet<int>)Field("_floorCols"); var queue = (IList)Field("_maskQueue");
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                object row = rows[i]; var type = row.GetType();
                if ((Transform)type.GetField("Root", Instance).GetValue(row) != root) continue;
                Nav.GetMethod("CloseMap", Static).Invoke(null, new[] { row });
                foreach (string name in new[] { "_baking", "_needed" }) if (ReferenceEquals(Field(name), row)) Nav.GetField(name, Static).SetValue(null, null);
                var colliders = type.GetField("MapColliders", Instance).GetValue(row) as Collider[];
                if (colliders != null) foreach (var c in colliders) if (c != null) floor.Remove(c.GetInstanceID());
                queue.Remove(row); rows.RemoveAt(i);
            }
            known.Remove(root.GetInstanceID()); Nav.GetMethod("PickReset", Static).Invoke(null, null);
            foreach (string name in new[] { "_compRing", "_outPlans", "_outMemo", "_exits", "_relaxedFields", "_debug" })
            { var value = Field(name); value.GetType().GetMethod("Clear").Invoke(value, null); }
        }
    }
}
