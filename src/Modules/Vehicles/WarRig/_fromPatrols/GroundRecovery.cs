using System.Collections.Generic;
using NWH.VehiclePhysics2;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    // Runtime-only evidence. Never rescue merely because a terrain-height sample is overhead.
    internal sealed class GroundRecovery
    {
        private Terrain previousTerrain, safeTerrain;
        private Vector3 previousLocal, safeLocal;
        private bool hasPrevious, hasSafe, crossed;
        private float safeAge, aboveTime, belowTime, holdTime, cooldown, observeTime;
        private readonly List<Rigidbody> held = new List<Rigidbody>();

        internal void Reset()
        {
            previousTerrain = safeTerrain = null;
            hasPrevious = hasSafe = crossed = false;
            safeAge = aboveTime = belowTime = holdTime = cooldown = observeTime = 0;
            held.Clear(); colliders.Clear(); collidersOf = null; collidersAt = 0;
        }

        internal void Tick(Rigidbody body, float dt)
        {
            bool enabled = S.GroundRecoveryEnabled.Value;
            if (!enabled)
            {
                // Observe at 5 Hz so opting in after a fall can use real crossing evidence.
                // Disabled recovery never writes body positions or velocities, even mid-hold.
                holdTime = belowTime = 0; held.Clear();
            }
            else if (holdTime > 0)
            {
                // the post-rescue hold stays per physics step
                Stop(held); holdTime -= dt;
                if (holdTime <= 0) held.Clear();
                return;
            }
            // Perf: sampled at 10 Hz enabled (was every physics step) / 5 Hz disabled, with the accumulated dt
            observeTime += dt;
            if (observeTime + .000001f < (enabled ? .1f : .2f)) return;
            dt = observeTime; observeTime = 0;
            cooldown = Mathf.Max(0, cooldown - dt); safeAge += dt;
            if (body == null || body.isKinematic || !body.gameObject.activeInHierarchy)
            { hasPrevious = hasSafe = crossed = false; aboveTime = belowTime = 0; return; }
            Terrain terrain; float surface;
            Bounds bounds;
            if (Time.unscaledTime >= collidersAt || !ReferenceEquals(collidersOf, body))
            { collidersAt = Time.unscaledTime + 2f; collidersOf = body; body.GetComponentsInChildren(colliders); }
            if (!Surface(body.position, out terrain, out surface) || !BodyBounds(body, colliders, out bounds))
            { hasPrevious = hasSafe = crossed = false; aboveTime = belowTime = 0; return; }

            if (hasPrevious && previousTerrain == terrain && hasSafe && safeAge < 30 && cooldown == 0)
            {
                var before = terrain.transform.TransformPoint(previousLocal);
                var movement = body.position - before;
                Terrain oldTerrain; float oldHeight;
                RaycastHit hit;
                var col = ColliderOf(terrain);
                // A falling body must actually cross the solid top surface. Excludes teleports,
                // sideways cave entry, terrain holes, and a world-origin translation.
                if (before.y >= surface && body.position.y < surface && body.velocity.y < -2f &&
                    movement.y < 0 && movement.magnitude < 20f &&
                    Surface(before, out oldTerrain, out oldHeight) && oldTerrain == terrain && before.y >= oldHeight &&
                    col.Raycast(new Ray(before, movement.normalized), out hit, movement.magnitude)) crossed = true;
            }
            previousTerrain = terrain; previousLocal = terrain.transform.InverseTransformPoint(body.position); hasPrevious = true;

            float low, high;
            if (!Heights(bounds, out low, out high)) { crossed = false; aboveTime = belowTime = 0; return; }
            if (body.position.y > surface + .25f && bounds.min.y >= high - 1.5f)
            {
                crossed = false; belowTime = 0; aboveTime += dt;
                if (aboveTime >= .5f)
                {
                    safeTerrain = terrain; safeLocal = terrain.transform.InverseTransformPoint(body.position);
                    hasSafe = true; safeAge = 0;
                }
                return;
            }
            aboveTime = 0;
            if (!crossed || !hasSafe || safeAge >= 30 || cooldown > 0 || bounds.max.y >= low - 3f)
            { belowTime = 0; return; }
            if (!S.GroundRecoveryEnabled.Value) return;

            var vehicles = Vehicles(body);
            foreach (var vehicle in vehicles)
            {
                Bounds b;
                if (!BodyBounds(vehicle, out b) || !Heights(b, out low, out high) || b.max.y >= low - 3f)
                { belowTime = 0; return; }
            }
            belowTime += dt;
            if (belowTime < .4f || !Recover(body, vehicles)) return;
            crossed = hasPrevious = hasSafe = false; belowTime = aboveTime = 0; cooldown = 5f;
        }

        // Perf: active terrains and their colliders cached once per rendered frame (was an array alloc + GetComponent per sample)
        private static Terrain[] terrains = new Terrain[0];
        private static TerrainCollider[] terrainCols = new TerrainCollider[0];
        private static int terrainFrame = -1;
        private static void CacheTerrains()
        {
            if (terrainFrame == Time.frameCount) return;
            terrainFrame = Time.frameCount; terrains = Terrain.activeTerrains;
            if (terrainCols.Length != terrains.Length) terrainCols = new TerrainCollider[terrains.Length];
            for (int i = 0; i < terrains.Length; i++) terrainCols[i] = terrains[i] != null ? terrains[i].GetComponent<TerrainCollider>() : null;
        }
        private static TerrainCollider ColliderOf(Terrain t)
        {
            CacheTerrains();
            for (int i = 0; i < terrains.Length; i++) if (ReferenceEquals(terrains[i], t)) return terrainCols[i];
            return t.GetComponent<TerrainCollider>();
        }

        internal static bool Surface(Vector3 point, out Terrain terrain, out float height)
        {
            terrain = null; height = 0;
            CacheTerrains();
            for (int i = 0; i < terrains.Length; i++)
            {
                var t = terrains[i];
                if (t == null || t.terrainData == null || !t.gameObject.activeInHierarchy) continue;
                var col = terrainCols[i];
                if (col == null || !col.enabled || col.terrainData != t.terrainData) continue;
                var p = point - t.transform.position; var size = t.terrainData.size;
                if (p.x < 0 || p.z < 0 || p.x > size.x || p.z > size.z) continue;
                RaycastHit hit;
                // Raycast the actual terrain collider, not SampleHeight: holes must remain holes.
                var top = new Vector3(point.x, t.transform.position.y + size.y + 10f, point.z);
                if (!col.Raycast(new Ray(top, Vector3.down), out hit, size.y + 20f)) continue;
                if (terrain == null || hit.point.y > height) { terrain = t; height = hit.point.y; }
            }
            return terrain != null;
        }

        private static bool Heights(Bounds bounds, out float low, out float high)
        {
            low = float.PositiveInfinity; high = float.NegativeInfinity;
            // Require loaded, solid terrain under the centre and all four footprint corners.
            for (int i = 0; i < 5; i++)
            {
                var p = bounds.center;
                if (i != 0) { p.x = (i <= 2 ? bounds.min.x : bounds.max.x); p.z = (i % 2 == 0 ? bounds.min.z : bounds.max.z); }
                Terrain t; float h;
                if (!Surface(p, out t, out h)) return false;
                low = Mathf.Min(low, h); high = Mathf.Max(high, h);
            }
            return true;
        }

        private static readonly List<Collider> scratch = new List<Collider>();
        private readonly List<Collider> colliders = new List<Collider>();   // the rig's colliders, refreshed every 2 s
        private float collidersAt; private Rigidbody collidersOf;
        private static bool BodyBounds(Rigidbody body, out Bounds bounds)
        { body.GetComponentsInChildren(scratch); bool found = BodyBounds(body, scratch, out bounds); scratch.Clear(); return found; }
        private static bool BodyBounds(Rigidbody body, List<Collider> list, out Bounds bounds)
        {
            bounds = new Bounds(); bool found = false;
            foreach (var c in list)
            {
                if (c == null || !c.enabled || c.isTrigger || c.attachedRigidbody != body || !c.gameObject.activeInHierarchy) continue;
                if (!found) bounds = c.bounds; else bounds.Encapsulate(c.bounds);
                found = true;
            }
            return found;
        }

        private static List<Rigidbody> Vehicles(Rigidbody rig)
        {
            var result = new List<Rigidbody> { rig };
            // The vanilla hitch is a joint on the trailer connected to the truck body.
            // Include joints in either direction, and additional connected trailers.
            var joints = Object.FindObjectsOfType<Joint>(); bool added;
            do
            {
                added = false;
                foreach (var j in joints)
                {
                    if (j.connectedBody == null) continue;
                    var own = j.GetComponent<Rigidbody>();
                    if (own == null) continue;
                    Rigidbody next = result.Contains(own) ? j.connectedBody : result.Contains(j.connectedBody) ? own : null;
                    if (next == null || result.Contains(next) || next.GetComponent<VehicleController>() == null) continue;
                    result.Add(next); added = true;
                }
            } while (added);
            return result;
        }

        private bool Recover(Rigidbody rig, List<Rigidbody> vehicles)
        {
            if (safeTerrain == null || !safeTerrain.gameObject.activeInHierarchy) return false;
            var destination = safeTerrain.transform.TransformPoint(safeLocal) + Vector3.up * 10f;
            var delta = destination - rig.position;
            // Re-check loaded terrain at the destination and clear the complete vehicle footprint.
            float lift = 0;
            foreach (var vehicle in vehicles)
            {
                if (vehicle.isKinematic) return false;
                Bounds b; float low, high;
                if (!BodyBounds(vehicle, out b)) return false;
                b.center += delta;
                if (!Heights(b, out low, out high)) return false;
                lift = Mathf.Max(lift, high + 2f - b.min.y);
            }
            delta += Vector3.up * lift;
            var roots = new List<Transform>();
            foreach (var v in vehicles) roots.Add(v.transform);
            var player = Core.GameRefs.Player;
            if (player != null)
            {
                bool child = false; foreach (var root in roots) if (player.transform.IsChildOf(root)) child = true;
                var p = Quaternion.Inverse(Layout.Rot) * (rig.transform.InverseTransformPoint(player.transform.position) - Layout.Off) / S.Scale;
                if (!child && new Bounds(new Vector3(0, .95f, -1.25f), new Vector3(4.4f, 2f, 17.7f)).Contains(p)) roots.Add(player.transform);
            }
            // Follow attachment joints too (parts/cargo/occupants with independent rigidbodies).
            var all = new HashSet<Rigidbody>();
            foreach (var root in roots) foreach (var rb in root.GetComponentsInChildren<Rigidbody>()) all.Add(rb);
            var joints = Object.FindObjectsOfType<Joint>(); bool added;
            do
            {
                added = false;
                foreach (var j in joints)
                {
                    var own = j.GetComponent<Rigidbody>(); var connected = j.connectedBody;
                    if (own == null || connected == null) continue;
                    var next = all.Contains(own) ? connected : all.Contains(connected) ? own : null;
                    if (next == null || all.Contains(next)) continue;
                    // A world-anchored body must not pull the world into our recovery.
                    if (next.isKinematic && !next.transform.IsChildOf(rig.transform)) return false;
                    all.Add(next); roots.Add(next.transform); added = true;
                    foreach (var child in next.GetComponentsInChildren<Rigidbody>()) all.Add(child);
                }
            } while (added);
            var topRoots = new List<Transform>();
            foreach (var root in roots)
            {
                bool child = false;
                foreach (var other in roots) if (other != root && root.IsChildOf(other)) { child = true; break; }
                if (!child && !topRoots.Contains(root)) topRoots.Add(root);
            }
            var positions = new Dictionary<Rigidbody, Vector3>();
            foreach (var rb in all) positions[rb] = rb.position;
            foreach (var root in topRoots)
            {
                var rb = root.GetComponent<Rigidbody>();
                root.position = (rb != null && positions.ContainsKey(rb) ? positions[rb] : root.position) + delta;
            }
            held.Clear();
            held.AddRange(all);
            // Parent-first absolute placement avoids both double-moving nested bodies and
            // SyncTransforms overwriting a physics-only teleport with an old Transform.
            held.Sort((a, b) => Depth(a.transform).CompareTo(Depth(b.transform)));
            foreach (var rb in held)
            { var p = positions[rb] + delta; rb.transform.position = p; rb.position = p; }
            Physics.SyncTransforms(); Stop(held); holdTime = .2f;
            Core.Log.Warn("[Patrols] " + "War Rig ground recovery: confirmed solid-terrain crossing; rescued " + vehicles.Count + " vehicle bodies together.");
            return true;
        }

        private static void Stop(List<Rigidbody> bodies)
        {
            foreach (var body in bodies)
                if (body != null && !body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }

        private static int Depth(Transform t)
        { int depth = 0; while (t.parent != null) { depth++; t = t.parent; } return depth; }
    }
}
