using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    internal static partial class TrafficLocations
    {
        internal static readonly HashSet<string> Captured = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<PlayMakerFSM, bool> CapturedSpawners = new Dictionary<PlayMakerFSM, bool>();

        internal static void Capture(ref Core.Events.CampOwnershipChanged change)
        {
            CampDiscovery camp;
            if (!Known.TryGetValue(change.CampId, out camp) || string.IsNullOrEmpty(change.NewFaction)) return;
            camp.faction = change.NewFaction; camp.cleared = false; Captured.Add(camp.id);
            TrafficCamps.Captured(camp.id); Changed();
            Core.Log.Always(null, "[POI] Camp ownership: " + camp.id + " -> " + camp.faction);
        }

        internal static void SuppressCaptured(string id, Transform root)
        {
            if (!Captured.Contains(id) || root == null) return;
            foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f.FsmName != "ItemSpawner" || !(f.name.StartsWith("EnemySpawn_Scrapyard", StringComparison.Ordinal) || f.name.StartsWith("EnemySpawn_Coyotes", StringComparison.Ordinal))) continue;
                if (!CapturedSpawners.ContainsKey(f)) CapturedSpawners.Add(f, f.enabled);
                f.enabled = false;
            }
        }

        internal static void RestoreCapturedSpawners()
        {
            foreach (var pair in CapturedSpawners) if (pair.Key != null) pair.Key.enabled = pair.Value;
            CapturedSpawners.Clear();
        }
    }
}
