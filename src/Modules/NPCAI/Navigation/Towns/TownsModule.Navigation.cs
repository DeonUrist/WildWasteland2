using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MapMagic.Terrains;
using Den.Tools;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// NPC town routing hook answered for the Nav.Next bridge: outside-wall detours, gate entrances, street graph, walker gates.
    /// Wave A (A6): entry split of Towns/TownsModule.cs (NPCAI/Navigation part, B08: becomes an IWaypointProvider registration).
    /// </summary>
    public sealed partial class TownsModule
    {
        internal bool Waypoint(GameObject owner, Vector3 position, Vector3 goal, out Vector3 waypoint, out bool approach)
        {
            waypoint = goal; approach = false;
            for (int i = 0; i < _sites.Count; i++)
            {
                var site = _sites[i].Site;
                if (site.Anchor == null || site.Approach == null) continue;
                var center = site.Anchor.TransformPoint(site.Plan.WorldCenter);
                float radius = site.Plan.Radius + site.Plan.Feather + 30f;
                float px = position.x - center.x, pz = position.z - center.z, gx = goal.x - center.x, gz = goal.z - center.z;
                if (px * px + pz * pz > radius * radius && gx * gx + gz * gz > radius * radius)
                {
                    float dx = gx - px, dz = gz - pz, t = Mathf.Clamp01(-(px * dx + pz * dz) / Mathf.Max(.0001f, dx * dx + dz * dz));
                    float x = px + dx * t, z = pz + dz * t; if (x * x + z * z > radius * radius) continue;
                }
                var local = site.Anchor.InverseTransformPoint(position) - site.Plan.WorldCenter;
                if (!site.Approach.Inside(local))
                {
                    var target = site.Anchor.InverseTransformPoint(goal) - site.Plan.WorldCenter;
                    if (!site.Approach.Inside(target))
                    {
                        Vector3 detour; if (site.Walls == null || !site.Approach.Around(owner, local, target, out detour)) continue;
                        waypoint = site.Ground(site.Anchor.TransformPoint(site.Plan.WorldCenter + detour));
                        approach = true; return true;
                    }
                    waypoint = site.Ground(site.Anchor.TransformPoint(site.Plan.WorldCenter + site.Approach.Entrance(local)));
                    approach = true; return true;
                }
                if (site.Navigation != null && site.Navigation.Ready && site.Navigation.Waypoint(position, goal, out waypoint)) return true;
                bool hold; Vector3 next;
                if (site.Walls != null && TownBakeBridge.Root(position) == null && site.Walls.Walkers.Waypoint(site.Anchor.InverseTransformPoint(position) - site.Plan.WorldCenter,
                    site.Anchor.InverseTransformPoint(goal) - site.Plan.WorldCenter, out next, out hold))
                { waypoint = site.Ground(site.Anchor.TransformPoint(site.Plan.WorldCenter + next)); return true; }
            }
            return false;
        }
        /// <summary>Installs the NPC Nav bridge (Nav.Next waypoint prefix/postfix, instance overlays).</summary>
        partial void InitNavigation() { TownBakeBridge.Install(this); }
        /// <summary>Removes the NPC Nav bridge (unpatches this module, drops the resolved Nav delegates).</summary>
        partial void ShutdownNavigation() { TownBakeBridge.Uninstall(this); }
    }
}
