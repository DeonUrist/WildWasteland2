using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Nests part: big spiders sitting in the hanging webs.</summary>
    internal static partial class Nests
    {
        // ---------- big spiders in the hanging webs ----------
        /// <summary>Seats the nest's sitter spiders in its hanging webs, one per web.</summary>
        private static void SeatAll(Nest n)
        {
            foreach (var w in n.Hanging)
            {
                bool taken = false; foreach (var s in n.Sitters) if (s.W == w) { taken = true; break; }
                if (taken) continue;
                Senses.Agent best = null; float bd = float.MaxValue;     // (1.7.5) any calm big spider of the nest, the nearest first
                foreach (var a in n.Spiders)
                {
                    if (a == null || a.Owner == null || !CanSit(a) || a.State != Senses.State.Idle || a.T.parent != null || a.Travel) continue;
                    bool sits = false; foreach (var s in n.Sitters) if (s.A == a) { sits = true; break; }
                    if (sits) continue;
                    float d = (a.T.position - w.Center).magnitude;
                    if (d < bd) { bd = d; best = a; }
                }
                if (best != null) Seat(n, best, w);
            }
        }

        /// <summary>Seats one spider in a web (kinematic body, brain held, cave-web sight rule).</summary>
        private static void Seat(Nest n, Senses.Agent a, Webs.Web w)
        {
            var rb = a.Owner.GetComponent<Rigidbody>();
            if (rb == null) return;
            // on the side of the web facing the middle of the nest, belly to the silk, head down
            Vector3 up = w.N; if (Vector3.Dot(n.C - w.Center, up) < 0f) up = -up;
            Vector3 fwd = Vector3.ProjectOnPlane(Vector3.down, up); if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up);
            var rot = Quaternion.LookRotation(fwd.normalized, up);
            var s = new Sit { A = a, W = w, Rb = rb, WasKinematic = rb.isKinematic };
            if (!rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
            Brain.SetTravel(a.Owner, true);
            Vector3 pos = w.Center + up * 0.02f;
            a.T.SetPositionAndRotation(pos, rot); rb.position = pos; rb.rotation = rot;
            n.Sitters.Add(s);
            a.CaveWeb = n.Cave;
            a.AmbushDist = Mathf.Max(0.3f, NpcaiSettings.NestSitterReach.Value);      // (1.7.5) in its web it strikes only at what comes within 1 m
            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Nests: " + a.Owner.name + " sits in a web at " + n.Name);
        }

        /// <summary>Sitting spiders drop and attack when the player (or a shot) comes within SitterReach.</summary>
        private static void Sitters(GameObject player, float now)
        {
            foreach (var n in _nests)
            {
                for (int i = n.Sitters.Count - 1; i >= 0; i--)
                {
                    var s = n.Sitters[i];
                    if (s.A == null || s.A.Owner == null) { n.Sitters.RemoveAt(i); continue; }
                    if (s.A.State != Senses.State.Idle || s.A.Blown) { Drop(s, player); n.Sitters.RemoveAt(i); }
                }
                if (n.Phase >= 5 && n.Sitters.Count < n.Hanging.Count && now >= n.NextAlarm) SeatAll(n);     // new / loaded spiders, calm again
            }
        }

        /// <summary>off the web: upright again, facing what it goes for, and falls to the ground under the web</summary>
        private static void Drop(Sit s, GameObject player)
        {
            if (s.A != null) s.A.CaveWeb = false;
            if (s.A == null || s.A.Owner == null) return;
            Vector3 f = s.A.Target != null ? s.A.Target.transform.position - s.A.T.position : (player != null ? player.transform.position - s.A.T.position : s.A.T.forward);
            f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            var rot = Quaternion.LookRotation(f.normalized, Vector3.up);
            s.A.T.rotation = rot;
            if (s.Rb != null) { s.Rb.rotation = rot; s.Rb.isKinematic = false; s.Rb.velocity = Vector3.zero; }
            Brain.SetTravel(s.A.Owner, false);
            if (s.A.AmbushDist > 0f && s.A.AmbushDist <= NpcaiSettings.NestSitterReach.Value + 0.01f) s.A.AmbushDist = 0f;   // off the web: the usual ambush distance again when calm
            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Nests: " + s.A.Owner.name + " drops from its web");
        }

    }
}
