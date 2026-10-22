using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    // Debug helper: writes everything that could tell which faction an NPC / enemy belongs to
    // (tag, layer, parents, component types, FSM variables with telling names) into faction_dump.txt.
    internal static class FactionDump
    {
        static readonly string[] Telling = { "faction", "team", "group", "hostile", "enemy", "friend", "ally", "tribe", "clan", "relation", "type", "kind", "species", "side", "owner", "camp" };

        static bool Interesting(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            for (int i = 0; i < Telling.Length; i++) if (n.Contains(Telling[i])) return true;
            return false;
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (Transform c = t; c != null; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        public static string Run(Vector3 center, float radius, string file, string[] peopleWords)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Faction dump " + DateTime.Now + "  centre " + center + "  radius " + radius);

            // PlayMaker globals that might be faction related
            sb.AppendLine();
            sb.AppendLine("== Global variables with telling names ==");
            try
            {
                FsmVariables g = PlayMakerGlobals.Instance.Variables;
                foreach (FsmInt v in g.IntVariables) if (Interesting(v.Name)) sb.AppendLine("int    " + v.Name + " = " + v.Value);
                foreach (FsmBool v in g.BoolVariables) if (Interesting(v.Name)) sb.AppendLine("bool   " + v.Name + " = " + v.Value);
                foreach (FsmString v in g.StringVariables) if (Interesting(v.Name)) sb.AppendLine("string " + v.Name + " = " + v.Value);
                foreach (FsmFloat v in g.FloatVariables) if (Interesting(v.Name)) sb.AppendLine("float  " + v.Name + " = " + v.Value);
            }
            catch (Exception e) { sb.AppendLine("(failed: " + e.Message + ")"); }

            // FSMs on the game manager whose names look faction related, with their current state
            sb.AppendLine();
            sb.AppendLine("== FSMs named like a faction / relation (anywhere) ==");
            List<PlayMakerFSM> all = PlayMakerFSM.FsmList;
            var seen = new HashSet<int>();
            var near = new List<GameObject>();
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    PlayMakerFSM f = all[i];
                    if (f == null || f.gameObject == null) continue;
                    if (Interesting(f.FsmName) && f.FsmName != "Health")
                        sb.AppendLine(PathOf(f.transform) + "  [" + f.FsmName + "]  state=" + f.ActiveStateName);
                    GameObject go = f.gameObject;
                    if (!go.activeInHierarchy) continue;
                    if (Game.HDist(go.transform.position, center) > radius) continue;
                    string n = Util.Norm(go.name);
                    bool person = f.FsmName == "Health" || f.FsmName == "PlayerIsEnemy";
                    for (int k = 0; k < peopleWords.Length && !person; k++) if (peopleWords[k].Length > 0 && n.Contains(peopleWords[k])) person = true;
                    if (person && seen.Add(go.GetInstanceID())) near.Add(go);
                }
            }

            sb.AppendLine();
            sb.AppendLine("== Takeover walkers ==");
            foreach (string s in Takeover.DescribeWalkers(center)) sb.AppendLine(s);
            sb.AppendLine("== Camps taken over by quests ==");
            foreach (KeyValuePair<string, string> o in Factions.Overrides) sb.AppendLine(o.Key + " -> " + o.Value);

            sb.AppendLine();
            sb.AppendLine("== Points of interest within 300 m (name, owner from its EnemySpawn_ child) ==");
            foreach (Poi poi in Game.FindPois(center, 0f, 300f, ""))
                sb.AppendLine(poi.Name + "   owner=" + (poi.Faction.Length > 0 ? poi.Faction : "(none)") + "   dist " + (int)Game.HDist(poi.Pos, center) + " m");

            sb.AppendLine();
            sb.AppendLine("== Objects near you (" + near.Count + ", showing up to 30) ==");
            for (int i = 0; i < near.Count && i < 30; i++)
            {
                GameObject go = near[i];
                sb.AppendLine();
                sb.AppendLine("-- " + PathOf(go.transform) + "   dist " + (int)Game.HDist(go.transform.position, center) + " m");
                sb.AppendLine("   tag=" + go.tag + "  layer=" + LayerMask.LayerToName(go.layer) + "  active=" + go.activeInHierarchy);
                try
                {
                    var types = new List<string>();
                    foreach (Component c in go.GetComponents<Component>()) if (c != null) types.Add(c.GetType().FullName);
                    sb.AppendLine("   components: " + string.Join(", ", types.ToArray()));
                }
                catch (Exception e) { sb.AppendLine("   components: (failed " + e.Message + ")"); }

                try
                {
                    // T-pose check: is the model's animator running, does it have a controller, which state is it in
                    foreach (Animator an in go.GetComponentsInChildren<Animator>(true))
                    {
                        string st = "";
                        try { if (an.runtimeAnimatorController != null && an.isActiveAndEnabled) st = "  state-hash=" + an.GetCurrentAnimatorStateInfo(0).shortNameHash + " time=" + an.GetCurrentAnimatorStateInfo(0).normalizedTime.ToString("0.00"); } catch (Exception) { }
                        sb.AppendLine("   Animator on " + an.gameObject.name + ": enabled=" + an.enabled + " activeInHierarchy=" + an.gameObject.activeInHierarchy + " controller=" + (an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "NONE") + " speed=" + an.speed + st);
                    }
                }
                catch (Exception e) { sb.AppendLine("   Animator: (failed " + e.Message + ")"); }

                foreach (PlayMakerFSM f in go.GetComponents<PlayMakerFSM>())
                {
                    var extra = new List<string>();
                    FsmVariables v = f.FsmVariables;
                    foreach (FsmString s in v.StringVariables) if (Interesting(s.Name) || s.Name == "ItemName") extra.Add(s.Name + "=\"" + s.Value + "\"");
                    foreach (FsmInt s in v.IntVariables) if (Interesting(s.Name)) extra.Add(s.Name + "=" + s.Value);
                    foreach (FsmBool s in v.BoolVariables) if (Interesting(s.Name)) extra.Add(s.Name + "=" + s.Value);
                    sb.AppendLine("   FSM " + f.FsmName + " [" + f.ActiveStateName + "]" + (extra.Count > 0 ? "  " + string.Join(", ", extra.ToArray()) : ""));
                }
            }

            File.WriteAllText(file, sb.ToString());
            return file;
        }
    }
}
