using WildWasteland.Core;
using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Brain part owned by Debug: NavDump trail pictures, the stuck look-ahead log and the status line.</summary>
    internal static partial class Brain
    {
        // [Debug] NavTrace (NavDump + VerboseLog): a line per moving NPC per second and a picture of its trail when a chase ends / it rests
        /// <summary>NavTrace on: NavDump and the NPC verbose log are both on.</summary>
        private static bool Tracing { get { return WildWasteland.Core.NavigationDiagnosticState.Capture && WildWasteland.Core.Log.L2; } }
        /// <summary>Writes a trace picture of the NPC's trail (at most every 15 s per NPC).</summary>
        private static void TraceDump(Npc n, string why)
        {
            if (!Tracing || n.Trail.Count < 2 || Time.time < n.NextTraceDump) return;
            n.NextTraceDump = Time.time + 15f;
            NpcaiRuntime.Deep("Trace: " + n.Owner.name + " picture (" + why + ")");
            try { Nav.TraceDump(n.Owner.name, n.Trail, n.TrailKind, n.TraceGoal); } catch (Exception e) { NpcaiRuntime.Error("Trace: " + e.Message); }
        }

        // [Debug] BrainLog, at a stuck: what is in front of the body (any layer, triggers too) - names the thing the feelers missed
        /// <summary>At a stuck (verbose log): logs what is in front of the body (any layer, triggers too).</summary>
        private static void LogAhead(Npc n)
        {
            try
            {
                Vector3 origin = n.Col != null ? new Vector3(n.Col.bounds.center.x, n.Col.bounds.min.y + 0.1f, n.Col.bounds.center.z) : n.T.position;
                var hits = Physics.RaycastAll(origin, n.T.forward, 1.5f, ~0, QueryTriggerInteraction.Collide);
                var sb = new System.Text.StringBuilder();
                foreach (var h in hits)
                {
                    if (h.collider == null || h.collider.transform.root == n.T) continue;
                    sb.Append(h.collider.name).Append(" [").Append(LayerMask.LayerToName(h.collider.gameObject.layer)).Append(h.collider.isTrigger ? ", trigger" : "")
                      .Append(", ").Append(h.distance.ToString("0.0")).Append(" m] ");
                }
                NpcaiRuntime.Deep("Brain: " + n.Owner.name + " ahead at ankle height: " + (sb.Length > 0 ? sb.ToString() : "nothing"));
                // what touches the body right now (the body capsule grown by 0.15 m, any layer, triggers too)
                if (n.Col != null)
                {
                    var b = n.Col.bounds;
                    float r = Mathf.Max(b.extents.x, b.extents.z) + 0.15f;
                    var touching = Physics.OverlapCapsule(new Vector3(b.center.x, b.min.y + r, b.center.z), new Vector3(b.center.x, b.max.y - r, b.center.z), r, ~0, QueryTriggerInteraction.Collide);
                    sb.Length = 0;
                    foreach (var c in touching)
                    {
                        if (c == null || c.transform.root == n.T) continue;
                        Vector3 cp = c.ClosestPoint(b.center);
                        Vector3 rel = n.T.InverseTransformPoint(cp);
                        sb.Append(c.name).Append(" [").Append(LayerMask.LayerToName(c.gameObject.layer)).Append(c.isTrigger ? ", trigger" : "").Append(", ")
                          .Append(c.GetType().Name).Append(", at ").Append(rel.x.ToString("0.0")).Append("/").Append(rel.y.ToString("0.0")).Append("/").Append(rel.z.ToString("0.0")).Append("] ");
                    }
                    NpcaiRuntime.Deep("Brain: " + n.Owner.name + " touching: " + (sb.Length > 0 ? sb.ToString() : "nothing") + " | vel " + (n.Rb != null ? n.Rb.velocity.magnitude.ToString("0.0") : "?") + " | fan free L " + n.FreeLeft.ToString("0.0") + " R " + n.FreeRight.ToString("0.0") + " side " + n.Side);
                }
            }
            catch (Exception) { }
        }

        /// <summary>Status line for the Debug page and the log.</summary>
        internal static string Status() { return _npcs.Count + " NPCs, " + _active + " engaged, tick " + _interval + " s"; }
    }
}
