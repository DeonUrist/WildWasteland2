using System.Collections.Generic;
using NWH.WheelController3D;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Patrols.WarRig
{
    /// <summary>
    /// Saved component on every War Rig (spawned or loaded): keeps the model, materials and hulls bound after Easy Save writes, gates the
    /// attacher to allowed trucks, mirrors the second leg, runs ground recovery and drives the storm-dust tick of the storm protection.
    /// </summary>
    public sealed class WarRigUnit : MonoBehaviour
    {
        /// <summary>Every enabled War Rig.</summary>
        public static readonly List<WarRigUnit> All = new List<WarRigUnit>();
        // Captured before player attachments are added. Unity remaps these refs when cloning the prefab.
        [SerializeField] private Renderer[] hiddenRenderers = new Renderer[0];
        /// <summary>Stores the vanilla renderers to keep hidden (captured at template build).</summary>
        internal void SetHiddenRenderers(Renderer[] renderers) { hiddenRenderers = renderers; }
        private float nextVisualCheck, fastVisualUntil;
        private static readonly string[] CollisionNodes = { "warrig_colliders", "warrig_hulls" };

        /// <summary>Visual check at 4 Hz for 15 s after enable / a load, else every 5 s (module running): rebinds the model when Easy Save broke it.</summary>
        private void LateUpdate()
        {
            if (!VehiclesModule.Running) return;
            if (SaveBus.Loading) fastVisualUntil = Time.unscaledTime + 15f;
            if (Time.unscaledTime < nextVisualCheck) return;
            // Perf: 4 Hz for 15 s after enable / a load (when Easy Save writes), 5 s otherwise (was 4 Hz always)
            nextVisualCheck = Time.unscaledTime + (Time.unscaledTime < fastVisualUntil ? .25f : 5f);
            // Easy Save can overwrite existing objects after Start, including on a death/reload.
            // Keep collision-only renderers hidden and rebind unresolved visual references after that write.
            try
            {
                var node = transform.Find(Factory.ModelNode);
                if (node == null) return;
                HideCollisionRenderers(node);
                var mf = node.GetComponent<MeshFilter>(); var mr = node.GetComponent<MeshRenderer>();
                if (mf == null || mr == null) return;
                var expected = Factory.Materials(mr.sharedMaterial); var current = mr.sharedMaterials;
                bool broken = mf.sharedMesh != Factory.Model().Mesh || !mr.enabled || current.Length != expected.Length;
                for (int i = 0; !broken && i < current.Length; i++) if (current[i] != expected[i]) broken = true;
                var hulls = node.Find("warrig_hulls");
                if (!broken && hulls != null)
                {
                    var meshes = Factory.Hulls(); var colliders = hulls.GetComponents<MeshCollider>();
                    for (int i = 0; !broken && i < colliders.Length && i < meshes.Count; i++) if (colliders[i].sharedMesh != meshes[i]) broken = true;
                }
                if (broken) Rebind();
            }
            catch (System.Exception e) { Log.Warn(VehiclesModule.Instance, "War Rig visual check: " + e.Message); }
        }

        /// <summary>Keeps the vanilla body and collision-only renderers hidden (attachments stay visible).</summary>
        private void HideCollisionRenderers(Transform node)
        {
            if (hiddenRenderers != null) foreach (var r in hiddenRenderers) if (r != null && r.enabled) r.enabled = false;
            foreach (var name in CollisionNodes)
            {
                var colliders = node.Find(name);
                // Attachments such as spikes are children of the hull object and must stay visible.
                if (colliders != null) foreach (var r in colliders.GetComponents<Renderer>()) if (r.enabled) r.enabled = false;
            }
        }

        /// <summary>Registers the rig and schedules the fast visual checks.</summary>
        private void OnEnable() { if (!All.Contains(this)) All.Add(this); fastVisualUntil = Time.unscaledTime + 15f; nextVisualCheck = 0f; }
        /// <summary>Unregisters the rig; the last one gone restores the storm dust (storm protection).</summary>
        private void OnDisable() { groundRecovery.Reset(); All.Remove(this); }

        private Transform leg, leg2;

        private Transform att;
        private Rigidbody rb;
        private readonly GroundRecovery groundRecovery = new GroundRecovery();
        private static float scanAt;
        private static Transform[] casters = new Transform[0];

        /// <summary>Per frame (module running, scene rig): storm-dust tick, second leg follows the first, attacher gate.</summary>
        private void Update()
        {
            if (!VehiclesModule.Running) return;
            if (!gameObject.scene.IsValid()) return;
            if (leg == null) { leg = transform.Find("leg"); leg2 = transform.Find("leg2"); att = transform.Find("attacher"); rb = GetComponent<Rigidbody>(); }
            if (leg != null && leg2 != null && leg2.gameObject.activeSelf != leg.gameObject.activeSelf) leg2.gameObject.SetActive(leg.gameObject.activeSelf);
            GateAttacher();
        }

        // Only the allowed trucks (Rustchief) may hitch: the attacher (layer 20, what TrailerAttach's sphere cast looks for)
        // colliders are enabled only while the nearest truck hitch (within 8 m) is an allowed one.
        // Perf: 4 Hz per rig with a random phase (was per frame); hitch list (static, 1 Hz) carries each truck's name and Allowed verdict
        private float gateAt = -1f;
        private static readonly List<Transform> casterList = new List<Transform>();
        private static string[] casterTrucks = new string[0];
        private static bool[] casterAllowed = new bool[0];
        /// <summary>4 Hz: enables the attacher colliders only while the nearest truck hitch (within 8 m, list refreshed 1 Hz) is an allowed truck.</summary>
        private void GateAttacher()
        {
            if (att == null) return;
            if (Time.unscaledTime < gateAt) return;
            gateAt = gateAt < 0f ? Time.unscaledTime + .25f * Random.value : Time.unscaledTime + .25f;
            if (Time.unscaledTime > scanAt)
            {
                scanAt = Time.unscaledTime + 1f;
                casterList.Clear();
                foreach (var v in FindObjectsOfType<NWH.VehiclePhysics2.VehicleController>()) { var h = v.transform.Find("hitch_raycast"); if (h != null) casterList.Add(h); }
                casters = casterList.ToArray(); casterTrucks = new string[casters.Length]; casterAllowed = new bool[casters.Length];
                for (int i = 0; i < casters.Length; i++) { casterTrucks[i] = casters[i].parent.name; casterAllowed[i] = Allowed(casterTrucks[i]); }
                casterList.Clear();
            }
            int best = -1; float bd = 8f;
            for (int i = 0; i < casters.Length; i++) { var c = casters[i]; if (c == null) continue; float d = Vector3.Distance(c.position, att.position); if (d < bd) { bd = d; best = i; } }
            bool ok = best >= 0 && casterAllowed[best];
            if (attCols == null) attCols = att.GetComponents<Collider>();
            foreach (var c in attCols) if (c != null && c.enabled != ok) c.enabled = ok;
            NearestTruck = best >= 0 ? casterTrucks[best] : null;
        }

        /// <summary>Name of the nearest truck in hitch reach (F3 page), or null.</summary>
        public string NearestTruck;
        private Collider[] attCols;

        /// <summary>True when the truck name matches the allowed list (Rustchief).</summary>
        public static bool Allowed(string truck)
        {
            var list = (S.AllowedTrucks ?? "").Split(',');
            foreach (var a in list) { var t = a.Trim(); if (t.Length == 0 || t == "*" || truck.StartsWith(t, System.StringComparison.OrdinalIgnoreCase)) return true; }
            return false;
        }

        /// <summary>Per physics step (module running, scene rig): ground recovery.</summary>
        private void FixedUpdate()
        {
            if (!VehiclesModule.Running) return;
            if (!gameObject.scene.IsValid()) return;
            if (rb == null) rb = GetComponent<Rigidbody>();
            groundRecovery.Tick(rb, Time.fixedDeltaTime);
        }

        /// <summary>Scene rigs: rebinds visuals and applies the layout once.</summary>
        private void Start()
        {
            if (!gameObject.scene.IsValid()) return;   // the template
            Rebind();
            Layout.Apply(gameObject, true);
        }

        /// <summary>Puts the shared mesh, materials, hulls and props back (a save may hand them back unresolved) and drops stray model nodes.</summary>
        public void Rebind()
        {
            try
            {
                var node = transform.Find(Factory.ModelNode);
                if (node == null) return;
                HideCollisionRenderers(node);
                var mf = node.GetComponent<MeshFilter>(); var mr = node.GetComponent<MeshRenderer>();
                var m = Factory.Model();
                if (mf != null && mf.sharedMesh != m.Mesh) mf.sharedMesh = m.Mesh;
                if (mr != null) { mr.enabled = true; mr.sharedMaterials = Factory.Materials(mr.sharedMaterial); }
                var hg = node.Find("warrig_hulls");
                if (hg != null)
                {
                    var hl = Factory.Hulls(); var mcs = hg.GetComponents<MeshCollider>();
                    for (int i = 0; i < mcs.Length && i < hl.Count; i++) if (mcs[i].sharedMesh != hl[i]) { mcs[i].convex = true; mcs[i].sharedMesh = hl[i]; }
                }
                Props.Rebind(gameObject);
                Props.Apply(gameObject);
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    var ch = transform.GetChild(i);
                    if (ch != node && ch.name == Factory.ModelNode) Destroy(ch.gameObject);
                }
            }
            catch (System.Exception e) { Log.Warn(VehiclesModule.Instance, "War Rig rebind: " + e.Message); }
        }

    }
}
