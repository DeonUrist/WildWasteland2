using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Pounce part owned by Presentation: the rear-up pose in LateUpdate and the airborne carcass tumble of a spider killed mid-leap.</summary>
    internal static partial class Pounce
    {
        // runs in LateUpdate, after the Animator: in the air the body rears back (nose up, about the feet) - the belly towards the player
        /// <summary>LateUpdate: an airborne spider rears back (belly towards the player).</summary>
        internal static void LateTick()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _list.Count; i++)
            {
                var p = _list[i];
                if (p.Rig == null) continue;
                if (p.Pitch == p.PitchGoal && !p.Tilted) continue;
                float rate = p.PitchGoal > p.Pitch ? 600f : 300f;
                p.Pitch = Mathf.MoveTowards(p.Pitch, p.PitchGoal, rate * dt);
                p.Rig.localRotation = Quaternion.AngleAxis(-p.Pitch, Vector3.right) * p.RigLocal;
                p.Tilted = p.Pitch > 0.01f;
                if (!p.Tilted) p.Rig.localRotation = p.RigLocal;
            }
        }

        // CreateObject.OnEnter postfix: the Health FSM's carcass of a spider that is in the air flies on, reared back and tumbling
        /// <summary>CreateObject.OnEnter postfix: the carcass of a spider killed mid-leap flies on, reared back and tumbling.</summary>
        public static void AfterCreateObject(CreateObject __instance)
        {
            if (Airborne == 0 || !NpcaiRuntime.Running) return;
            try
            {
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Health") return;
                var owner = fsm.GameObject;
                var made = __instance.storeObject != null ? __instance.storeObject.Value : null;
                if (owner == null || made == null) return;
                foreach (var p in _list)
                {
                    if (p.A == null || p.A.Owner != owner || (p.Phase != Phase.Air && p.Phase != Phase.Bite)) continue;
                    Vector3 vel = p.Rb.velocity;
                    made.transform.SetPositionAndRotation(p.T.position, p.T.rotation * Quaternion.AngleAxis(-p.Pitch, Vector3.right));
                    var rb = made.GetComponent<Rigidbody>();
                    if (rb == null) { rb = made.AddComponent<Rigidbody>(); rb.mass = Mathf.Max(0.05f, p.Rb.mass); UnityEngine.Object.Destroy(rb, 6f); }
                    if (rb.isKinematic) break;
                    rb.velocity = vel;
                    rb.angularVelocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(6f, 12f);
                    if (p.Ray != null && p.RayLen > 0f) { p.Ray.Length = p.RayLen; p.RayLen = 0f; }
                    Log(p, "killed in the air, the carcass tumbles on (" + vel.magnitude.ToString("0.0") + " m/s)");
                    break;
                }
            }
            catch (Exception e) { Core.Log.Warn(PresentationModule.Instance, "Pounce: carcass: " + e.Message); }
        }
    }
}
