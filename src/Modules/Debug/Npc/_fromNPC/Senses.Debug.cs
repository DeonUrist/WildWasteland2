using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Senses part owned by Debug: the per-agent log line, the status line and the ghost / state world overlay (DrawDebug).</summary>
    internal static partial class Senses
    {
        private static void Log(Agent a, string s)
        {
            if (NpcModule.VerboseLog.Value) NpcModule.Log.LogInfo("Senses: " + a.Owner.name + " " + s);
        }

        internal static string Status() { return _agents.Count + " NPCs, " + _ghosts.Count + " ghosts"; }

        private static readonly Color[] SrcColor = { Color.gray, new Color(0.4f, 0.6f, 1f), new Color(0.4f, 0.9f, 0.4f), new Color(1f, 0.9f, 0.2f), new Color(1f, 0.55f, 0.15f), new Color(1f, 0.3f, 0.9f), new Color(1f, 0.25f, 0.2f) };
        internal static void DrawDebug()
        {
            if (!On || !NpcModule.ShowNav.Value) return;
            var cam = Core.GameRefs.MainCamera; if (cam == null) return;
            float now = Time.time, r2 = DebugOverlay.Range * DebugOverlay.Range; Vector3 cp = cam.transform.position;   // (WW perf) no label strings for what is not drawn
            foreach (var g in _ghosts)
            {
                if ((g.Pos - cp).sqrMagnitude > r2) continue;
                var c = g.Retired ? Color.gray : SrcColor[(int)g.Src];
                DebugOverlay.Mark(g.Pos + Vector3.up * 1.2f, c, 14f);
                DebugOverlay.Label(g.Pos + Vector3.up * 1.6f, "#" + g.Id + " " + g.Src.ToString().ToUpperInvariant() + (g.Retired ? " (searched)" : "") + " " + g.About + "  (" + g.Holders.Count + ", " + (now - g.Born).ToString("0") + " s)", c);
            }
            foreach (var kv in _agents)
            {
                var a = kv.Value;
                if (a.Owner == null || a.State == State.Idle || (a.T.position - cp).sqrMagnitude > r2) continue;
                string s; Color c;
                switch (a.State)
                {
                    case State.Combat: s = "COMBAT " + Name(a.Target); c = SrcColor[(int)Src.Vision]; break;
                    case State.Hide: s = "HIDING from " + Name(a.Target); c = new Color(0.4f, 0.8f, 1f); break;
                    case State.Investigate: s = (a.Flanking ? "flanks " : "-> ") + "ghost #" + (a.Ghost != null ? a.Ghost.Id.ToString() : "?") + " (" + (Rank(a.GhostPrio) == 2 ? "seen" : "heard") + ", " + a.GhostPrio.ToString().ToLowerInvariant() + ")"; c = a.Ghost != null ? SrcColor[(int)a.Ghost.Src] : Color.white; break;
                    default: s = "SEARCH " + Mathf.Max(0f, a.SearchUntil - now).ToString("0") + " s"; c = new Color(0.75f, 0.6f, 1f); break;
                }
                Vector3 top = a.Col != null ? new Vector3(a.Col.bounds.center.x, a.Col.bounds.max.y, a.Col.bounds.center.z) : a.T.position + Vector3.up * 2f;
                DebugOverlay.Label(top + Vector3.up * 0.35f, Prefab(a.Owner.name) + ": " + s, c);
            }
        }
    }
}
