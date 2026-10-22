using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    /// <summary>
    /// Navigation side of custom POI dressing: the Nav.Make prefix that holds a custom POI back until its static props exist
    /// ("structure not ready" gate) and the Exit-marker waypoint hint used by the Nav.Next bridge.
    /// Wave A (A6): partial split of Towns/PoiDressing.cs (NPCAI part, B08: INavApi gate + exit-hint provider).
    /// </summary>
    internal sealed partial class PoiDressing
    {
        /// <summary>The make gate as a delegate (registered with Nav by TownBakeBridge.Install).</summary>
        internal static readonly System.Func<Transform, bool> MapGate = BeforeMap;

        /// <summary>Holds a custom POI's structure back until its static dressing exists (false = not now). Still Harmony-patched onto Nav.Make
        /// by POI PoiDressing.Install (request B08-5: drop that patch, the gate is registered through Nav.AddMakeGate).</summary>
        private static bool BeforeMap(Transform root)
        {
            if (_current == null || !Custom(root)) return true;
            var site = _current.Attach(root); return site == null || site.Ready;
        }
        /// <summary>Waypoint hint: the cheapest exit marker of the ready custom POI the NPC stands in when its goal lies outside it.</summary>
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

    /// <summary>Forgets the navigation structure of a custom POI being (re)dressed (rare authoring transaction): <see cref="Nav.Forget"/>.
    /// Wave A (A6) moved it here; README file-map: delete once POI calls INavApi.ForgetStructure (request B08-5).</summary>
    internal static class PoiDressNavigation
    {
        /// <summary>Drops root's structure; the sweep / Discover maps it again once the dressing is ready.</summary>
        [System.Obsolete("refactor: use ModApi.Get<INavApi>().ForgetStructure(root)")]
        internal static void Forget(Transform root) { ModApi.Get<INavApi>()?.ForgetStructure(root); }
    }
}
