using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    // Read-only template data from the existing baker. Never written by Towns.
    internal sealed class TownBakeData
    {
        internal int W, H;
        internal float Cell;
        internal Vector3 Min;
        internal float[] Floor;
        internal byte[] Why, Sky, Edges;
        internal string Source;
        [StructLayout(LayoutKind.Explicit)]
        private struct Bits { [FieldOffset(0)] internal float F; [FieldOffset(0)] internal uint U; }
        private static uint Sum(uint sum, float y, byte why, byte sky, byte edge)
        { unchecked { sum = (sum ^ new Bits { F = y }.U) * 16777619; sum = (sum ^ why) * 16777619; sum = (sum ^ sky) * 16777619; return (sum ^ edge) * 16777619; } }
        internal static string LastMiss = "";
        internal static TownBakeData Load(BuildingSpec spec, float margin, float cell)
        {
            // exact maps first; then (WW) a map of the same grid whose collider count differs (loose / dynamic props counted
            // differently by the runtime baker): the footprint, floors and walls are the same grid
            var map = Load(spec, margin, cell, true);
            return map ?? Load(spec, margin, cell, false);
        }
        private static TownBakeData Load(BuildingSpec spec, float margin, float cell, bool strict)
        {
            cell = Mathf.Max(cell, Mathf.Max(spec.HalfX * 2 + margin * 2, spec.HalfZ * 2 + margin * 2) / 600f);
            int w = Mathf.Max(2, Mathf.CeilToInt((spec.HalfX * 2 + margin * 2) / cell));
            int h = Mathf.Max(2, Mathf.CeilToInt((spec.HalfZ * 2 + margin * 2) / cell));
            string prefix = "NPC.NavMaps.Vanilla." + spec.Name + "-";
            var assembly = typeof(Services).Assembly;
            foreach (string resource in WildWasteland.Core.DiskResources.Names)
                if (resource.StartsWith(prefix, StringComparison.Ordinal) && resource.EndsWith(".wwnav", StringComparison.Ordinal))
                { using (var stream = WildWasteland.Core.DiskResources.Open(resource)) { var map = Read(stream, resource, spec, w, h, cell, margin, strict); if (map != null) return map; } }
            foreach (string directory in new[] { Path.Combine(Assets.ModuleDir("NPC"), "NavMaps", "Vanilla"),
                Path.Combine(Assets.ModuleConfigDir("NPC"), "NavMaps", "Custom"), Path.Combine(Assets.ModuleDir("NPC"), "NavMaps") })
            {
                if (!Directory.Exists(directory)) continue;
                var files = Directory.GetFiles(directory, spec.Name + "-*.wwnav"); Array.Sort(files, StringComparer.Ordinal);
                foreach (string file in files)
                    using (var stream = File.OpenRead(file)) { var map = Read(stream, file, spec, w, h, cell, margin, strict); if (map != null) return map; }
            }
            LastMiss = "wants " + w + "x" + h + " cells of " + cell.ToString("0.###") + " m, " + spec.ColliderCount + " colliders";
            return null;
        }
        private static TownBakeData Read(Stream stream, string source, BuildingSpec spec, int w, int h, float cell, float margin, bool strict)
        {
            using (var r = new BinaryReader(stream))
            {
                if (r.ReadInt32() != 0x574E4156 || r.ReadInt32() != 1) return null;
                r.ReadUInt64(); int mw = r.ReadInt32(), mh = r.ReadInt32(); float mc = r.ReadSingle(), reference = r.ReadSingle(); int colliders = r.ReadInt32();
                if (mw != w || mh != h || mc != cell || strict && colliders != spec.ColliderCount) return null;
                uint sum = Sum(2166136261, reference, 0, 0, 0);
                for (int i = 0; i < colliders; i++) sum = Sum(sum, 0, r.ReadByte(), 0, 0);
                int n = r.ReadInt32();
                if (n != w * h || r.BaseStream.Length - r.BaseStream.Position != n * 7L + 4) throw new InvalidDataException("Invalid bake length: " + source);
                var map = new TownBakeData { W = w, H = h, Cell = cell, Source = source,
                    Min = new Vector3(Mathf.Round((spec.Center.x - spec.HalfX) * 1000) / 1000 - margin, 0,
                        Mathf.Round((spec.Center.z - spec.HalfZ) * 1000) / 1000 - margin),
                    Floor = new float[n], Why = new byte[n], Sky = new byte[n], Edges = new byte[n] };
                for (int i = 0; i < n; i++)
                {
                    map.Floor[i] = r.ReadSingle(); map.Why[i] = r.ReadByte(); map.Sky[i] = r.ReadByte(); map.Edges[i] = r.ReadByte();
                    sum = Sum(sum, map.Floor[i], map.Why[i], map.Sky[i], map.Edges[i]);
                }
                if (r.ReadUInt32() != sum) throw new InvalidDataException("Bake checksum mismatch: " + source);
                return map;
            }
        }
        internal int Index(Vector3 local)
        {
            int x = Mathf.FloorToInt((local.x - Min.x) / Cell), z = Mathf.FloorToInt((local.z - Min.z) / Cell);
            return x < 0 || z < 0 || x >= W || z >= H ? -1 : z * W + x;
        }
        internal Vector3 Point(int i)
        { return new Vector3(Min.x + (i % W + .5f) * Cell, float.IsNaN(Floor[i]) ? 0 : Floor[i], Min.z + (i / W + .5f) * Cell); }
        internal bool Protected(int i)
        { return i >= 0 && (Why[i] == 2 || Why[i] == 3 || (!float.IsNaN(Floor[i]) && Sky[i] == 0)); }
        internal bool Walk(int a, int b)
        {
            if (a < 0 || b < 0 || a >= Floor.Length || b >= Floor.Length || float.IsNaN(Floor[a]) || float.IsNaN(Floor[b])) return false;
            if (a == b) return true;
            int dx = b % W - a % W, dz = b / W - a / W;
            if (Mathf.Abs(dx) > 1 || Mathf.Abs(dz) > 1) return false;
            if (dz < 0 || (dz == 0 && dx < 0)) { int swap = a; a = b; b = swap; dx = -dx; dz = -dz; }
            int k = dz == 0 ? 0 : dx == 0 ? 1 : dx > 0 ? 2 : 3;
            return (Edges[a] & (1 << k)) != 0 || (k < 2 && (Edges[a] & (1 << (k + 4))) != 0);
        }
        internal bool FindEntry(BuildingSpec spec)
        {
            // Exterior sky cells reachable from the map boundary, without crossing a wall or roof.
            var outside = new bool[Floor.Length]; var queue = new Queue<int>();
            for (int i = 0; i < Floor.Length; i++)
                if ((i % W == 0 || i % W == W - 1 || i / W == 0 || i / W == H - 1) && Sky[i] != 0 && !float.IsNaN(Floor[i]))
                { outside[i] = true; queue.Enqueue(i); }
            int[] dxs = { 1, -1, 0, 0 }, dzs = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                int a = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int x = a % W + dxs[d], z = a / W + dzs[d]; if (x < 0 || z < 0 || x >= W || z >= H) continue;
                    int b = z * W + x;
                    if (!outside[b] && Sky[b] != 0 && Walk(a, b)) { outside[b] = true; queue.Enqueue(b); }
                }
            }
            float best = float.NegativeInfinity; Vector3 entry = Vector3.zero, normal = Vector3.zero;
            for (int i = 0; i < Floor.Length; i++)
            {
                if (float.IsNaN(Floor[i]) || Sky[i] != 0) continue;
                for (int d = 0; d < 4; d++)
                {
                    int x = i % W + dxs[d], z = i / W + dzs[d]; if (x < 0 || z < 0 || x >= W || z >= H) continue;
                    int b = z * W + x; if (!outside[b] || !Walk(i, b)) continue;
                    Vector3 outward = new Vector3(dxs[d], 0, dzs[d]);
                    if (!RayOut(b, outward)) continue;
                    var p = (Point(i) + Point(b)) * .5f; p.y = Point(b).y;
                    float score = 1000 - new Vector2(p.x - spec.Center.x, p.z - spec.Center.z).sqrMagnitude;
                    if (score > best) { best = score; entry = p; normal = outward; }
                }
            }
            // Roofless ruins: use a measured clear opening along their actual footprint, preferring
            // an opening between blocked wall cells over an arbitrary corner of the bake envelope.
            if (float.IsNegativeInfinity(best))
                for (int d = 0; d < 4; d++) for (int k = 0; k < (d < 2 ? H : W); k++)
                {
                    Vector3 outward = new Vector3(dxs[d], 0, dzs[d]);
                    var p = spec.Center;
                    if (d < 2) { p.x += dxs[d] * (spec.HalfX - Cell); p.z = Min.z + (k + .5f) * Cell; }
                    else { p.z += dzs[d] * (spec.HalfZ - Cell); p.x = Min.x + (k + .5f) * Cell; }
                    int a = Index(p); if (a < 0 || float.IsNaN(Floor[a]) || !outside[a] || !RayOut(a, outward)) continue;
                    int inside = Index(p - outward * 2); if (inside < 0 || float.IsNaN(Floor[inside])) continue;
                    float score = -new Vector2(p.x - spec.Center.x, p.z - spec.Center.z).sqrMagnitude;
                    if (score > best) { best = score; entry = Point(a); normal = outward; }
                }
            if (float.IsNegativeInfinity(best)) return false;
            spec.Entry = entry; spec.Outward = normal; return true;
        }
        private bool RayOut(int start, Vector3 direction)
        {
            int previous = start; var p = Point(start);
            for (int k = 1; k <= Mathf.Max(W, H); k++)
            {
                int next = Index(p + direction * (k * Cell)); if (next < 0) return true;
                if (!Walk(previous, next)) return false; previous = next;
            }
            return false;
        }
    }
}
