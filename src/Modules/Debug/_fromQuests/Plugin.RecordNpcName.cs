using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Quests engine, Debug part (Wave A, from Quests/Plugin.cs; B03): the [Quests / Debug] RecordNpcNames recorder that logs each NPC talked to and appends npc_names.txt / npc_locations.txt.</summary>
    internal sealed partial class Plugin : EngineSettings
    {
        readonly HashSet<string> seenNpcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // With [Debug] RecordNpcNames on: logs each NPC you talk to and keeps running lists in npc_names.txt and
        // npc_locations.txt next to the DLL (use these words in the 'giver' key of quests.txt).
        void RecordNpcName(string npc)
        {
            if (!recordNpcs.Value) return;
            try
            {
                if (string.IsNullOrEmpty(npc)) return;
                Info("Talking to NPC: \"" + npc + "\"");
                if (seenNpcs.Add(npc))
                {
                    string path = Path.Combine(DataDir, "npc_names.txt");
                    var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (File.Exists(path)) foreach (string l in File.ReadAllLines(path)) known.Add(l.Trim());
                    if (known.Add(npc)) File.AppendAllText(path, npc + Environment.NewLine);
                }
            }
            catch (Exception e) { Warn("Could not record NPC name: " + e.Message); }
            try
            {
                // which camp / cave / building is this NPC standing at? (helps you fill in factions.txt)
                Transform t = bridge.Npc;
                if (t == null || string.IsNullOrEmpty(npc)) return;
                List<Poi> near = Game.FindPois(t.position, 0f, 150f, "");
                if (near.Count == 0) return;
                Poi best = near[0];
                float bd = Game.HDist(best.Pos, t.position);
                for (int i = 1; i < near.Count; i++)
                {
                    float d = Game.HDist(near[i].Pos, t.position);
                    if (d < bd) { bd = d; best = near[i]; }
                }
                string entry = npc + " @ " + best.Name;
                Faction nf = Factions.OfObject(t.gameObject.tag, npc, t.position);
                Info("NPC location: " + entry + " (" + (int)bd + " m)  faction: " + (nf != null ? nf.Name : "none"));
                string lpath = Path.Combine(DataDir, "npc_locations.txt");
                var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(lpath)) foreach (string l in File.ReadAllLines(lpath)) have.Add(l.Trim());
                if (have.Add(entry)) File.AppendAllText(lpath, entry + Environment.NewLine);
            }
            catch (Exception e) { Warn("Could not record NPC name: " + e.Message); }
        }
    }
}
