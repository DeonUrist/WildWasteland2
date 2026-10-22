using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// <summary>Tracers part: the visible crossbow bolt (native arrow mesh) in flight and after landing.</summary>
    internal static partial class Tracers
    {
        private static Mesh _boltMesh;
        private static Material[] _boltMaterials;
        private static Vector3 _boltScale;
        private static readonly List<LandedBolt> _landedBolts = new List<LandedBolt>();

        // Native crossbow arrow: a plain mesh pointing along +Z, also present in NPC props.
        /// <summary>Remembers the native crossbow arrow mesh, materials and scale.</summary>
        private static void CacheBoltModel(Transform weapon)
        {
            if (_boltMesh != null || weapon == null) return;
            foreach (var mf in weapon.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name != "arrow" || mf.sharedMesh == null) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                _boltMesh = mf.sharedMesh; _boltMaterials = mr.sharedMaterials;
                var scale = mf.transform.lossyScale;
                _boltScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                return;
            }
            GunplayLog.Warn("Crossbow bolt: native arrow mesh missing on " + weapon.name);
        }

        /// <summary>A crossbow shot gets its visible arrow.</summary>
        private static void LaunchBolt(ref Shot s)
        {
            if (!s.Bolt) return;
            if (_boltMesh == null) { var arm = PlayerState.WeaponsArm; if (arm != null) CacheBoltModel(arm); }
            if (_boltMesh == null) return;
            var go = new GameObject("WildWasteland.Guns.Bolt") { hideFlags = HideFlags.DontSave, layer = 9 };
            go.AddComponent<MeshFilter>().sharedMesh = _boltMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = _boltMaterials;
            s.Visual = go.transform; s.Visual.localScale = _boltScale;
            MoveBolt(ref s);
        }

        /// <summary>Places the arrow at the bolt's simulated position.</summary>
        private static void MoveBolt(ref Shot s) { MoveBoltAt(ref s, s.Pos); }
        /// <summary>Places the arrow so its tip is at <paramref name="pos"/>, pointing along the flight.</summary>
        private static void MoveBoltAt(ref Shot s, Vector3 pos)
        {
            if (s.Visual != null) s.Visual.SetPositionAndRotation(pos - s.Dir * (_boltMesh.bounds.max.z * _boltScale.z), Quaternion.LookRotation(s.Dir));
        }

        /// <summary>The bolt hit: stuck halfway in soft targets (bone-anchored), ricochets off hard ones; BoltLanded event; removed after 15 s.</summary>
        private static void LandBolt(ref Shot s, Vector3 point, Vector3 normal, Transform target, bool soft)
        {
            if (s.Visual == null) return;
            var bolt = s.Visual; var bounds = _boltMesh.bounds;
            Transform owner = null, anchor = target;
            if (soft)
            {
                int layer;
                anchor = BoltSurface(target, ref point, s.Dir, out owner, out layer);
                bolt.gameObject.layer = layer;
                // Mesh centre on the surface puts exactly half the shaft inside it.
                bolt.SetPositionAndRotation(point - Quaternion.LookRotation(s.Dir) * Vector3.Scale(bounds.center, _boltScale), Quaternion.LookRotation(s.Dir));
                bolt.SetParent(anchor, true);
            }
            else
            {
                s.Pos = point + normal * 0.03f; MoveBolt(ref s);
                var physics = new GameObject("BoltPhysics") { layer = 2 }; physics.transform.SetParent(bolt, false);
                var col = physics.AddComponent<CapsuleCollider>();
                col.direction = 2; col.center = bounds.center; col.height = bounds.size.z;
                col.radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
                var rb = bolt.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.03f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.velocity = Vector3.Reflect(s.Dir, normal) * Mathf.Min(s.Speed * 0.15f, 8f) + normal;
                rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 10f;
            }
            _landedBolts.Add(new LandedBolt { Visual = bolt, Owner = owner, Bone = owner != null ? BoltBonePath(anchor, owner) : null, Until = Time.time + 15f });
            CombatEvents.BoltLanded(s.Id, soft ? (owner != null ? owner : target) : null, owner != null ? BoltBonePath(anchor, owner) : null, bolt, 15f);
            s.Visual = null;
            if (Log.L2) GunplayLog.Deep("Crossbow bolt: " + (soft ? "embedded halfway in " : "ricochet from ") + (target != null ? target.name : "world") + " (15 s)");
        }

        /// <summary>Removes landed bolts whose time is up.</summary>
        private static void TickBolts()
        {
            for (int i = _landedBolts.Count - 1; i >= 0; i--)
            {
                var b = _landedBolts[i];
                if (b.Visual != null && Time.time < b.Until) continue;
                if (b.Visual != null) UnityEngine.Object.Destroy(b.Visual.gameObject);
                _landedBolts.RemoveAt(i);
            }
        }

        /// <summary>Destroys every bolt and skin-intersection cache.</summary>
        private static void ClearBolts()
        {
            foreach (var s in _shots) if (s.Visual != null) UnityEngine.Object.Destroy(s.Visual.gameObject);
            foreach (var b in _landedBolts) if (b.Visual != null) UnityEngine.Object.Destroy(b.Visual.gameObject);
            _landedBolts.Clear(); _boltMesh = null; _boltMaterials = null;
            _boltSkins.Clear(); _boltVertices.Clear(); _boltTriangles.Clear(); _boltWeights.Clear();
            if (_boltSkin != null) UnityEngine.Object.Destroy(_boltSkin); _boltSkin = null;
        }
    }
}
