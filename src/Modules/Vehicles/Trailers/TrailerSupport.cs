using System;
using System.Linq;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// Native trailers: a trailer is a separate saved vehicle whose ConfigurableJoint lives on the trailer, with the trailer's attacher and the
    /// towing frame's hitch as its two anchors. Detection, connection test, the attached-trailer query and the hitching itself.
    /// </summary>
    internal static class TrailerSupport
    {
        /// <summary>True for a native trailer (it carries a TrailerAttached FSM).</summary>
        internal static bool IsTrailer(GameObject go)
        {
            if (go == null) return false;
            Fs.Clear(); go.GetComponents(Fs); bool found = false;
            foreach (var f in Fs) if (f != null && f.FsmName == "TrailerAttached") { found = true; break; }
            Fs.Clear(); return found;
        }

        private static readonly System.Collections.Generic.List<ConfigurableJoint> Joints = new System.Collections.Generic.List<ConfigurableJoint>();
        private static readonly System.Collections.Generic.List<PlayMakerFSM> Fs = new System.Collections.Generic.List<PlayMakerFSM>();
        private static ConfigurableJoint[] _joints = new ConfigurableJoint[0]; private static int _jointsFrame = -1;
        /// <summary>True when the trailer's joint is connected to the car's rigidbody.</summary>
        internal static bool Connected(GameObject car, GameObject trailer)
        {
            if (car == null || trailer == null) return false;
            var rb = car.GetComponent<Rigidbody>();
            if (rb == null) return false;
            Joints.Clear(); trailer.GetComponents(Joints);   // Perf: List overload, no LINQ closure (per frame from RiderGuard)
            bool found = false; foreach (var j in Joints) if (j != null && j.connectedBody == rb) { found = true; break; }
            Joints.Clear(); return found;
        }

        /// <summary>The trailer hitched to the car: the Patrols marker's trailer, the car's trailer FSM variable, then one shared joint scan per frame.</summary>
        internal static GameObject Attached(GameObject car)
        {
            if (car == null) return null;
            foreach (var f in car.GetComponents<PlayMakerFSM>())
            {
                if (f == null || (f.FsmName != "TrailerAttach" && f.FsmName != "TrailerLoad")) continue;
                var v = f.FsmVariables.GetFsmGameObject("Trailer");
                if (v != null && IsTrailer(v.Value) && Connected(car, v.Value)) return v.Value;
            }
            var rb = car.GetComponent<Rigidbody>();
            if (rb == null) return null;
            // Perf: one scene-wide joint scan per frame shared by every caller (cleanup bursts, camp clearance checks)
            if (_jointsFrame != Time.frameCount) { _jointsFrame = Time.frameCount; _joints = UnityEngine.Object.FindObjectsOfType<ConfigurableJoint>(); }
            foreach (var j in _joints)
                if (j != null && j.connectedBody == rb && IsTrailer(j.gameObject)) return j.gameObject;
            return null;
        }

        /// <summary>Hitches the trailer to the car like the native attach (joint anchors, FSM variables, rear bumper/leg hidden, TrailerLoad off) and publishes TrailerAttached.</summary>
        internal static void Connect(GameObject car, GameObject trailer, bool preservePose = false)
        {
            var hitch = FindChild(car.transform, "hitch");
            var attacher = FindChild(trailer.transform, "attacher");
            var towRb = car.GetComponent<Rigidbody>();
            if (hitch == null || attacher == null || towRb == null || trailer.GetComponent<Rigidbody>() == null || !IsTrailer(trailer))
                throw new InvalidOperationException("The vehicle or trailer has no native hitch/attacher");
            var joint = trailer.GetComponent<ConfigurableJoint>();
            if (joint != null && joint.connectedBody != towRb) throw new InvalidOperationException("Trailer is attached to another vehicle");
            if (joint == null)
            {
                if (!preservePose)
                {
                    trailer.transform.rotation = car.transform.rotation;
                    trailer.transform.position += hitch.position - attacher.position;
                }
                joint = trailer.AddComponent<ConfigurableJoint>();
            }
            joint.connectedBody = towRb;
            joint.anchor = trailer.transform.InverseTransformPoint(attacher.position);
            joint.axis = Vector3.right;
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = car.transform.InverseTransformPoint(hitch.position);
            joint.secondaryAxis = Vector3.up;
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
            joint.rotationDriveMode = RotationDriveMode.XYAndZ;
            joint.projectionDistance = 0.1f; joint.projectionAngle = 180f;
            joint.enableCollision = true;

            foreach (var f in car.GetComponents<PlayMakerFSM>())
            {
                var attached = f.FsmVariables.GetFsmBool("trailerAttached");
                if (attached != null) attached.Value = true;
                if (f.FsmName != "TrailerAttach" && f.FsmName != "TrailerLoad") continue;
                var v = f.FsmVariables.GetFsmGameObject("Trailer"); if (v != null) v.Value = trailer;
            }
            var rearBumper = FindChild(car.transform, "hinge_bumper_rear");
            if (rearBumper != null) rearBumper.gameObject.SetActive(false);
            var leg = FindChild(trailer.transform, "leg"); if (leg != null) leg.gameObject.SetActive(false);
            // TrailerLoad would run its raycast recipe and add a second joint. Leave the interactive
            // TrailerAttach FSM enabled only by the player's native driving controls, as on any car.
            foreach (var f in car.GetComponents<PlayMakerFSM>()) if (f.FsmName == "TrailerLoad") f.enabled = false;
            if (ModEvents.HasListeners<TrailerAttached>())
            {
                var ev = new TrailerAttached { Truck = Entities.Of(car), Trailer = Entities.Of(trailer) };
                ModEvents.Publish(ref ev);
            }
        }

        /// <summary>Finds a named descendant, direct children first (same order as Patrols' helper).</summary>
        private static Transform FindChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++) { var c = root.GetChild(i); if (c.name == name) return c; }
            for (int i = 0; i < root.childCount; i++) { var r = FindChild(root.GetChild(i), name); if (r != null) return r; }
            return null;
        }
    }
}
