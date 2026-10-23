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
        /// <summary>Publishes copied NPC name/home/faction data only while Debug's authoring recorder requests it.</summary>
        void RecordNpcName(string npc)
        {
            if (!QuestDiagnosticState.RecordNpcNames || string.IsNullOrEmpty(npc)) return;
            var identity = new NpcDiagnosticIdentity { Name = npc };
            Transform target = bridge.Npc;
            if (target != null)
            {
                identity.World = Origin.World(target.position);
                List<Poi> near = Game.FindPois(target.position, 0f, 150f, "");
                if (near.Count > 0)
                {
                    Poi best = near[0]; float distance = Core.WorldGeometry.HorizontalDistance(best.Pos, target.position);
                    for (int i = 1; i < near.Count; i++)
                    {
                        float candidate = Core.WorldGeometry.HorizontalDistance(near[i].Pos, target.position);
                        if (candidate < distance) { distance = candidate; best = near[i]; }
                    }
                    identity.Location = best.Name; identity.Distance = distance;
                    Faction faction = Factions.OfObject(target.gameObject.tag, npc, target.position);
                    identity.Faction = faction != null ? faction.Name : "none";
                }
            }
            QuestDiagnosticState.Publish(identity);
        }
    }

    /// <summary>Copies quest-owned takeover/POI details for Debug's explicit faction dump.</summary>
    internal sealed class QuestDiagnosticSource : IQuestDiagnosticSource
    {
        /// <summary>Provides a copy so Debug cannot mutate quest classification data.</summary>
        public string[] PeopleWords { get { return (string[])Factions.PeopleWords.Clone(); } }
        /// <summary>Formats the same diagnostic context without exposing quest DTOs or collections.</summary>
        public string DescribeFactionContext(Vector3 sceneCenter)
        {
            var text = new StringBuilder();
            text.AppendLine("== Takeover reinforcements use Patrols traveler journeys ==");
            text.AppendLine("== Camps taken over by quests ==");
            foreach (var pair in Factions.Overrides) text.AppendLine(pair.Key + " -> " + pair.Value);
            text.AppendLine().AppendLine("== Points of interest within 300 m (name, camp/town owner) ==");
            foreach (Poi poi in Game.FindPois(sceneCenter, 0f, 300f, ""))
                text.AppendLine(poi.Name + "   owner=" + (poi.Faction.Length > 0 ? poi.Faction : "(none)") + "   dist " + (int)Core.WorldGeometry.HorizontalDistance(poi.Pos, sceneCenter) + " m");
            return text.ToString();
        }
    }
}
