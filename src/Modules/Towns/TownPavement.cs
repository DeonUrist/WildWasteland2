using UnityEngine;
namespace WildWasteland.Modules.Towns
{
    internal static class TownPavement
    {
        // Keep only the parking slab: no internal road/path mesh or terrain painting.
        internal static void Build(TownSite site, TownAssets assets, Transform root)
        {
            if (Core.VistasWorldGen.Present) return;
            if (assets.PavementMaterial == null) return;
            foreach (var lot in site.Plan.Lots) if (lot.IsParking && !lot.Skipped)
            {
                var rotation = Quaternion.Euler(0, lot.Yaw, 0); var vertices = new Vector3[4];
                var corners = new[] { new Vector3(-lot.HalfX, .025f, -lot.HalfZ), new Vector3(-lot.HalfX, .025f, lot.HalfZ), new Vector3(lot.HalfX, .025f, lot.HalfZ), new Vector3(lot.HalfX, .025f, -lot.HalfZ) };
                for (int i = 0; i < 4; i++) vertices[i] = root.InverseTransformPoint(site.Scene(lot.LocalPosition + rotation * corners[i]));
                var mesh = new Mesh { name = "Towns parking slab", vertices = vertices, uv = new[] { Vector2.zero, new Vector2(0, 10), new Vector2(6, 10), new Vector2(6, 0) }, triangles = new[] { 0, 1, 2, 0, 2, 3 } };
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var go = new GameObject("Parking"); go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = assets.PavementMaterial;
            }
        }
    }
}
