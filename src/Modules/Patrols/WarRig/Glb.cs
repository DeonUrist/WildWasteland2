using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    // Static (unskinned) glTF 2.0 / GLB reader: every mesh node is baked into one Unity mesh in glTF scene space,
    // one submesh per primitive. glTF is right-handed: x is mirrored, winding flipped, v flipped.
    internal sealed class GlbModel
    {
        public Mesh Mesh;
        public readonly List<string> ImageNames = new List<string>();      // per submesh: baseColor image name (or null)
        public readonly List<string> MaterialNames = new List<string>();   // per submesh: glTF material name
        public readonly List<byte[]> ImageBytes = new List<byte[]>();      // per submesh: embedded PNG/JPG, null if none
        public Bounds Bounds;
        public readonly List<Mesh> Colliders = new List<Mesh>();          // nodes named COL*: convex collider hulls, not rendered
    }

    internal static class Glb
    {
        private static Dictionary<string, object> Obj(object o) { return o as Dictionary<string, object>; }
        private static List<object> Arr(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) ? v as List<object> : null; }
        private static bool Has(Dictionary<string, object> d, string k) { return d != null && d.ContainsKey(k) && d[k] != null; }
        private static int Int(Dictionary<string, object> d, string k, int def) { object v; return d != null && d.TryGetValue(k, out v) && v is double ? (int)(double)v : def; }
        private static string Str(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) ? v as string : null; }
        private static double D(object o) { return o is double ? (double)o : 0.0; }

        public static GlbModel Load(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            string json = null; byte[] bin = null;
            if (file.Length >= 12 && BitConverter.ToUInt32(file, 0) == 0x46546C67)
            {
                int off = 12;
                while (off + 8 <= file.Length)
                {
                    int len = BitConverter.ToInt32(file, off); uint type = BitConverter.ToUInt32(file, off + 4); off += 8;
                    if (type == 0x4E4F534A) json = Encoding.UTF8.GetString(file, off, len);
                    else if (type == 0x004E4942) { bin = new byte[len]; Buffer.BlockCopy(file, off, bin, 0, len); }
                    off += len;
                }
            }
            else json = Encoding.UTF8.GetString(file);
            if (json == null) throw new InvalidDataException("no JSON chunk");
            var root = Obj(Json.Parse(json));
            string dir = Path.GetDirectoryName(path);

            var buffers = new List<byte[]>();
            foreach (var b in Arr(root, "buffers") ?? new List<object>())
            {
                string uri = Str(Obj(b), "uri");
                if (uri == null) buffers.Add(bin);
                else if (uri.StartsWith("data:", StringComparison.Ordinal)) buffers.Add(Convert.FromBase64String(uri.Substring(uri.IndexOf(',') + 1)));
                else buffers.Add(File.ReadAllBytes(Path.Combine(dir, Uri.UnescapeDataString(uri))));
            }
            var views = Arr(root, "bufferViews") ?? new List<object>();
            var acc = Arr(root, "accessors") ?? new List<object>();
            var nodes = Arr(root, "nodes") ?? new List<object>();
            var meshes = Arr(root, "meshes") ?? new List<object>();
            var mats = Arr(root, "materials") ?? new List<object>();
            var texs = Arr(root, "textures") ?? new List<object>();
            var imgs = Arr(root, "images") ?? new List<object>();

            int n = nodes.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = -1;
            for (int i = 0; i < n; i++) { var ch = Arr(Obj(nodes[i]), "children"); if (ch != null) foreach (var c in ch) parent[(int)D(c)] = i; }
            var world = new Matrix4x4[n]; var done = new bool[n];
            for (int i = 0; i < n; i++) World(i, nodes, parent, world, done);

            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            var subs = new List<List<int>>();
            var model = new GlbModel();
            for (int ni = 0; ni < n; ni++)
            {
                var nd = Obj(nodes[ni]);
                if (!Has(nd, "mesh")) continue;
                var m = world[ni];
                string nodeName = Str(nd, "name") ?? "";
                if (nodeName.StartsWith("COL", StringComparison.OrdinalIgnoreCase))
                {
                    model.Colliders.Add(Hull(nodeName, Obj(meshes[Int(nd, "mesh", 0)]), m, acc, views, buffers));
                    continue;
                }
                var nm = m.inverse.transpose;
                foreach (var po in Arr(Obj(meshes[Int(nd, "mesh", 0)]), "primitives"))
                {
                    var p = Obj(po);
                    if (Int(p, "mode", 4) != 4) continue;
                    var at = Obj(p["attributes"]);
                    int c;
                    float[] pos = Read(acc, views, buffers, Int(at, "POSITION", -1), out c);
                    float[] nor = Has(at, "NORMAL") ? Read(acc, views, buffers, Int(at, "NORMAL", -1), out c) : null;
                    float[] uv = Has(at, "TEXCOORD_0") ? Read(acc, views, buffers, Int(at, "TEXCOORD_0", -1), out c) : null;
                    int count = pos.Length / 3, b0 = verts.Count;
                    for (int v = 0; v < count; v++)
                    {
                        var w = m.MultiplyPoint3x4(new Vector3(pos[v * 3], pos[v * 3 + 1], pos[v * 3 + 2]));
                        verts.Add(new Vector3(-w.x, w.y, w.z));
                        if (nor != null) { var q = nm.MultiplyVector(new Vector3(nor[v * 3], nor[v * 3 + 1], nor[v * 3 + 2])).normalized; norms.Add(new Vector3(-q.x, q.y, q.z)); }
                        uvs.Add(uv != null ? new Vector2(uv[v * 2], 1f - uv[v * 2 + 1]) : Vector2.zero);
                    }
                    var tri = new List<int>();
                    if (Has(p, "indices"))
                    {
                        float[] ix = Read(acc, views, buffers, Int(p, "indices", -1), out c);
                        for (int t = 0; t + 2 < ix.Length; t += 3) { tri.Add(b0 + (int)ix[t]); tri.Add(b0 + (int)ix[t + 2]); tri.Add(b0 + (int)ix[t + 1]); }
                    }
                    else for (int t = 0; t + 2 < count; t += 3) { tri.Add(b0 + t); tri.Add(b0 + t + 2); tri.Add(b0 + t + 1); }
                    // determinant < 0 (mirroring node scale) flips winding once more
                    if (m.determinant < 0) for (int t = 0; t + 2 < tri.Count; t += 3) { int s = tri[t + 1]; tri[t + 1] = tri[t + 2]; tri[t + 2] = s; }
                    subs.Add(tri);

                    string img = null, matName = null; byte[] bytes = null;
                    int mi = Int(p, "material", -1);
                    if (mi >= 0 && mi < mats.Count)
                    {
                        var md = Obj(mats[mi]); matName = Str(md, "name");
                        var pbr = Obj(md.ContainsKey("pbrMetallicRoughness") ? md["pbrMetallicRoughness"] : null);
                        var bct = Obj(pbr != null && pbr.ContainsKey("baseColorTexture") ? pbr["baseColorTexture"] : null);
                        int ti = Int(bct, "index", -1);
                        if (ti >= 0 && ti < texs.Count)
                        {
                            int ii = Int(Obj(texs[ti]), "source", -1);
                            if (ii >= 0 && ii < imgs.Count)
                            {
                                var im = Obj(imgs[ii]); img = Str(im, "name");
                                int bv = Int(im, "bufferView", -1);
                                if (bv >= 0) { var vd = Obj(views[bv]); var buf = buffers[Int(vd, "buffer", 0)]; bytes = new byte[Int(vd, "byteLength", 0)]; Buffer.BlockCopy(buf, Int(vd, "byteOffset", 0), bytes, 0, bytes.Length); }
                                if (img == null) { string u = Str(im, "uri"); if (u != null && !u.StartsWith("data:")) img = Path.GetFileNameWithoutExtension(u); }
                            }
                        }
                    }
                    model.ImageNames.Add(img); model.MaterialNames.Add(matName); model.ImageBytes.Add(bytes);
                }
            }
            if (verts.Count == 0)
            {
                if (model.Colliders.Count > 0) { model.Mesh = new Mesh(); return model; }
                throw new InvalidDataException("no triangle meshes in the file");
            }
            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
            if (norms.Count == verts.Count) mesh.SetNormals(norms); else mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            model.Mesh = mesh; model.Bounds = mesh.bounds;
            return model;
        }

        // Positions only (x mirrored); winding is irrelevant for a convex MeshCollider.
        private static Mesh Hull(string name, Dictionary<string, object> mesh, Matrix4x4 m, List<object> acc, List<object> views, List<byte[]> buffers)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            foreach (var po in Arr(mesh, "primitives"))
            {
                var p = Obj(po); var at = Obj(p["attributes"]); int c;
                float[] pos = Read(acc, views, buffers, Int(at, "POSITION", -1), out c);
                int b0 = verts.Count, count = pos.Length / 3;
                for (int v = 0; v < count; v++) { var w = m.MultiplyPoint3x4(new Vector3(pos[v * 3], pos[v * 3 + 1], pos[v * 3 + 2])); verts.Add(new Vector3(-w.x, w.y, w.z)); }
                if (Has(p, "indices")) { float[] ix = Read(acc, views, buffers, Int(p, "indices", -1), out c); for (int t = 0; t + 2 < ix.Length; t += 3) { tris.Add(b0 + (int)ix[t]); tris.Add(b0 + (int)ix[t + 2]); tris.Add(b0 + (int)ix[t + 1]); } }
                else for (int t = 0; t + 2 < count; t += 3) { tris.Add(b0 + t); tris.Add(b0 + t + 2); tris.Add(b0 + t + 1); }
            }
            var r = new Mesh { name = name };
            r.SetVertices(verts); r.SetTriangles(tris, 0); r.RecalculateBounds();
            return r;
        }

        private static void World(int i, List<object> nodes, int[] parent, Matrix4x4[] world, bool[] done)
        {
            if (done[i]) return;
            var nd = Obj(nodes[i]);
            Matrix4x4 l;
            var mat = Arr(nd, "matrix");
            if (mat != null && mat.Count == 16) { l = new Matrix4x4(); for (int k = 0; k < 16; k++) l[k % 4, k / 4] = (float)D(mat[k]); }
            else
            {
                var t = Arr(nd, "translation"); var r = Arr(nd, "rotation"); var s = Arr(nd, "scale");
                l = Matrix4x4.TRS(
                    t != null ? new Vector3((float)D(t[0]), (float)D(t[1]), (float)D(t[2])) : Vector3.zero,
                    r != null ? new Quaternion((float)D(r[0]), (float)D(r[1]), (float)D(r[2]), (float)D(r[3])) : Quaternion.identity,
                    s != null ? new Vector3((float)D(s[0]), (float)D(s[1]), (float)D(s[2])) : Vector3.one);
            }
            if (parent[i] >= 0) { World(parent[i], nodes, parent, world, done); l = world[parent[i]] * l; }
            world[i] = l; done[i] = true;
        }

        private static float[] Read(List<object> acc, List<object> views, List<byte[]> buffers, int index, out int comps)
        {
            var a = Obj(acc[index]);
            int count = Int(a, "count", 0), ct = Int(a, "componentType", 5126);
            string type = Str(a, "type");
            comps = type == "SCALAR" ? 1 : type == "VEC2" ? 2 : type == "VEC3" ? 3 : type == "VEC4" ? 4 : type == "MAT4" ? 16 : 1;
            var r = new float[count * comps];
            if (!Has(a, "bufferView")) return r;
            var v = Obj(views[Int(a, "bufferView", 0)]);
            var buf = buffers[Int(v, "buffer", 0)];
            int csize = ct == 5126 || ct == 5125 ? 4 : ct == 5123 || ct == 5122 ? 2 : 1;
            int stride = Int(v, "byteStride", 0); if (stride == 0) stride = csize * comps;
            int b = Int(v, "byteOffset", 0) + Int(a, "byteOffset", 0);
            bool norm = a.ContainsKey("normalized") && a["normalized"] is bool && (bool)a["normalized"];
            for (int i = 0; i < count; i++)
                for (int k = 0; k < comps; k++)
                {
                    int o = b + i * stride + k * csize; float f;
                    switch (ct)
                    {
                        case 5126: f = BitConverter.ToSingle(buf, o); break;
                        case 5125: f = BitConverter.ToUInt32(buf, o); break;
                        case 5123: f = BitConverter.ToUInt16(buf, o); if (norm) f /= 65535f; break;
                        case 5122: f = BitConverter.ToInt16(buf, o); if (norm) f = Mathf.Max(f / 32767f, -1f); break;
                        case 5121: f = buf[o]; if (norm) f /= 255f; break;
                        default: f = (sbyte)buf[o]; if (norm) f = Mathf.Max(f / 127f, -1f); break;
                    }
                    r[i * comps + k] = f;
                }
            return r;
        }
    }
}
