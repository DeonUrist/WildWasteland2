using System.Linq;
using NWH.Common.CoM;
using NWH.WheelController3D;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    // Applies the config to one War Rig (the template, or a live one when a setting changes in Apocasetter).
    internal static class Layout
    {
        public static Quaternion Rot { get { return Quaternion.Euler(0f, S.Yaw, 0f); } }
        public static Vector3 Off { get { return S.V3(S.Offset, Vector3.zero); } }

        // model units -> trailer local space
        public static Vector3 M(Vector3 p) { return Rot * (p * S.Scale) + Off; }

        public static Vector3 HingePos(int i)
        {
            var z = S.Floats(S.Axles);
            float az = z.Length > i / 2 ? z[i / 2] : 0f;
            var c = M(new Vector3(0f, S.HubHeight, az));
            float side = (i % 2 == 0) ? -1f : 1f;           // L = trailer -x
            return c + new Vector3(side * S.AxleTrack * S.Scale, 0f, 0f);
        }

        public static void Apply(GameObject root, bool live)
        {
            float k = S.Scale;
            var node = root.transform.Find(Factory.ModelNode);
            if (node == null) return;
            node.localPosition = Off;
            node.localRotation = Rot;
            node.localScale = Vector3.one * k;

            // colliders: root box = tank (trailer space), child boxes = the rest (model space, scaled by the node)
            Vector3 c, s;
            bool hulls = node.Find("warrig_hulls") != null;
            var rootBox = root.GetComponent<BoxCollider>();
            if (rootBox != null)
            {
                if (S.Box(S.TankBox, out c, out s)) { rootBox.enabled = !hulls; rootBox.center = M(c); rootBox.size = Abs(Rot * (s * k)); }
                else rootBox.enabled = false;
            }
            var cols = node.Find("warrig_colliders");
            if (cols != null && hulls) foreach (var b in cols.GetComponents<BoxCollider>()) b.enabled = false;
            else if (cols != null)
            {
                var boxes = cols.GetComponents<BoxCollider>();
                var specs = new[] { S.FrameBox, S.FrontBox, S.RearBox };
                for (int i = 0; i < boxes.Length; i++)
                {
                    Vector3 bc, bs;
                    bool ok = S.Box(i < specs.Length ? specs[i] : null, out bc, out bs);
                    boxes[i].enabled = ok;
                    if (ok) { boxes[i].center = bc; boxes[i].size = bs; }
                }
            }

            // wheels
            for (int i = 0; i < Factory.Hinges.Length; i++)
            {
                var h = root.transform.Find(Factory.Hinges[i]);
                if (h == null) continue;
                h.localPosition = HingePos(i);
                var wc = h.GetComponent<WheelController>();
                if (wc == null) continue;
                if (S.SpringForce > 0f && wc.spring != null) wc.spring.maxForce = S.SpringForce;
                if (S.SpringLength > 0f && wc.spring != null) wc.spring.maxLength = S.SpringLength;
                if (S.DamperBump > 0f && wc.damper != null) wc.damper.bumpRate = S.DamperBump;
                if (S.DamperRebound > 0f && wc.damper != null) wc.damper.reboundRate = S.DamperRebound;
            }

            // hitch + leg
            var att = root.transform.Find("attacher");
            if (att != null)
            {
                att.localPosition = M(S.V3(S.Attacher, new Vector3(0f, -0.45f, -10.87f)));
                var sph = att.GetComponent<SphereCollider>(); if (sph != null) sph.radius = S.AttachRadius;
                if (live)
                    foreach (var j in root.GetComponents<ConfigurableJoint>())
                        if (j.connectedBody != null) j.anchor = att.localPosition;   // re-anchor a hitched joint on the rig
            }
            // parking legs: vanilla "leg" (toggled by the TrailerAttached FSM) + "leg2" mirrored
            var lp = S.V3(S.Leg, new Vector3(0.9f, -0.1f, -9.6f));
            float len = S.LegLength > 0f ? S.LegLength : AutoLeg(root, M(lp));
            Place(root.transform.Find("leg"), M(new Vector3(-Mathf.Abs(lp.x), lp.y, lp.z)), len);
            Place(root.transform.Find("leg2"), M(new Vector3(Mathf.Abs(lp.x), lp.y, lp.z)), len);

            // item lock zone (vanilla PhysicsLock: items whose +-10 m vertical rays hit this layer-18 box get parented to the rig)
            var lk = root.transform.Find("parts/PhysicsLock");
            if (lk != null)
            {
                var boxes = lk.GetComponents<BoxCollider>();
                Vector3 lc, ls;
                bool lok = S.Box(S.LockBox, out lc, out ls);
                for (int i = 0; i < boxes.Length; i++) boxes[i].enabled = lok && i == 0;
                if (lok && boxes.Length > 0)
                {
                    boxes[0].center = lk.InverseTransformPoint(root.transform.TransformPoint(M(lc)));
                    boxes[0].size = Abs(lk.InverseTransformVector(root.transform.TransformVector(Abs(Rot * (ls * k)))));
                }
            }

            Props.Apply(root);

            // mass / centre of mass / inertia (box approximation from the tank collider)
            float mass = S.Mass;
            var com = root.GetComponent<VariableCenterOfMass>();
            var cm = M(S.V3(S.CenterOfMass, new Vector3(0f, 0.3f, -1.2f)));
            Vector3 dim = rootBox != null ? rootBox.size : new Vector3(2.5f, 1.5f, 9.5f);
            var inertia = new Vector3(mass / 12f * (dim.y * dim.y + dim.z * dim.z), mass / 12f * (dim.x * dim.x + dim.z * dim.z), mass / 12f * (dim.x * dim.x + dim.y * dim.y));
            if (com != null)
            {
                com.useDefaultMass = false; com.useDefaultCenterOfMass = false; com.useDefaultInertia = false;
                com.baseMass = mass; com.combinedMass = mass;
                com.centerOfMass = cm; com.combinedCenterOfMass = cm;
                com.dimensions = dim;
                com.inertiaTensor = inertia; com.combinedInertiaTensor = inertia;
            }
            var rb = root.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = mass; rb.centerOfMass = cm;
                rb.inertiaTensor = inertia; rb.inertiaTensorRotation = Quaternion.identity;
                if (live) rb.WakeUp();
            }
        }

        public static float LastLegLength;

        private static void Place(Transform leg, Vector3 pos, float len)
        {
            if (leg == null) return;
            leg.localPosition = pos;
            // vanilla leg: z points down after its +90 deg X rotation; its collider/mesh reach 1.13 * z-scale below the pivot
            var sc = leg.localScale;
            leg.localScale = new Vector3(sc.x, sc.y, len / 1.13f);
        }

        // Leg length so that, parked on the legs and the middle axle, the hitch point sits HitchHeight above the ground.
        private static float AutoLeg(GameObject root, Vector3 legTop)
        {
            var mid = HingePos(2);
            float r = 0.54f;   // truck_wheel_1
            var h = root.transform.Find(Factory.Hinges[2]);
            var wc = h != null ? h.GetComponent<WheelController>() : null;
            if (wc != null && wc.Radius > 0.1f) r = wc.Radius;
            float ground = mid.y - r;
            var att = M(S.V3(S.Attacher, new Vector3(0f, -0.45f, -10.87f)));
            float arm = Mathf.Abs(att.z - mid.z);
            float drop = (att.y - ground) - S.HitchHeight;
            float slope = arm > 0.5f ? drop / arm : 0f;
            float len = (legTop.y - ground) - slope * Mathf.Abs(legTop.z - mid.z);
            LastLegLength = Mathf.Clamp(len, 0.2f, 3f);
            return LastLegLength;
        }

        private static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }
    }
}
