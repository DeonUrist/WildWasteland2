using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ES3Internal;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Ragdoll pushes (launch every bone, throw a carcass); Presentation part of Patrol (Wave A split of Patrols Spawning/Patrol.cs).</summary>
    internal partial class Patrol
    {
        // Gives every bone of a ragdoll the same velocity (a shove), waking it up.
        /// <summary>Gives every non-kinematic bone of a ragdoll the same velocity (a shove), waking it up.</summary>
        internal static void Launch(Transform root, Vector3 velocity)
        {
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb.isKinematic) continue;
                rb.WakeUp();
                rb.velocity = velocity;
                rb.angularVelocity = Vector3.zero;
            }
        }

        // Moves a ragdoll as a whole (every rigidbody by the same offset) and gives all of it one velocity (zero = put down asleep).
        /// <summary>Moves a ragdoll as a whole and gives all of it one velocity (zero = put down asleep).</summary>
        internal static void Throw(Transform root, Vector3 to, Vector3 velocity)
        {
            var rbs = root.GetComponentsInChildren<Rigidbody>(true);
            Vector3 from = root.position;
            if (rbs.Length > 0)
            {
                from = Vector3.zero;
                foreach (var rb in rbs) from += rb.position;
                from /= rbs.Length;
            }
            var offset = to - from;
            root.position += offset;
            foreach (var rb in rbs)
            {
                if (rb.transform == root) continue;                  // already moved with the transform
                rb.position = rb.transform.position;                 // sync the physics position to the moved transform
            }
            foreach (var rb in rbs)
            {
                if (rb.isKinematic) continue;
                rb.velocity = velocity; rb.angularVelocity = Vector3.zero;
                if (velocity == Vector3.zero) rb.Sleep();
            }
        }
    }
}
