#if false // Dismemberment disabled at Denis's request; retained for future investigation.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    // Cosmetic meshes only; the native corpse, skeleton, loot and save identity stay intact.
    internal static class ChainsawLimbs
    {
        private sealed class Part
        {
            internal SkinnedMeshRenderer Skin; internal Mesh Original, Cut, LooseMesh;
            internal GameObject Loose; internal float Until;
            internal Renderer[] Hidden; internal bool[] Enabled;
        }
        private static readonly List<Part> parts = new List<Part>();

        internal static void Cut(GameObject corpse, Vector3 point)
        {
            Tick();
            Transform limb = null; float best = float.MaxValue;
            var skins = corpse.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var skin in skins)
            {
                if (!skin.enabled || skin.sharedMesh == null) continue;
                foreach (var bone in skin.bones)
                {
                    if (bone == null) continue;
                    string name = bone.name.ToLowerInvariant();
                    if (!(name.Contains("head") || name.Contains("forearm") || name.Contains("upperarm") || name.Contains("leftarm")
                        || name.Contains("rightarm") || name.Contains("upleg") || name.Contains("leftleg") || name.Contains("rightleg"))) continue;
                    float distance = (bone.position - point).sqrMagnitude;
                    if (distance < best) { best = distance; limb = bone; }
                }
            }
            if (limb == null) { Core.Log.Warn("[NPC] Chainsaw corpse has no supported limb bones: " + corpse.name); return; }
            // Bound physics debris even during a large fight; retained corpse meshes have no update behaviour.
            foreach (var old in parts) DropLoose(old);
            bool removed = false;
            foreach (var skin in skins)
            {
                if (!skin.enabled || skin.sharedMesh == null || !skin.sharedMesh.isReadable) continue;
                Part part = null;
                try
                {
                    Mesh source = skin.sharedMesh; var weights = source.boneWeights; var bones = skin.bones;
                    if (weights.Length != source.vertexCount) continue;
                    var selected = new bool[bones.Length];
                    for (int i = 0; i < bones.Length; i++) selected[i] = bones[i] != null && (bones[i] == limb || bones[i].IsChildOf(limb));
                    var influence = new float[weights.Length];
                    for (int i = 0; i < weights.Length; i++)
                    {
                        var w = weights[i];
                        influence[i] = Weight(selected, w.boneIndex0, w.weight0) + Weight(selected, w.boneIndex1, w.weight1)
                            + Weight(selected, w.boneIndex2, w.weight2) + Weight(selected, w.boneIndex3, w.weight3);
                    }
                    var retained = new List<int>[source.subMeshCount]; var detached = new List<int>[source.subMeshCount]; int count = 0;
                    for (int s = 0; s < retained.Length; s++)
                    {
                        retained[s] = new List<int>(); detached[s] = new List<int>(); var triangles = source.GetTriangles(s);
                        for (int t = 0; t < triangles.Length; t += 3)
                        {
                            bool take = influence[triangles[t]] + influence[triangles[t + 1]] + influence[triangles[t + 2]] >= 1.5f;
                            var output = take ? detached[s] : retained[s];
                            output.Add(triangles[t]); output.Add(triangles[t + 1]); output.Add(triangles[t + 2]);
                            if (take) count += 3;
                        }
                    }
                    if (count == 0) continue;
                    part = new Part { Skin = skin, Original = source, Cut = UnityEngine.Object.Instantiate(source), Until = Time.time + 15f };
                    part.Cut.name = "WW.Chainsaw." + source.name; part.Cut.hideFlags = HideFlags.DontSave;
                    for (int s = 0; s < retained.Length; s++) part.Cut.SetTriangles(retained[s], s, false);
                    using (var baked = new Baked(skin)) part.LooseMesh = Detached(baked.Mesh, detached);
                    part.Loose = new GameObject("WW.Chainsaw.Limb") { hideFlags = HideFlags.DontSave, layer = skin.gameObject.layer };
                    part.Loose.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation); part.Loose.transform.localScale = skin.transform.lossyScale;
                    part.Loose.AddComponent<MeshFilter>().sharedMesh = part.LooseMesh;
                    part.Loose.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    var box = part.Loose.AddComponent<BoxCollider>(); box.center = part.LooseMesh.bounds.center; box.size = part.LooseMesh.bounds.size;
                    var rb = part.Loose.AddComponent<Rigidbody>(); rb.mass = 2f; rb.AddForce(Vector3.up * .7f, ForceMode.VelocityChange);
                    parts.Add(part); skin.sharedMesh = part.Cut; removed = true;
                }
                catch (Exception e)
                {
                    if (part != null) Release(part);
                    Core.Log.Warn("[NPC] Chainsaw limb mesh: " + e.Message);
                }
            }
            if (removed)
            {
                var owner = parts[parts.Count - 1]; owner.Hidden = limb.GetComponentsInChildren<Renderer>(true); owner.Enabled = new bool[owner.Hidden.Length];
                for (int i = 0; i < owner.Hidden.Length; i++) { owner.Enabled[i] = owner.Hidden[i].enabled; owner.Hidden[i].enabled = false; }
                if (NpcModule.BrainLog.Value) NpcModule.Log.LogInfo("[NPC] Chainsaw severed " + limb.name + " on " + corpse.name);
            }
        }

        private static float Weight(bool[] selected, int index, float weight) { return index >= 0 && index < selected.Length && selected[index] ? weight : 0f; }
        private sealed class Baked : IDisposable
        {
            internal readonly Mesh Mesh = new Mesh();
            internal Baked(SkinnedMeshRenderer skin) { skin.BakeMesh(Mesh); }
            public void Dispose() { UnityEngine.Object.Destroy(Mesh); }
        }
        private static Mesh Detached(Mesh baked, List<int>[] triangles)
        {
            var old = baked.vertices; var normals = baked.normals; var uv = baked.uv;
            var vertices = new List<Vector3>(); var nn = new List<Vector3>(); var tex = new List<Vector2>(); var mapping = new Dictionary<int, int>();
            for (int s = 0; s < triangles.Length; s++)
                for (int i = 0; i < triangles[s].Count; i++)
                {
                    int index = triangles[s][i], mapped;
                    if (!mapping.TryGetValue(index, out mapped))
                    {
                        mapped = vertices.Count; mapping[index] = mapped; vertices.Add(old[index]);
                        nn.Add(normals.Length == old.Length ? normals[index] : Vector3.up); tex.Add(uv.Length == old.Length ? uv[index] : Vector2.zero);
                    }
                    triangles[s][i] = mapped;
                }
            var mesh = new Mesh { name = "WW.Chainsaw.Detached", hideFlags = HideFlags.DontSave, indexFormat = baked.indexFormat };
            mesh.SetVertices(vertices); mesh.SetNormals(nn); mesh.SetUVs(0, tex); mesh.subMeshCount = triangles.Length;
            for (int s = 0; s < triangles.Length; s++) mesh.SetTriangles(triangles[s], s, false);
            mesh.RecalculateBounds(); return mesh;
        }

        internal static void Tick()
        {
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var part = parts[i];
                if (part.Skin == null) { Release(part); parts.RemoveAt(i); continue; }
                if (part.Loose != null && Time.time >= part.Until) DropLoose(part);
            }
        }
        internal static void Saving(bool value)
        {
            foreach (var part in parts)
            {
                if (part.Skin != null) part.Skin.sharedMesh = value ? part.Original : part.Cut;
                if (part.Loose != null) part.Loose.SetActive(!value);
                if (part.Hidden != null) for (int i = 0; i < part.Hidden.Length; i++) if (part.Hidden[i] != null) part.Hidden[i].enabled = value && part.Enabled[i];
            }
        }
        private static void DropLoose(Part part)
        {
            if (part.Loose != null) UnityEngine.Object.Destroy(part.Loose);
            if (part.LooseMesh != null) UnityEngine.Object.Destroy(part.LooseMesh);
            part.Loose = null; part.LooseMesh = null;
        }
        private static void Release(Part part)
        {
            if (part.Skin != null && part.Skin.sharedMesh == part.Cut) part.Skin.sharedMesh = part.Original;
            if (part.Hidden != null) for (int i = 0; i < part.Hidden.Length; i++) if (part.Hidden[i] != null) part.Hidden[i].enabled = part.Enabled[i];
            DropLoose(part); if (part.Cut != null) UnityEngine.Object.Destroy(part.Cut);
        }
        internal static void Clear() { foreach (var part in parts) Release(part); parts.Clear(); }
    }
}
#endif
