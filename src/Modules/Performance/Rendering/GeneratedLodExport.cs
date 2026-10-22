using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using WildWasteland.Core;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    // Optional authoring capture for procedural/GLB meshes that do not exist in Unity asset bundles.
    // Explicit user action only. Captures originals; never edits a mesh or launches the game.
    internal static class GeneratedLodExport
    {
        [Serializable] sealed class Channel { public int kind, dimension; public float[] data; }
        [Serializable] sealed class Submesh { public int[] indices; }
        [Serializable] sealed class Model
        {
            public int version = 1;
            public string key, name;
            public int count;
            public float[] bounds, bindposes;
            public Channel[] channels;
            public Submesh[] submeshes;
        }
        static Mesh[] _meshes;
        static int _index, _written, _skipped;
        static Job _job;
        internal static string Status = "For GLB/procedural models: export loaded meshes, then rerun Generate.cmd.";

        internal static void Start(PerformanceModule module)
        {
            if (_job != null) return;
            _meshes = Resources.FindObjectsOfTypeAll<Mesh>();
            _index = _written = _skipped = 0;
            Directory.CreateDirectory(Path.Combine(Assets.Root, "Performance", "LODInput"));
            _job = Scheduler.Sliced(module, "Performance.LodExport", 500, Step);
        }
        internal static void Stop()
        {
            if (_job != null) { _job.Remove(); _job = null; }
            _meshes = null;
        }

        static bool Step()
        {
            if (_index >= _meshes.Length)
            {
                Status = "Exported " + _written + " meshes; " + _skipped + " skipped (unreadable, small, blend shapes or unsupported). Rerun Generate.cmd.";
                Log.Always("[Performance] RC9 " + Status);
                Stop(); return false;
            }
            Mesh mesh = _meshes[_index++];
            Status = "Exporting loaded meshes " + _index + "/" + _meshes.Length;
            try
            {
                if (mesh == null || !mesh.isReadable || mesh.blendShapeCount != 0 || mesh.name.StartsWith("WWLOD_", StringComparison.Ordinal)) { _skipped++; return true; }
                int total = 0;
                var subs = new Submesh[mesh.subMeshCount];
                for (int i = 0; i < subs.Length; i++)
                {
                    if (mesh.GetTopology(i) != MeshTopology.Triangles) { _skipped++; return true; }
                    subs[i] = new Submesh { indices = mesh.GetTriangles(i) }; total += subs[i].indices.Length;
                }
                if (total < 600) { _skipped++; return true; }
                var channels = new List<Channel>();
                Add3(channels, 0, mesh.vertices); Add3(channels, 1, mesh.normals);
                Vector4[] tangents = mesh.tangents;
                if (tangents.Length > 0)
                {
                    var values = new float[tangents.Length * 4];
                    for (int i = 0; i < tangents.Length; i++) for (int k = 0; k < 4; k++) values[i * 4 + k] = tangents[i][k];
                    channels.Add(new Channel { kind = 2, dimension = 4, data = values });
                }
                Color[] colors = mesh.colors;
                if (colors.Length > 0)
                {
                    var values = new float[colors.Length * 4];
                    for (int i = 0; i < colors.Length; i++) for (int k = 0; k < 4; k++) values[i * 4 + k] = colors[i][k];
                    channels.Add(new Channel { kind = 3, dimension = 4, data = values });
                }
                var uv = new List<Vector4>();
                for (int channel = 0; channel < 8; channel++)
                {
                    uv.Clear(); mesh.GetUVs(channel, uv);
                    if (uv.Count == 0) continue;
                    var values = new float[uv.Count * 4];
                    for (int i = 0; i < uv.Count; i++) for (int k = 0; k < 4; k++) values[i * 4 + k] = uv[i][k];
                    channels.Add(new Channel { kind = 4 + channel, dimension = 4, data = values });
                }
                var skin = mesh.boneWeights;
                if (skin.Length > 0)
                {
                    var weights = new float[skin.Length * 4]; var bones = new float[skin.Length * 4];
                    for (int i = 0; i < skin.Length; i++)
                    {
                        int k = i * 4;
                        weights[k] = skin[i].weight0; weights[k + 1] = skin[i].weight1; weights[k + 2] = skin[i].weight2; weights[k + 3] = skin[i].weight3;
                        bones[k] = skin[i].boneIndex0; bones[k + 1] = skin[i].boneIndex1; bones[k + 2] = skin[i].boneIndex2; bones[k + 3] = skin[i].boneIndex3;
                    }
                    channels.Add(new Channel { kind = 12, dimension = 4, data = weights }); channels.Add(new Channel { kind = 13, dimension = 4, data = bones });
                }
                var matrices = mesh.bindposes; var bindposes = new float[matrices.Length * 16];
                for (int i = 0; i < matrices.Length; i++) for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++) bindposes[i * 16 + row * 4 + col] = matrices[i][row, col];
                Bounds bounds = mesh.bounds;
                var model = new Model { key = GeneratedLodPack.Key(mesh), name = mesh.name, count = mesh.vertexCount, channels = channels.ToArray(), submeshes = subs, bindposes = bindposes,
                    bounds = new[] { bounds.center.x, bounds.center.y, bounds.center.z, bounds.extents.x, bounds.extents.y, bounds.extents.z } };
                string json = JsonUtility.ToJson(model);
                string path = Path.Combine(Assets.Root, "Performance", "LODInput", model.key + "." + GeneratedLodPack.Hash(Encoding.UTF8.GetBytes(json)) + ".json");
                if (!File.Exists(path)) File.WriteAllText(path, json, new UTF8Encoding(false));
                _written++;
            }
            catch (Exception e) { _skipped++; Log.Warn("[Performance] RC9 mesh export skipped: " + e.Message); }
            return true;
        }
        static void Add3(List<Channel> channels, int kind, Vector3[] vectors)
        {
            if (vectors.Length == 0) return;
            var values = new float[vectors.Length * 3];
            for (int i = 0; i < vectors.Length; i++) { values[i * 3] = vectors[i].x; values[i * 3 + 1] = vectors[i].y; values[i * 3 + 2] = vectors[i].z; }
            channels.Add(new Channel { kind = kind, dimension = 3, data = values });
        }
    }
}
