using UnityEngine;
namespace WildWasteland.Modules.Towns
{
    internal static class TownDebris
    {
        // Two crashed cars in the central wreck garden, placed from the deterministic dressing plan (same seeds as before).
        internal static void Build(TownSite site, TownAssets assets, Transform root)
        {
            var plan = TownOverlayPlan.DressingFor(site.Plan, assets);
            for (int i = 0; i < plan.Garden.Length; i++)
            {
                var item = plan.Garden[i]; if (item.Index >= assets.CarShells.Count) continue;
                var shell = assets.CarShells[item.Index]; var bounds = shell.Mesh.bounds;
                var go = new GameObject("Towns crashed car " + i); go.transform.SetParent(root, false);
                go.transform.rotation = site.Anchor.rotation * Quaternion.Euler(0, item.Yaw, 0); go.transform.localScale = Vector3.one * item.Scale;
                go.transform.position = site.Ground(site.Anchor.TransformPoint(site.Plan.WorldCenter + item.Position), item.Position.y);
                go.AddComponent<MeshFilter>().sharedMesh = shell.Mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterials = shell.Materials;
                var collider = go.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
                var blocker = item.Box; float offset = blocker.center.y; blocker.center += site.Plan.WorldCenter;
                blocker.center = site.Anchor.InverseTransformPoint(site.Ground(site.Anchor.TransformPoint(blocker.center), offset)); blocker.Expand(.3f); site.DebrisBlockers.Add(blocker);
            }
        }
    }
}
