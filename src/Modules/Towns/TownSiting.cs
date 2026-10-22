using System;
using System.Collections.Generic;
using Den.Tools.Matrices;
using MapMagic.Products;
using UnityEngine;

namespace WildWasteland.Modules.Towns
{
    internal static class TownSiting
    {
        internal sealed class Site
        {
            internal Vector3 Center;
            internal float Relief, Cost;
            internal bool Preferred;
        }
        internal static List<Site> Find(TownPlan plan, GenerationInput input, TileData data, MatrixWorld heights,
            List<Vector3> avoid, TownWorldRoads roads, System.Random random, StopToken stop)
        {
            var result = new List<Site>(); var area = data.area.active;
            float outer = plan.Radius + plan.Feather, border = outer + 16;
            if (area.worldSize.x <= border * 2 || area.worldSize.z <= border * 2) return result;
            // A sparse square grid can miss the entire eligible road corridor.
            var points = Core.VistasWorldGen.Present ? new List<Vector3>() : roads.Sites(outer, input.AccessLength);
            for (int c = 0; c < 64; c++)
                points.Add(new Vector3(area.worldPos.x + border + (c % 8 + (float)random.NextDouble()) * (area.worldSize.x - border * 2) / 8, 0,
                    area.worldPos.z + border + (c / 8 + (float)random.NextDouble()) * (area.worldSize.z - border * 2) / 8));
            foreach (var point in points)
            {
                if (stop != null && stop.stop) throw new OperationCanceledException();
                if (point.x < area.worldPos.x + border || point.z < area.worldPos.z + border
                    || point.x > area.worldPos.x + area.worldSize.x - border || point.z > area.worldPos.z + area.worldSize.z - border) continue;
                bool occupied = false;
                foreach (var p in avoid) if (new Vector2(point.x - p.x, point.z - p.z).sqrMagnitude < (outer + 40) * (outer + 40)) { occupied = true; break; }
                if (occupied) continue;
                Vector3 road; bool hasRoad = roads.Nearest(point, input.AccessLength, out road);
                if (!hasRoad && !input.EnsureTown && !Core.VistasWorldGen.Present) continue;
                float distance = hasRoad ? new Vector2(road.x - point.x, road.z - point.z).magnitude : input.AccessLength;
                if (hasRoad && distance < outer + (Core.VistasWorldGen.Present ? 40 : 3)) continue;
                if (Core.VistasWorldGen.Present) distance = 0;
                float lo = float.PositiveInfinity, hi = float.NegativeInfinity, grade = 0;
                // Rank cheaply first. Full-resolution relief/grade is measured only for the shortlist.
                for (int z = -2; z <= 2; z++) for (int x = -2; x <= 2; x++)
                {
                    if (x * x + z * z > 4) continue;
                    float height = heights.GetWorldInterpolatedValue(point.x + x * outer * .5f, point.z + z * outer * .5f) * data.globals.height;
                    lo = Mathf.Min(lo, height); hi = Mathf.Max(hi, height);
                }
                float relief = hi - lo;
                bool preferred = relief <= input.MaxRelief && (Core.VistasWorldGen.Present || relief * .5f <= input.MaxEarthwork);
                var site = new Site { Center = new Vector3(point.x, (lo + hi) * .5f, point.z), Relief = relief,
                    Preferred = preferred, Cost = preferred ? relief + grade * 10 + distance * .004f : relief };
                float separation = Mathf.Max(40, outer * .3f);
                int nearby = result.FindIndex(s => new Vector2(s.Center.x - point.x, s.Center.z - point.z).sqrMagnitude < separation * separation);
                if (nearby >= 0)
                {
                    var old = result[nearby];
                    if (old.Preferred && !preferred || old.Preferred == preferred && old.Cost <= site.Cost) continue;
                    result.RemoveAt(nearby);
                }
                int index = result.FindIndex(s => preferred && !s.Preferred || preferred == s.Preferred && site.Cost < s.Cost);
                if (index < 0) index = result.Count;
                if (index >= 4) continue;
                result.Insert(index, site); if (result.Count > 4) result.RemoveAt(4);
            }
            if (input.Progress != null) input.Progress.Report(.15f, "Measuring the best " + result.Count + " ground sites");
            for (int i = result.Count - 1; i >= 0; i--)
            {
                if (stop != null && stop.stop) throw new OperationCanceledException();
                var site = result[i]; float lo, hi, grade;
                TownPlanner.Measure(heights, data.globals.height, site.Center.x, site.Center.z, outer, out lo, out hi, out grade);
                site.Center.y = (lo + hi) * .5f; site.Relief = hi - lo;
                site.Preferred = site.Relief <= input.MaxRelief && (Core.VistasWorldGen.Present || site.Relief * .5f <= input.MaxEarthwork) && grade <= input.MaxGrade;
                site.Cost = site.Relief;
                if (Core.VistasWorldGen.Present ? !site.Preferred || site.Relief > Mathf.Min(3, input.MaxRelief) || grade > Mathf.Min(.12f, input.MaxGrade)
                    : !site.Preferred && !input.EnsureTown) result.RemoveAt(i);
            }
            result.Sort((a, b) => a.Preferred != b.Preferred ? (a.Preferred ? -1 : 1) : a.Cost.CompareTo(b.Cost));
            return result;
        }
    }
}
