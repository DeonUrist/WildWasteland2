using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Optimization;

namespace WildWasteland.Modules.Debug
{
    /// <summary>On-demand FSM/state/action census. No scene scan occurs during ordinary measurement ticks.</summary>
    internal static class DebugCensus
    {
        /// <summary>FSM census (count + active actions per FSM name and state) and scene counters into a text file. Allocates; on request only.</summary>
        internal static string WriteCensus()
        {
            try
            {
                var by = new Dictionary<string, int[]>();
                var list = PlayMakerFSM.FsmList;
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i]; if (f == null) continue;
                    var fsm = f.Fsm; if (fsm == null) continue;
                    string key = fsm.Name + " | " + (fsm.ActiveStateName ?? "-");
                    int[] c; if (!by.TryGetValue(key, out c)) { c = new int[3]; by[key] = c; }
                    c[0]++; var st = fsm.ActiveState; if (st != null) c[1] += st.ActiveActions.Count;
                    if (fsm.ManualUpdate) c[2]++;
                }
                var rows = new List<KeyValuePair<string, int[]>>(by);
                rows.Sort((a, b) => b.Value[0].CompareTo(a.Value[0]));
                var sb = new StringBuilder();
                sb.Append("Wild Wasteland Debug census ").Append(DateTime.Now.ToString("s", CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("FsmList ").Append(list.Count).Append(", preset ").Append(OptimizationRuntime.Preset).Append('\n');
                if (OptimizationRuntime.Instance != null) foreach (var f in OptimizationRuntime.Instance.Features) sb.Append(f.PlanId).Append(' ').Append(f.Key).Append(": ").Append(f.Running).Append(f.Mismatches > 0 ? " mismatches " + f.Mismatches : "").Append('\n');
                sb.Append("lights ").Append(UnityEngine.Object.FindObjectsOfType<Light>().Length).Append(", rigidbodies ").Append(UnityEngine.Object.FindObjectsOfType<Rigidbody>().Length).Append('\n');
                sb.Append("\ncount\tactive actions\tsleeping\tfsm | state\n");
                foreach (var r in rows) sb.Append(r.Value[0]).Append('\t').Append(r.Value[1]).Append('\t').Append(r.Value[2]).Append('\t').Append(r.Key).Append('\n');
                string dir = Path.Combine(Assets.ConfigDir, "Debug");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "census-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".txt");
                File.WriteAllText(path, sb.ToString());
                return path;
            }
            catch (Exception e) { Log.Error("[Performance] census", e); return null; }
        }
    }
}
