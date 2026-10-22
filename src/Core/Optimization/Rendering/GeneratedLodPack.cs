using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace WildWasteland.Modules.Performance
{
    // Generated packs are optional user data, separate from the shipped runtime asset index.
    internal sealed class GeneratedLodPack
    {
        internal readonly Dictionary<string, string> Entries = new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly string Root;
        internal GeneratedLodPack(string root)
        {
            Root = root;
            string[] lines = File.ReadAllLines(Path.Combine(root, "catalog.tsv"));
            if (lines.Length == 0 || lines[0] != "WWLOD\t1") throw new InvalidDataException("Unsupported LOD catalog version");
            foreach (string line in lines)
            {
                if (line.Length == 0) continue;
                string[] row = line.Split('\t');
                if (row[0] == "source")
                {
                    if (row.Length != 4) throw new InvalidDataException("Invalid LOD source row");
                    string path = Encoding.UTF8.GetString(Convert.FromBase64String(row[1]));
                    var file = new FileInfo(path);
                    long seconds = file.Exists ? (file.LastWriteTimeUtc.Ticks - 621355968000000000L) / 10000000L : -1;
                    if (!file.Exists || file.Length.ToString(CultureInfo.InvariantCulture) != row[2] || seconds.ToString(CultureInfo.InvariantCulture) != row[3])
                        throw new InvalidDataException("LOD source changed; rerun Generate.cmd: " + path);
                }
                else if (row[0] == "mesh")
                {
                    if (row.Length != 3 || !IsHash(row[1]) || !IsHash(row[2]) || Entries.ContainsKey(row[1])) throw new InvalidDataException("Invalid LOD mesh row");
                    Entries.Add(row[1], row[2]);
                }
                else if (row[0] != "WWLOD") throw new InvalidDataException("Unknown LOD catalog row");
            }
        }

        static bool IsHash(string value)
        {
            if (value.Length != 64) return false;
            foreach (char c in value) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }

        internal static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        internal static string Key(Mesh mesh)
        {
            var b = mesh.bounds;
            var text = new StringBuilder(mesh.name).Append('\0').Append(mesh.vertexCount.ToString(CultureInfo.InvariantCulture));
            foreach (float value in new[] { b.center.x, b.center.y, b.center.z, b.extents.x, b.extents.y, b.extents.z })
                text.Append('|').Append(Math.Floor((double)value * 10000 + 0.5).ToString("0", CultureInfo.InvariantCulture));
            text.Append('|').Append(mesh.subMeshCount.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < mesh.subMeshCount; i++) text.Append('|').Append(mesh.GetIndexCount(i).ToString(CultureInfo.InvariantCulture));
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }

        internal sealed class Channel { internal int Kind, Dimension; internal float[] Values; }
        internal sealed class Level
        {
            internal int Count;
            internal Channel[] Channels;
            internal Matrix4x4[] Bindposes;
            internal int[][] Indices;

            internal Mesh Create(Bounds bounds)
            {
                var mesh = new Mesh { name = "WWLOD_generated", indexFormat = Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                try
                {
                    Channel weights = null, bones = null;
                    foreach (var c in Channels)
                    {
                        float[] a = c.Values;
                        if (c.Kind == 0 || c.Kind == 1)
                        {
                            var values = new Vector3[Count];
                            for (int i = 0; i < Count; i++) values[i] = new Vector3(a[i * 3], a[i * 3 + 1], a[i * 3 + 2]);
                            if (c.Kind == 0) mesh.vertices = values; else mesh.normals = values;
                        }
                        else if (c.Kind == 2)
                        {
                            var values = new Vector4[Count];
                            for (int i = 0; i < Count; i++) values[i] = new Vector4(a[i * 4], a[i * 4 + 1], a[i * 4 + 2], a[i * 4 + 3]);
                            mesh.tangents = values;
                        }
                        else if (c.Kind == 3)
                        {
                            var values = new Color[Count];
                            for (int i = 0; i < Count; i++) values[i] = new Color(a[i * 4], a[i * 4 + 1], a[i * 4 + 2], a[i * 4 + 3]);
                            mesh.colors = values;
                        }
                        else if (c.Kind >= 4 && c.Kind <= 11)
                        {
                            // Keep 2D/3D/4D UV channels, including lightmap UVs.
                            if (c.Dimension == 2)
                            {
                                var values = new List<Vector2>(Count);
                                for (int i = 0; i < Count; i++) values.Add(new Vector2(a[i * 2], a[i * 2 + 1]));
                                mesh.SetUVs(c.Kind - 4, values);
                            }
                            else if (c.Dimension == 3)
                            {
                                var values = new List<Vector3>(Count);
                                for (int i = 0; i < Count; i++) values.Add(new Vector3(a[i * 3], a[i * 3 + 1], a[i * 3 + 2]));
                                mesh.SetUVs(c.Kind - 4, values);
                            }
                            else
                            {
                                var values = new List<Vector4>(Count);
                                for (int i = 0; i < Count; i++) values.Add(new Vector4(a[i * 4], a[i * 4 + 1], a[i * 4 + 2], a[i * 4 + 3]));
                                mesh.SetUVs(c.Kind - 4, values);
                            }
                        }
                        else if (c.Kind == 12) weights = c;
                        else if (c.Kind == 13) bones = c;
                    }
                    if (weights != null)
                    {
                        var skin = new BoneWeight[Count];
                        for (int i = 0; i < Count; i++)
                        {
                            int k = i * 4;
                            skin[i] = new BoneWeight { weight0 = weights.Values[k], weight1 = weights.Values[k + 1], weight2 = weights.Values[k + 2], weight3 = weights.Values[k + 3],
                                boneIndex0 = (int)bones.Values[k], boneIndex1 = (int)bones.Values[k + 1], boneIndex2 = (int)bones.Values[k + 2], boneIndex3 = (int)bones.Values[k + 3] };
                        }
                        mesh.boneWeights = skin;
                        mesh.bindposes = Bindposes;
                    }
                    mesh.subMeshCount = Indices.Length;
                    for (int i = 0; i < Indices.Length; i++) mesh.SetTriangles(Indices[i], i, false);
                    mesh.bounds = bounds;
                    mesh.UploadMeshData(true);
                    return mesh;
                }
                catch { UnityEngine.Object.Destroy(mesh); throw; }
            }
        }

        // Runs on a worker: file IO, hash verification and decoding only, no Unity object calls.
        internal Level[] Read(string payload, int originalVertices, int submeshes)
        {
            string path = Path.Combine(Root, payload + ".wwlod");
            long length = new FileInfo(path).Length;
            if (length < 16 || length > 128 * 1024 * 1024) throw new InvalidDataException("LOD payload size limit");
            byte[] bytes = File.ReadAllBytes(path);
            if (Hash(bytes) != payload) throw new InvalidDataException("LOD payload checksum mismatch");
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != 0x314c5757 || reader.ReadInt32() != 2) throw new InvalidDataException("Invalid LOD payload header");
                var levels = new Level[2];
                for (int l = 0; l < levels.Length; l++)
                {
                    int vertices = reader.ReadInt32(), channels = reader.ReadInt32();
                    if (vertices < 1 || vertices > originalVertices || vertices > 2000000 || channels < 1 || channels > 14) throw new InvalidDataException("Invalid LOD vertex count");
                    var level = new Level { Count = vertices, Channels = new Channel[channels] };
                    int kinds = 0;
                    for (int c = 0; c < channels; c++)
                    {
                        int kind = reader.ReadInt32(), dim = reader.ReadInt32();
                        if (kind < 0 || kind > 13 || dim < 2 || dim > 4 || (kinds & (1 << kind)) != 0
                            || ((kind == 0 || kind == 1) && dim != 3) || ((kind == 2 || kind == 3 || kind >= 12) && dim != 4))
                            throw new InvalidDataException("Invalid LOD channel");
                        kinds |= 1 << kind;
                        if ((long)vertices * dim * 4 > stream.Length - stream.Position) throw new InvalidDataException("Truncated LOD channel");
                        var values = new float[vertices * dim];
                        for (int i = 0; i < values.Length; i++)
                        {
                            float value = reader.ReadSingle();
                            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite LOD value");
                            values[i] = value;
                        }
                        level.Channels[c] = new Channel { Kind = kind, Dimension = dim, Values = values };
                    }
                    if ((kinds & 1) == 0 || ((kinds >> 12) & 3) == 1 || ((kinds >> 12) & 3) == 2) throw new InvalidDataException("Missing LOD positions/skin channel");
                    int poses = reader.ReadInt32();
                    if (poses < 0 || poses > 4096) throw new InvalidDataException("LOD bindpose limit");
                    level.Bindposes = new Matrix4x4[poses];
                    for (int p = 0; p < poses; p++)
                    {
                        var matrix = new Matrix4x4();
                        for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++) matrix[row, col] = reader.ReadSingle();
                        level.Bindposes[p] = matrix;
                    }
                    foreach (var c in level.Channels)
                        if (c.Kind == 13)
                            foreach (float bone in c.Values)
                                if (bone < 0 || bone >= poses || bone != (int)bone) throw new InvalidDataException("Invalid LOD bone index");
                    int subs = reader.ReadInt32();
                    if (subs != submeshes || subs < 1 || subs > 1024) throw new InvalidDataException("LOD submesh mismatch");
                    level.Indices = new int[subs][];
                    for (int s = 0; s < subs; s++)
                    {
                        int count = reader.ReadInt32();
                        if (count < 0 || count % 3 != 0 || (long)count * 4 > stream.Length - stream.Position) throw new InvalidDataException("Invalid LOD index count");
                        int[] indices = new int[count];
                        for (int i = 0; i < count; i++) { indices[i] = reader.ReadInt32(); if (indices[i] < 0 || indices[i] >= vertices) throw new InvalidDataException("Invalid LOD index"); }
                        level.Indices[s] = indices;
                    }
                    levels[l] = level;
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Trailing LOD data");
                return levels;
            }
        }
    }
}
