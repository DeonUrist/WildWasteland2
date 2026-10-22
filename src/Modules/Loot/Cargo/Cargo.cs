using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Loot;

namespace WildWasteland.Modules.Loot
{
    /// <summary>
    /// Cargo loot of patrol cars and trucks (Loot, ex Patrols Vehicles/Cargo.cs). Every vehicle has parts/PhysicsLock: BoxColliders
    /// on layer 18 (floor and ceiling of the cargo volume). Vanilla items carry a LockPhysics FSM that raycasts up and down on that
    /// layer and, when both hit, parents the item to the lower box and destroys its Rigidbody - that is how cargo rides along until
    /// the player grabs it (GrabItem sends LockPhysics_OFF, the FSM re-adds the Rigidbody). Items are spawned inside that volume
    /// with the vanilla spawn recipe (named and registered through Core ItemSpawner, so the game saves them) and locked the same
    /// way at once. Runs inside the Vehicles build queue coroutine (one item per slice). Prefab names resolve through the Patrols
    /// prefab matcher and the slice token is the Vehicles BuildQueue's (REFACTOR-REQUEST B15-3). Namespace kept for Wave C.
    /// </summary>
    internal static class Cargo
    {
        /// <summary>One resolved loot prefab and its count.</summary>
        private class Slot
        {
            /// <summary>Item prefab.</summary>
            public GameObject Prefab;
            /// <summary>Copies to load.</summary>
            public int Count;
        }

        /// <summary>Reused hit buffer of the bed/seat surface probes (main thread only).</summary>
        private static readonly RaycastHit[] Hits = new RaycastHit[64];

        /// <summary>The top of the car's own solid geometry under a point: a ray from 1.2 m above, down 2.5 m, ignoring triggers,
        /// the crew (Driver/Passenger/_Dead) and the PhysicsLock volume; the point's own height when nothing is hit.</summary>
        private static float SeatSurface(GameObject car, Vector3 p)
        {
            float best = float.MinValue;
            int count = Physics.RaycastNonAlloc(p + car.transform.up * 1.2f, -car.transform.up, Hits, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var h = Hits[i];
                if (h.collider == null || !h.collider.transform.IsChildOf(car.transform)) continue;
                bool occupant = false;
                for (var a = h.collider.transform; a != null && a != car.transform; a = a.parent)
                    if (a.name.IndexOf("(Driver)", StringComparison.Ordinal) >= 0 || a.name.IndexOf("(Passenger)", StringComparison.Ordinal) >= 0
                        || a.name.IndexOf("_Dead", StringComparison.Ordinal) >= 0 || a.name == "PhysicsLock") { occupant = true; break; }
                if (occupant) continue;
                if (h.point.y > best) best = h.point.y;
            }
            return best > float.MinValue ? best : p.y;
        }

        /// <summary>Loads rolled loot (prefab name -> count) into a car (around the rear seat) or a truck bed; spawned items are
        /// appended to <paramref name="items"/> (for <see cref="SettleFsms"/>). Unknown prefab names are skipped with a warning.</summary>
        internal static IEnumerator Load(GameObject car, Dictionary<string, int> counts, bool truck, List<GameObject> items)
        {
            var slots = new List<Slot>();
            foreach (var pair in counts)
            {
                var prefab = PrefabCatalog.Find(pair.Key); // refs-ok: REFACTOR-REQUEST B15-3
                if (prefab != null && pair.Value > 0) slots.Add(new Slot { Prefab = prefab, Count = pair.Value });
                else Log.Warn(null,"[Patrols] " + "Loot prefab not found: " + pair.Key);
            }
            if (truck) yield return LoadBed(car, slots, items);
            else yield return LoadCar(car, slots, items);
        }

        /// <summary>Car (no bed): items dropped one per slice around the rear seat (else behind the driver), standing on the seat surface, locked to PhysicsLock (or the car root).</summary>
        private static IEnumerator LoadCar(GameObject car, List<Slot> slots, List<GameObject> items)
        {
            var ct = car.transform;
            var lockGo = FindChild(ct, "PhysicsLock") ?? ct;
            var rear = FindChild(ct, "hinge_seat_rear");
            var driver = FindChild(ct, "hinge_seat_driver");
            Vector3 spot = rear != null && rear.gameObject.activeInHierarchy ? rear.position : driver != null ? driver.position - ct.forward * 0.6f : ct.position;
            foreach (var slot in slots) for (int i = 0; i < slot.Count; i++)
            {
                yield return null; if (car == null) yield break; // refs-ok: REFACTOR-REQUEST B15-3
                Vector3 p = spot + ct.right * UnityEngine.Random.Range(-0.3f, 0.3f) + ct.forward * UnityEngine.Random.Range(-0.2f, 0.2f);
                var go = UnityEngine.Object.Instantiate(slot.Prefab, p + ct.up, ct.rotation);
                go.SetActive(true); var bounds = Bounds(go);
                float y = SeatSurface(car, p) + (go.transform.position.y - bounds.min.y) + 0.02f;
                go.transform.position = new Vector3(p.x, y, p.z);
                ItemSpawner.Name(go, slot.Prefab.name); ItemSpawner.AddExisting(go, false, car); Lock(go, lockGo); items.Add(go);
            }
        }

        /// <summary>Truck: items scattered over the largest PhysicsLock zone's floor (random yaw, cans often on their side), stacked on
        /// earlier items when no free spot is found in 25 tries, one per slice; cars without a PhysicsLock box fall back to <see cref="LoadCar"/>.</summary>
        private static IEnumerator LoadBed(GameObject car, List<Slot> slots, List<GameObject> items)
        {
            if (slots.Count == 0) yield break;

            var lockGo = FindChild(car.transform, "PhysicsLock");
            var boxes = lockGo != null ? lockGo.GetComponents<BoxCollider>() : null;
            if (boxes == null || boxes.Length < 1)
            {
                Log.Warn(null,"[Patrols] " + "Cargo: " + car.name + " has no parts/PhysicsLock box; no cargo");
                yield return LoadCar(car, slots, items); yield break;
            }
            // each BoxCollider is one lock zone (an item inside it, or up to 10 m above it, gets locked by its own LockPhysics raycasts);
            // the bed is the zone with the largest footprint
            BoxCollider bed = boxes[0];
            foreach (var bx in boxes) if (bx.size.x * bx.size.z > bed.size.x * bed.size.z) bed = bx;
            var t = lockGo;
            float minX = bed.center.x - bed.size.x / 2f + 0.15f, maxX = bed.center.x + bed.size.x / 2f - 0.15f;
            float minZ = bed.center.z - bed.size.z / 2f + 0.15f, maxZ = bed.center.z + bed.size.z / 2f - 0.15f;
            float floorY = bed.center.y - bed.size.y / 2f + 0.02f;
            float ceilY = bed.center.y + bed.size.y / 2f;
            var scale = t.lossyScale;
            if (Log.L2)
            {
                foreach (var bx in boxes) Log.Deep(LootModule.Instance, "Cargo: lock zone centre " + bx.center.ToString("0.00") + " size " + bx.size.ToString("0.00") + (bx == bed ? " (bed)" : ""));
                Log.Deep(LootModule.Instance, "Cargo: volume x " + minX.ToString("0.00") + ".." + maxX.ToString("0.00") + " z " + minZ.ToString("0.00") + ".." + maxZ.ToString("0.00")
                    + " y " + floorY.ToString("0.00") + ".." + ceilY.ToString("0.00") + " (local, scale " + scale + ")");
            }
            if (maxX <= minX || maxZ <= minZ || ceilY <= floorY) { Log.Warn(null,"[Patrols] " + "Cargo: PhysicsLock volume is degenerate on " + car.name); yield break; }

            // scatter over the bed floor: random XZ that keeps clear of the items already placed (a few tries, then stack on top)
            var placedPos = new List<Vector3>(); var placedR = new List<float>(); var placedTop = new List<float>();
            int placed = 0, wanted = 0, sinceYield = 0;
            bool full = false;
            foreach (var slot in slots)
            {
                if (full) break;
                wanted += slot.Count;
                for (int i = 0; i < slot.Count; i++)
                {
                    if (sinceYield > 0) { sinceYield = 0; yield return null; if (car == null || t == null) yield break; } // refs-ok: REFACTOR-REQUEST B15-3
                    sinceYield++;
                    var go = UnityEngine.Object.Instantiate(slot.Prefab, t.TransformPoint(new Vector3(0f, ceilY + 5f, 0f)), t.rotation);
                    go.SetActive(true);
                    // orientation: random yaw; cans / bottles / canisters lie on their side more often than not
                    var rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                    if (IsCanLike(slot.Prefab.name) && UnityEngine.Random.value < 0.6f)
                        rot = rot * Quaternion.Euler(UnityEngine.Random.value < 0.5f ? 90f : -90f, 0f, UnityEngine.Random.Range(-10f, 10f));
                    go.transform.rotation = t.rotation * rot;
                    var b = Bounds(go);
                    float r = Mathf.Max(b.size.x, b.size.z) / 2f / Mathf.Max(scale.x, 0.01f) + 0.03f;
                    float h = b.size.y / Mathf.Max(scale.y, 0.01f);
                    float lx = 0f, lz = 0f, baseY = -1f; bool free = false;
                    for (int attempt = 0; attempt < 25 && !free; attempt++)
                    {
                        lx = UnityEngine.Random.Range(minX + r, maxX - r); lz = UnityEngine.Random.Range(minZ + r, maxZ - r);
                        if (maxX - minX < 2f * r) lx = (minX + maxX) / 2f;
                        if (maxZ - minZ < 2f * r) lz = (minZ + maxZ) / 2f;
                        free = true;
                        for (int k = 0; k < placedPos.Count && free; k++)
                        {
                            float dx = placedPos[k].x - lx, dz = placedPos[k].z - lz;
                            if (dx * dx + dz * dz < (placedR[k] + r) * (placedR[k] + r)) free = false;
                        }
                    }
                    if (!free)
                    {
                        // no free spot: put it on top of whatever is there
                        for (int k = 0; k < placedPos.Count; k++)
                        {
                            float dx = placedPos[k].x - lx, dz = placedPos[k].z - lz;
                            if (dx * dx + dz * dz < (placedR[k] + r) * (placedR[k] + r)) baseY = Mathf.Max(baseY, placedTop[k]);
                        }
                    }
                    if (baseY < 0f) baseY = FloorAt(car, t, lx, lz, floorY, ceilY);
                    // Locked loot may stack above the box; do not discard guaranteed items when the bed is full.
                    // move the renderer bottom-centre onto (lx, baseY, lz)
                    var pivotWorld = go.transform.position;
                    var bottomWorld = new Vector3(b.center.x, b.min.y, b.center.z);
                    var pivotOffsetLocal = t.InverseTransformVector(pivotWorld - bottomWorld);
                    go.transform.position = t.TransformPoint(new Vector3(lx, baseY + 0.01f, lz) + pivotOffsetLocal);
                    placedPos.Add(new Vector3(lx, baseY, lz)); placedR.Add(r); placedTop.Add(baseY + h + 0.01f);

                    ItemSpawner.Name(go, slot.Prefab.name); ItemSpawner.AddExisting(go, false, car);
                    Lock(go, t);
                    items.Add(go);
                    placed++;
                }
            }
            if (Log.L2) Log.Deep(LootModule.Instance, "Cargo: " + placed + " item(s) loaded into " + car.name);
        }

        /// <summary>First descendant with an exact name, breadth of each level first (same order as Patrols Patrol.FindChild).</summary>
        private static Transform FindChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++) { var c = root.GetChild(i); if (c.name == name) return c; }
            for (int i = 0; i < root.childCount; i++) { var r = FindChild(root.GetChild(i), name); if (r != null) return r; }
            return null;
        }

        /// <summary>Cans, bottles, canisters, jerry cans and barrels (by prefab name) often lie on their side.</summary>
        private static bool IsCanLike(string prefab)
        {
            string n = prefab.ToLowerInvariant();
            return n.Contains("can") || n.Contains("bottle") || n.Contains("canister") || n.Contains("jerry") || n.Contains("barrel");
        }

        /// <summary>The real floor under (lx, lz) in lock-zone space: highest car collider hit below the zone ceiling, else the zone bottom.</summary>
        private static float FloorAt(GameObject car, Transform t, float lx, float lz, float floorY, float ceilY)
        {
            var from = t.TransformPoint(new Vector3(lx, ceilY - 0.05f, lz));
            float best = -1f;
            int count = Physics.RaycastNonAlloc(from, -t.up, Hits, ceilY - floorY + 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var h = Hits[i];
                if (h.collider == null || !h.collider.transform.IsChildOf(car.transform)) continue;
                float ly = t.InverseTransformPoint(h.point).y;
                if (ly < floorY - 0.3f) continue;
                if (best < 0f || ly > best) best = ly;
            }
            return best >= 0f ? best : floorY;
        }

        /// <summary>What the item's own LockPhysics FSM would do 3 s later: parent to the PhysicsLock object, no Rigidbody.</summary>
        private static void Lock(GameObject go, Transform lockGo)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) UnityEngine.Object.DestroyImmediate(rb);
            go.transform.SetParent(lockGo, true);
        }

        /// <summary>After the build: parks every loaded item's LockPhysics FSM in "off" (the item is already locked).</summary>
        internal static void SettleFsms(List<GameObject> items)
        {
            foreach (var go in items)
            {
                if (go == null) continue;
                foreach (var f in go.GetComponents<PlayMakerFSM>())
                    if (f.FsmName == "LockPhysics" && f.Fsm.Initialized && f.ActiveStateName != "off")
                        f.Fsm.SetState("off");
            }
        }

        /// <summary>World bounds of an item's renderers (particles excluded), else its colliders, else a 0.3 m cube at its position.</summary>
        private static Bounds Bounds(GameObject go)
        {
            bool any = false; var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r.GetType().Name == "ParticleSystemRenderer") continue;   // smoke, not the item
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any)
                foreach (var c in go.GetComponentsInChildren<Collider>())
                { if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds); }
            if (!any) b = new Bounds(go.transform.position, new Vector3(0.3f, 0.3f, 0.3f));
            return b;
        }
    }
}
