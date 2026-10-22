using System;
using System.Collections.Generic;
using Den.Tools;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace WildWasteland.Modules.POI
{
    // Runs only as part of MapMagic's native generation. No Unity object or physics access on the worker.
    [Serializable]
    internal sealed class PoiLocations : Generator, IMultiInlet, IOutlet<TransitionsList>, ICustomDependence
    {
        public PoiLocations() { }
        public readonly Inlet<MatrixWorld> Heights = new Inlet<MatrixWorld>();
        internal IOutlet<TransitionsList>[] Avoid = new IOutlet<TransitionsList>[0];
        internal int Chance, Seed;
        internal float Radius;
        public IEnumerable<IInlet<object>> Inlets() { yield return Heights; }
        public IEnumerable<Generator> PriorGens() { foreach (var source in Avoid) yield return source.Gen; }
        public override void Generate(TileData data, StopToken stop)
        {
            var result = new TransitionsList();
            var heights = data.ReadInletProduct(Heights);
            if (heights == null || (stop != null && stop.stop)) return;
            var area = data.area.active;
            float sizeX = area.worldSize.x, sizeZ = area.worldSize.z;
            int tx = Mathf.RoundToInt(area.worldPos.x / sizeX), tz = Mathf.RoundToInt(area.worldPos.z / sizeZ);
            var noise = new Noise(data.random, Seed);
            if (Chance <= 0 || (Chance < 100 && noise.Random(tx, tz) * 100f >= Chance))
            { data.StoreProduct(this, result); return; }
            Vector3 best = Vector3.zero; float bestCost = float.PositiveInfinity;
            // A jittered grid always supplies candidates, including at 100% debug chance.
            for (int n = 0; n < 400; n++)
            {
                if (stop != null && stop.stop) return;
                float x = area.worldPos.x + 100f + (n % 20 + noise.Random(tx, tz, n)) * (sizeX - 200f) / 20f;
                float z = area.worldPos.z + 100f + (n / 20 + noise.Random(tz, tx, n + 701)) * (sizeZ - 200f) / 20f;
                var p = new Vector3(x, 0f, z); bool blocked = false;
                foreach (var source in Avoid)
                {
                    var sites = data.ReadOutletProduct(source); if (sites == null) continue;
                    for (int i = 0; i < sites.count; i++)
                    {
                        var delta = sites.arr[i].pos - p; delta.y = 0f;
                        if (delta.sqrMagnitude < 350f * 350f) { blocked = true; break; }
                    }
                    if (blocked) break;
                }
                if (blocked) continue;
                float h = heights.GetWorldInterpolatedValue(x, z);
                float cost = 0f;
                for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
                    cost = Mathf.Max(cost, Mathf.Abs(heights.GetWorldInterpolatedValue(x + dx * Radius, z + dz * Radius) - h));
                if (cost < bestCost) { bestCost = cost; best = p; }
            }
            if (float.IsPositiveInfinity(bestCost)) throw new InvalidOperationException("No free POI pad in tile " + tx + "," + tz);
            var transition = new Transition(best.x, best.z);
            transition.Yaw = noise.Random(tx, tz, 913) * 360f;
            result.Add(transition); data.StoreProduct(this, result);
        }
    }

    [Serializable]
    internal sealed class PoiClearance : Generator, IInlet<TransitionsList>, IOutlet<TransitionsList>, ICustomDependence
    {
        public PoiClearance() { }
        internal PoiLocations[] Sites;
        public IEnumerable<Generator> PriorGens() { foreach (var site in Sites) yield return site; }
        public override void Generate(TileData data, StopToken stop)
        {
            var input = data.ReadInletProduct(this); if (input == null) return;
            var result = new TransitionsList();
            for (int i = 0; i < input.count; i++)
            {
                if (stop != null && stop.stop) return;
                bool blocked = false;
                foreach (var source in Sites)
                {
                    var sites = data.ReadOutletProduct(source); if (sites == null) continue;
                    for (int j = 0; j < sites.count; j++)
                    {
                        var delta = input.arr[i].pos - sites.arr[j].pos; delta.y = 0f;
                        if (delta.sqrMagnitude < source.Radius * source.Radius) { blocked = true; break; }
                    }
                    if (blocked) break;
                }
                if (!blocked) result.Add(input.arr[i]);
            }
            data.StoreProduct(this, result);
        }
    }
}
