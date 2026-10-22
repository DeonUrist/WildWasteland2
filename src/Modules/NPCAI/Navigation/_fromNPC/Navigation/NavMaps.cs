using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    internal static partial class Nav
    {
        // These are prefab templates, not save-slot data. Bump MapVersion when sampling or edge rules change.
        private const int MapVersion = 1, MapMagic = 0x574E4156;
        internal sealed class MapIo
        {
            internal BinaryReader Reader;
            internal BinaryWriter Writer;
            internal string Temp;
            internal string Source;
            internal bool Vanilla;
            internal int Next;
            internal uint Checksum = 2166136261;
            internal float RefOffset;
        }

        // (WW perf) decoded templates kept in memory per map key: a second instance of the same prefab clones the arrays
        // (plus its areas) instead of re-reading the .wwnav and re-flooding. Instance arrays are copies; the template is never edited.
        private sealed class MapTemplate
        {
            internal float[] Y; internal byte[] Why; internal bool[] Open, Near; internal byte[] Edges; internal int[] Comp, CompSize; internal byte[] Floors;
            internal float RefOffset; internal bool Vanilla; internal string Source; internal int Walkable;
        }
        private static readonly Dictionary<ulong, MapTemplate> _templates = new Dictionary<ulong, MapTemplate>();
        private const int TemplateLimit = 96;
        internal static int TemplateCount { get { return _templates.Count; } }
        private static void RememberTemplate(Structure s)
        {
            if (!(s.LoadedMap || s.MapSaved) || s.MapKey == 0 || s.MapFile == null || s.Root == null || s.FloorY == null || s.Comp == null || s.CompSize == null || s.Near == null) return;
            if (_templates.ContainsKey(s.MapKey)) return;
            if (_templates.Count >= TemplateLimit) _templates.Clear();
            int n = s.W * s.H; float rootY = s.Root.position.y;
            var t = new MapTemplate { Y = new float[n], Why = (byte[])s.Why.Clone(), Open = (bool[])s.Open.Clone(), Near = (bool[])s.Near.Clone(), Edges = (byte[])s.Edges.Clone(),
                Comp = (int[])s.Comp.Clone(), CompSize = (int[])s.CompSize.Clone(), RefOffset = s.RefY - rootY, Vanilla = s.LoadedVanilla, Source = s.MapSource ?? s.MapFile, Walkable = s.Walkable };
            for (int i = 0; i < n; i++) t.Y[i] = s.FloorY[i] - rootY;
            var colliders = s.MapColliders; t.Floors = new byte[colliders == null ? 0 : colliders.Length];
            if (colliders != null && s.FloorHits != null)
                for (int i = 0; i < colliders.Length; i++)
                { int count; t.Floors[i] = (byte)(colliders[i] != null && s.FloorHits.TryGetValue(colliders[i].GetInstanceID(), out count) && count >= FloorColCells ? 1 : 0); }
            _templates[s.MapKey] = t;
        }
        private static bool CloneTemplate(Structure s)
        {
            MapTemplate t; int n = s.W * s.H;
            if (s.ReplaceMap || !_templates.TryGetValue(s.MapKey, out t) || t.Y.Length != n || (s.SkipVanilla && t.Vanilla)
                || s.MapColliders == null || t.Floors.Length != s.MapColliders.Length) return false;
            float rootY = s.Root.position.y;
            for (int i = 0; i < n; i++) s.FloorY[i] = t.Y[i] + rootY;
            Array.Copy(t.Why, s.Why, n); Array.Copy(t.Open, s.Open, n); Array.Copy(t.Edges, s.Edges, n);
            s.Near = (bool[])t.Near.Clone(); s.Comp = (int[])t.Comp.Clone(); s.CompSize = (int[])t.CompSize.Clone();
            for (int i = 0; i < t.Floors.Length; i++) if (t.Floors[i] != 0 && s.MapColliders[i] != null) s.FloorHits[s.MapColliders[i].GetInstanceID()] = FloorColCells;
            s.RefY = rootY + t.RefOffset; s.Walkable = t.Walkable; s.Next = n;
            s.LoadedMap = true; s.LoadedVanilla = t.Vanilla; s.MapSource = t.Source; s.LoadedTemplate = true; s.Phase = 2;
            RememberCapturedMap(s);
            if (NpcModule.NavLog.Value) NpcModule.Log.LogInfo("Nav: cloned map " + s.Name + " from memory (" + (t.Vanilla ? "vanilla" : "custom cache") + "); " + t.Source);
            return true;
        }
        internal static void ForgetTemplates(bool vanillaToo)
        {
            if (vanillaToo) { _templates.Clear(); return; }
            var keys = new List<ulong>(); foreach (var kv in _templates) if (!kv.Value.Vanilla) keys.Add(kv.Key);
            foreach (ulong key in keys) _templates.Remove(key);
        }

        private static string CustomMapDir { get { return System.IO.Path.Combine(NpcModule.DataDir, "NavMaps", "Custom"); } }
        private static string VanillaMapDir { get { return System.IO.Path.Combine(Core.Assets.ModuleDir("NPC"), "NavMaps", "Vanilla"); } }
        internal static string MapActionStatus;

        private struct MapHash
        {
            internal ulong Value;
            internal void Add(long v) { unchecked { Value = (Value ^ (ulong)v) * 1099511628211UL; } }
            internal void Add(float v) { Add((long)Math.Round(v * 10000d)); }
            internal void Add(Vector3 v) { Add(v.x); Add(v.y); Add(v.z); }
            internal void Add(Bounds b) { Add(b.center); Add(b.size); }
            internal void Add(string s) { if (s != null) foreach (char c in s) Add((long)c); Add(-1L); }
        }

        private static bool MapCollider(Collider c)
        {
            return c != null && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger
                && ((1 << c.gameObject.layer) & BakeMask) != 0
                && (c.attachedRigidbody == null || c.attachedRigidbody.isKinematic);
        }

        private static void ConfigureMap(Structure s, Collider[] colliders, float margin)
        {
            // Yaw and positive scale preserve the horizontal grid and world-sized NPC clearance. Other placements retain the old bake.
            var root = s.Root; var scale = root.lossyScale;
            if (Vector3.Dot(root.up, Vector3.up) < 0.999999f || scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
            { NpcModule.Log.LogInfo("Nav: per-instance bake for " + s.Name + " (tilted or mirrored root)"); return; }
            for (int i = 0; i < 3; i++) scale[i] = Mathf.Round(scale[i] * 10000f) / 10000f;
            var hash = new MapHash { Value = 14695981039346656037UL };
            string name = root.name; int end = name.IndexOfAny(new[] { '(', ' ' });
            if (end >= 0) name = name.Substring(0, end);
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            if (name.Length > 80) name = name.Substring(0, 80);
            hash.Add((long)MapVersion); hash.Add(name); hash.Add(Application.version); hash.Add(Application.buildGUID);
            hash.Add(typeof(HutongGames.PlayMaker.Actions.CreateObject).Module.ModuleVersionId.ToString());
            hash.Add(scale); hash.Add(margin); hash.Add(Mathf.Max(0.25f, NpcModule.NavCellSize.Value));
            s.MapStep = Mathf.Max(0.1f, NpcModule.NavMaxStep.Value); hash.Add(s.MapStep);
            var kept = new List<Collider>(); Bounds bounds = new Bounds(); bool any = false;
            foreach (var c in colliders)
            {
                if (!MapCollider(c)) continue;
                Bounds shape;
                var mesh = c as MeshCollider; var box = c as BoxCollider;
                var sphere = c as SphereCollider; var capsule = c as CapsuleCollider;
                hash.Add(c.GetType().FullName); hash.Add((long)c.gameObject.layer);
                if (mesh != null && mesh.sharedMesh != null)
                {
                    shape = mesh.sharedMesh.bounds; hash.Add(mesh.sharedMesh.name);
                    hash.Add((long)mesh.sharedMesh.vertexCount); hash.Add(mesh.convex ? 1L : 0L);
                }
                else if (box != null) shape = new Bounds(box.center, box.size);
                else if (sphere != null) shape = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
                else if (capsule != null)
                {
                    var size = Vector3.one * capsule.radius * 2f;
                    size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
                    shape = new Bounds(capsule.center, size); hash.Add((long)capsule.direction);
                }
                else { NpcModule.Log.LogInfo("Nav: per-instance bake for " + s.Name + " (unsupported " + c.GetType().Name + ")"); return; }
                hash.Add(shape);
                // Compose local transforms: worldToLocal * localToWorld loses precision far from the floating origin.
                var matrix = Matrix4x4.identity;
                for (var t = c.transform; t != root; t = t.parent)
                {
                    hash.Add(t.name); hash.Add((long)t.GetSiblingIndex());
                    matrix = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * matrix;
                }
                matrix = Matrix4x4.Scale(scale) * matrix;
                for (int i = 0; i < 16; i++) hash.Add(matrix[i]);
                for (int i = 0; i < 8; i++)
                {
                    var p = matrix.MultiplyPoint3x4(shape.center + Vector3.Scale(shape.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
                }
                kept.Add(c);
            }
            if (!any) return;
            // Stable millimetre bounds keep equivalent rotations/scales on identical cell counts.
            var min = bounds.min; var max = bounds.max;
            for (int i = 0; i < 3; i++) { min[i] = Mathf.Round(min[i] * 1000f) / 1000f; max[i] = Mathf.Round(max[i] * 1000f) / 1000f; }
            bounds.SetMinMax(min, max); bounds.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
            float cell = Mathf.Max(0.25f, NpcModule.NavCellSize.Value);
            cell = Mathf.Max(cell, Mathf.Max(bounds.size.x, bounds.size.z) / 600f);
            s.Cell = cell; s.W = Mathf.Max(2, Mathf.CeilToInt(bounds.size.x / cell)); s.H = Mathf.Max(2, Mathf.CeilToInt(bounds.size.z / cell));
            s.GridRight = root.rotation * Vector3.right; s.GridRight.y = 0f; s.GridRight.Normalize();
            s.GridForward = Vector3.Cross(s.GridRight, Vector3.up);
            var localMin = bounds.min;
            s.GridOrigin = root.position + s.GridRight * localMin.x + s.GridForward * localMin.z;
            s.Box = new Bounds(s.GridOrigin + Vector3.up * bounds.center.y, Vector3.zero);
            var world = s.Box;
            world.Encapsulate(s.GridOrigin + s.GridRight * bounds.size.x + Vector3.up * bounds.min.y);
            world.Encapsulate(s.GridOrigin + s.GridForward * bounds.size.z + Vector3.up * bounds.max.y);
            world.Encapsulate(s.GridOrigin + s.GridRight * bounds.size.x + s.GridForward * bounds.size.z + Vector3.up * bounds.center.y);
            s.Box = world; hash.Add(bounds); hash.Add(cell); hash.Add((long)kept.Count);
            s.MapKey = hash.Value; s.MapColliders = kept.ToArray();
            string file = name + "-" + hash.Value.ToString("X16") + ".wwnav";
            s.MapFile = System.IO.Path.Combine(CustomMapDir, file);
            s.VanillaMapFile = System.IO.Path.Combine(VanillaMapDir, file);
            s.LegacyMapFile = System.IO.Path.Combine(Core.Assets.ModuleDir("NPC"), "NavMaps", file);
        }

        private static bool OpenMap(Structure s)
        {
            if (s.MapFile == null) return false;
            if (s.MapStep != Mathf.Max(0.1f, NpcModule.NavMaxStep.Value)) { s.MapFile = null; return false; }
            s.LoadedTemplate = false;
            if (CloneTemplate(s)) return true;
            try
            {
                s.MapJob = new MapIo(); var job = s.MapJob; Stream stream = null;
                if (!s.SkipVanilla)
                {
                    job.Vanilla = true;
                    string resource = "NPC.NavMaps.Vanilla." + System.IO.Path.GetFileName(s.MapFile);
                    job.Source = "embedded:" + resource;
                    stream = WildWasteland.Core.DiskResources.Open(resource);
                    if (stream == null)
                    {
                        job.Source = s.VanillaMapFile;
                        if (File.Exists(job.Source)) stream = new FileStream(job.Source, FileMode.Open, FileAccess.Read, FileShare.Read, 16384);
                    }
                }
                if (stream == null)
                {
                    job.Vanilla = false; job.Source = s.MapFile;
                    if (!File.Exists(job.Source)) job.Source = s.LegacyMapFile;
                    if (!File.Exists(job.Source))
                    {
                        CloseMap(s); NpcModule.Log.LogInfo("Nav: map missing " + s.Name + "; baking " + s.MapFile);
                        return false;
                    }
                    stream = new FileStream(job.Source, FileMode.Open, FileAccess.Read, FileShare.Read, 16384);
                }
                job.Reader = new BinaryReader(stream);
                var r = job.Reader;
                if (r.ReadInt32() != MapMagic || r.ReadInt32() != MapVersion || r.ReadUInt64() != s.MapKey
                    || r.ReadInt32() != s.W || r.ReadInt32() != s.H || r.ReadSingle() != s.Cell) throw new InvalidDataException("incompatible header");
                job.RefOffset = r.ReadSingle();
                if (float.IsNaN(job.RefOffset) || float.IsInfinity(job.RefOffset) || Mathf.Abs(job.RefOffset) > 100f
                    || r.ReadInt32() != s.MapColliders.Length) throw new InvalidDataException("invalid floor metadata");
                job.Checksum = MapChecksum(job.Checksum, job.RefOffset, 0, 0, 0);
                for (int i = 0; i < s.MapColliders.Length; i++)
                {
                    byte floor = r.ReadByte(); if (floor > 1) throw new InvalidDataException("invalid floor collider");
                    job.Checksum = MapChecksum(job.Checksum, 0f, floor, 0, 0);
                    if (floor != 0) s.FloorHits[s.MapColliders[i].GetInstanceID()] = FloorColCells;
                }
                int n = s.W * s.H;
                if (r.ReadInt32() != n || r.BaseStream.Length - r.BaseStream.Position != n * 7L + 4L) throw new InvalidDataException("invalid map length");
                s.RefY = s.Root.position.y + job.RefOffset; s.Phase = -1;
                NpcModule.Log.LogInfo("Nav: loading map " + s.Name + " (" + (job.Vanilla ? "vanilla" : "custom cache") + "); " + job.Source);
                return true;
            }
            catch (Exception e) { bool vanilla = RejectMap(s, e); s.FloorHits.Clear(); return vanilla && OpenMap(s); }
        }

        private static void ReadMap(Structure s, float budget)
        {
            try
            {
                var job = s.MapJob; var r = job.Reader; int n = s.W * s.H;
                while (job.Next < n && _sw.Elapsed.TotalMilliseconds < budget)
                {
                    int i = job.Next++; float y = r.ReadSingle(); byte why = r.ReadByte(), sky = r.ReadByte(), edges = r.ReadByte();
                    if (float.IsInfinity(y) || (!float.IsNaN(y) && Mathf.Abs(y - job.RefOffset) > 6.01f)
                        || why > 4 || sky > 1 || (edges & 192) != 0) throw new InvalidDataException("invalid map cell");
                    job.Checksum = MapChecksum(job.Checksum, y, why, sky, edges);
                    s.FloorY[i] = y + s.Root.position.y; s.Why[i] = why; s.Open[i] = sky != 0; s.Edges[i] = edges;
                    if (!float.IsNaN(y)) s.Walkable++;
                }
                s.Next = job.Next;
                if (job.Next < n) return;
                if (r.ReadUInt32() != job.Checksum) throw new InvalidDataException("map checksum mismatch");
                s.LoadedVanilla = job.Vanilla; s.MapSource = job.Source;
                RememberCapturedMap(s);
                CloseMap(s); s.LoadedMap = true; s.Phase = 2; s.Next = 0;
            }
            catch (Exception e) { bool vanilla = RejectMap(s, e); BeginBake(s, vanilla); }
        }

        private static bool CreateMap(Structure s)
        {
            if (s.MapFile == null || s.MapStep != Mathf.Max(0.1f, NpcModule.NavMaxStep.Value)) return false;
            try
            {
                if (!s.ReplaceMap && File.Exists(s.MapFile)) return false;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(s.MapFile));
                s.MapJob = new MapIo { Temp = s.MapFile + "." + s.Root.GetInstanceID() + ".tmp" };
                var w = new BinaryWriter(new FileStream(s.MapJob.Temp, FileMode.Create, FileAccess.Write, FileShare.None, 16384));
                s.MapJob.Writer = w;
                w.Write(MapMagic); w.Write(MapVersion); w.Write(s.MapKey); w.Write(s.W); w.Write(s.H); w.Write(s.Cell);
                w.Write(s.RefY - s.Root.position.y); w.Write(s.MapColliders.Length);
                s.MapJob.Checksum = MapChecksum(s.MapJob.Checksum, s.RefY - s.Root.position.y, 0, 0, 0);
                foreach (var c in s.MapColliders)
                {
                    int count; byte floor = (byte)(c != null && s.FloorHits.TryGetValue(c.GetInstanceID(), out count) && count >= FloorColCells ? 1 : 0);
                    w.Write(floor); s.MapJob.Checksum = MapChecksum(s.MapJob.Checksum, 0f, floor, 0, 0);
                }
                w.Write(s.W * s.H); s.Phase = 3; s.Next = 0;
                return true;
            }
            catch (Exception e) { MapWriteFailed(s, e); return false; }
        }

        private static bool WriteMap(Structure s, float budget)
        {
            try
            {
                var job = s.MapJob; var w = job.Writer; int n = s.W * s.H;
                while (job.Next < n && _sw.Elapsed.TotalMilliseconds < budget)
                {
                    int i = job.Next++; float y = s.FloorY[i] - s.Root.position.y; byte sky = (byte)(s.Open[i] ? 1 : 0);
                    w.Write(y); w.Write(s.Why[i]); w.Write(sky); w.Write(s.Edges[i]);
                    job.Checksum = MapChecksum(job.Checksum, y, s.Why[i], sky, s.Edges[i]);
                }
                s.Next = job.Next;
                if (job.Next < n) return false;
                w.Write(job.Checksum); w.Dispose(); job.Writer = null;
                if (File.Exists(s.MapFile)) File.Replace(job.Temp, s.MapFile, null); else File.Move(job.Temp, s.MapFile);
                job.Temp = null; CloseMap(s); s.Phase = 2; s.MapSaved = true;
                RememberCapturedMap(s);
                NpcModule.Log.LogInfo("Nav: saved map " + s.Name + ": " + s.W + " x " + s.H + ", " + s.Walkable + " walkable; " + s.MapFile);
            }
            catch (Exception e) { MapWriteFailed(s, e); s.Phase = 2; }
            return true;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct MapFloat
        {
            [FieldOffset(0)] internal float Float;
            [FieldOffset(0)] internal uint Bits;
        }
        private static uint MapChecksum(uint hash, float y, byte why, byte sky, byte edges)
        {
            unchecked
            {
                hash = (hash ^ new MapFloat { Bits = 0, Float = y }.Bits) * 16777619;
                hash = (hash ^ why) * 16777619; hash = (hash ^ sky) * 16777619;
                return (hash ^ edges) * 16777619;
            }
        }

        private static bool RejectMap(Structure s, Exception e)
        {
            bool vanilla = s.MapJob != null && s.MapJob.Vanilla;
            string source = s.MapJob != null ? s.MapJob.Source : s.MapFile;
            CloseMap(s);
            if (vanilla) s.SkipVanilla = true; else s.ReplaceMap = true;
            _capturedMapTypes.Remove(HighlightMapName(s.Name));
            NpcModule.Log.LogWarning("Nav: rejected map " + s.Name + " (" + e.Message + "); " + (vanilla ? "trying custom cache; " : "rebaking; ") + source);
            return vanilla;
        }
        private static void MapWriteFailed(Structure s, Exception e)
        {
            CloseMap(s); NpcModule.Log.LogWarning("Nav: could not save map " + s.Name + " (" + e.Message + "); using the live bake");
        }
        private static void CloseMap(Structure s)
        {
            var job = s.MapJob; if (job == null) return; s.MapJob = null;
            try { if (job.Reader != null) job.Reader.Dispose(); } catch { }
            try { if (job.Writer != null) job.Writer.Dispose(); } catch { }
            try { if (job.Temp != null && File.Exists(job.Temp)) File.Delete(job.Temp); } catch { }
        }
    }
}
