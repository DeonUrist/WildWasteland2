using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Native main-terrain standing-clearance probes used by load placement and save cleanup.</summary>
    internal static class StandingSurface
    {
        /// <summary>
        /// Finds a free standing spot near center on main (non-draft) terrain or a walkable surface: rings of downward probes,
        /// capsule overlap check ignoring the player. False until the main terrain tile is applied.
        /// </summary>
        internal static bool TryFind(Vector3 center, Transform player, out Vector3 position)
        {
            // Main terrain must have replaced the draft before any object/floor is used as support.
            RaycastHit terrain;
            if (!Physics.Raycast(center + Vector3.up * 1500f, Vector3.down, out terrain, 3000f, 1 << 14, QueryTriggerInteraction.Ignore) || terrain.collider.name == "Draft Terrain")
            { position = Vector3.zero; return false; }
            var tile = terrain.collider.GetComponentInParent<MapMagic.Terrains.TerrainTile>();
            if (!(terrain.collider is TerrainCollider) || !terrain.collider.enabled || !terrain.collider.gameObject.activeInHierarchy
                || tile != null && (tile.main == null || !tile.main.applyReady || tile.main.terrain == null || terrain.collider.gameObject != tile.main.terrain.gameObject))
            { position = Vector3.zero; return false; }
            for (int ring = 0; ring < 5; ring++) for (int step = 0; step < 12; step++)
            {
                float angle = step * Mathf.PI / 6f, radius = ring == 0 ? 0 : ring * 1.5f;
                var probe = center + new Vector3(Mathf.Cos(angle) * radius, 4f, Mathf.Sin(angle) * radius);
                int count = Physics.RaycastNonAlloc(probe, Vector3.down, SurfaceHits, 40f, SurfaceMask, QueryTriggerInteraction.Ignore);
                if (count >= SurfaceHits.Length) continue;
                for (int i = 0; i < count; i++)
                {
                    var hit = SurfaceHits[i]; if (hit.normal.y < .7f || hit.collider.name == "Draft Terrain" || hit.collider.transform.IsChildOf(player) || hit.point.y < terrain.point.y - .5f) continue;
                    var feet = hit.point + Vector3.up * .08f;
                    int blocked = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * .35f, feet + Vector3.up * 1.55f, .32f, Capsule, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    bool free = blocked < Capsule.Length;
                    for (int j = 0; j < blocked && j < Capsule.Length; j++) if (!Capsule[j].transform.IsChildOf(player)) { free = false; break; }
                    if (!free) continue;
                    position = hit.point + Vector3.up * 1.05f; return true;
                }
            }
            position = Vector3.zero; return false;
        }
        const int SurfaceMask = (1 << 0) | (1 << 8) | (1 << 14) | (1 << 16);
        static readonly Collider[] Capsule = new Collider[32];
        static readonly RaycastHit[] SurfaceHits = new RaycastHit[32];
    }
}
