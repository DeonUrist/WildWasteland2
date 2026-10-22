using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// Core part of <see cref="SaveRuntime"/> (Wave A split): player queries used by VehicleLoadPlacement/InventorySupplies
    /// (Health, InCar; B01 moves them to GameRefs/PlayerState), the in-vehicle-load placement probe (Surface) and the
    /// InventorySupplies install hook. B01 takes this over.
    /// </summary>
    internal static partial class SaveRuntime
    {
        static PlayMakerFSM _health, _inCar;

        /// <summary>Installs the InventorySupplies fix (inside SaveRuntime.Install, before the HUD overlay, as before).</summary>
        static partial void InstallFixes() { InventorySupplies.Install(); }

        /// <summary>True while the player's InCar FSM is in any state but OnFoot (FSM cached per scene).</summary>
        internal static bool InCar
        {
            get { if (_inCar == null && GameRefs.Player != null) _inCar = FsmUtil.Find(GameRefs.Player, "InCar"); return _inCar != null && _inCar.ActiveStateName != "OnFoot"; }
        }

        /// <summary>Player health from the Health FSM (0 when unavailable; FSM cached per scene).</summary>
        internal static float Health()
        {
            if (_health == null && GameRefs.Player != null) _health = FsmUtil.Find(GameRefs.Player, "Health");
            var value = _health != null ? _health.FsmVariables.FindFsmFloat("Health") : null;
            return value != null ? value.Value : 0;
        }

        /// <summary>
        /// Finds a free standing spot near center on main (non-draft) terrain or a walkable surface: rings of downward probes,
        /// capsule overlap check ignoring the player. False until the main terrain tile is applied.
        /// </summary>
        internal static bool Surface(Vector3 center, Transform player, out Vector3 position)
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
