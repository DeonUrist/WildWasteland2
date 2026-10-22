using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    internal static class TownRoadBridge
    {
        private static readonly Dictionary<string, Vector3[]> Access = new Dictionary<string, Vector3[]>(StringComparer.Ordinal);
        private static Vector3[][] _lines = new Vector3[0][];
        private static ConfigEntry<bool> _enabled;
        internal static void Install(TownsModule owner)
        {
            var assembly = typeof(Services).Assembly;
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var vehicle = assembly.GetType("WildWasteland.Modules.Patrols.VehicleRoute", true);
            var foot = assembly.GetType("WildWasteland.Modules.Patrols.TerrainWaypoints", true);
            var planner = assembly.GetType("WildWasteland.Modules.Patrols.RoadPlanner", true);
            _enabled = (ConfigEntry<bool>)assembly.GetType("WildWasteland.Modules.Patrols.PatrolsModule", true).GetField("TrafficRoadRouting", flags).GetValue(null);
            var lines = typeof(TownRoadBridge).GetMethod("Lines", flags);
            var hints = typeof(TownRoadBridge).GetMethod("Hints", flags);
            if (!Patches.Patch(owner, Patches.Method(vehicle, "Build"), prefix: lines)
                || !Patches.Patch(owner, Patches.Method(vehicle, "Revalidate"), prefix: lines)
                || !Patches.Patch(owner, Patches.Method(foot, "Build"), prefix: hints)
                || !Patches.Patch(owner, Patches.Method(planner, "Build"), prefix: hints))
                throw new InvalidOperationException("Patrols town access-road bridge could not be installed.");
        }
        internal static void Add(TownPlan plan)
        {
            if (plan.AccessPath.Length < 2 || Access.ContainsKey(plan.Key)) return;
            // Only the outside access road is a traffic hint. Narrow paths between buildings
            // belong to NPC navigation and must not attract vehicles through town interiors.
            var points = new List<Vector3>();
            for (int i = 0; i < plan.AccessPath.Length; i++)
                if (new Vector2(plan.AccessPath[i].x, plan.AccessPath[i].z).magnitude >= plan.Radius + 2) points.Add(plan.World(plan.AccessPath[i]));
            if (points.Count < 2) return;
            Access.Add(plan.Key, points.ToArray());
            var all = new Vector3[Access.Count][]; Access.Values.CopyTo(all, 0); Volatile.Write(ref _lines, all);
            Log.Always("[Towns] access road published after native roads: " + plan.Key + "; " + points.Count + " points for Patrols; native road cache unchanged.");
        }
        internal static void Clear() { Access.Clear(); Volatile.Write(ref _lines, new Vector3[0][]); }
        internal static void Remove(string key)
        { Access.Remove(key); var all = new Vector3[Access.Count][]; Access.Values.CopyTo(all, 0); Volatile.Write(ref _lines, all); }
        private static void Lines(ref Vector3[][] lines) { lines = Append(lines); }
        private static void Hints(ref Vector3[][] hints) { hints = Append(hints); }
        private static Vector3[][] Append(Vector3[][] original)
        {
            var extra = Volatile.Read(ref _lines);
            if (!_enabled.Value || extra.Length == 0 || original.Length > 0 && ReferenceEquals(original[original.Length - 1], extra[extra.Length - 1])) return original;
            var result = new Vector3[original.Length + extra.Length][];
            Array.Copy(original, result, original.Length); Array.Copy(extra, 0, result, original.Length, extra.Length); return result;
        }
    }
}
