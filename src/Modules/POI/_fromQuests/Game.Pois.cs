using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Quests world helpers, POI part (Wave A, from Quests/Game.cs; B13): camps/caves/buildings/wrecks in the loaded tiles, the human faction that owns a camp's spawners, and visited map icons.</summary>
    internal static partial class Game
    {
        // ---- points of interest and enemies -------------------------------------------

        static readonly string[] PoiKinds = { "Camp", "Cave", "Building", "Wreck" };

        // The game marks who a camp belongs to with child objects named after the faction: EnemySpawn_Coyotes_V1,
        // CoyotesMerchantSpawn_V1, EnemySpawn_Scrapyard_V1 ... Only human factions (see Factions.HumanFactions) count;
        // the wild / burrower spawners are animals and mutants.
        internal static string SpawnFactionOf(Transform poi)
        {
            for (int i = 0; i < poi.childCount; i++)
            {
                string n = Util.Norm(poi.GetChild(i).name);
                for (int h = 0; h < Factions.HumanFactions.Length; h++)
                {
                    string hf = Factions.HumanFactions[h];
                    if (hf.Length > 0 && n.Contains(hf)) return char.ToUpperInvariant(hf[0]) + hf.Substring(1);
                }
            }
            return "";
        }

        // Camps, caves, buildings and wrecks in the loaded MapMagic tiles whose horizontal distance
        // from `center` is between minD and maxD. kindFilter ("" = any) is a word like "cave".
        public static List<Poi> FindPois(Vector3 center, float minD, float maxD, string kindFilter)
        {
            var list = new List<Poi>(); string filter = Util.Norm(kindFilter);
            foreach (var p in QuestWorld.Pois)
            {
                if (p.Obj == null || !p.Obj.gameObject.activeInHierarchy) continue;
                p.Pos = p.Obj.position;
                if (filter.Length > 0 && !Util.Norm(p.Name).Contains(filter) && !Util.Norm(p.Kind).Contains(filter)) continue;
                float d = HDist(p.Pos, center); if (d >= minD && d <= maxD) list.Add(p);
            }
            return list;
        }

        // ---- places the player has been to ----------------------------------------------------------
        // World positions of the map markers that the game has turned yellow = places the player has visited (the game's
        // Visited FSM does this; Immersive Map reads it the same way). Found by type name so the TextMeshPro DLL is not needed.

        public static List<Vector3> VisitedIcons()
        {
            var result = new List<Vector3>();
            foreach (var icon in QuestWorld.Icons) if (icon != null) { var c = icon.color; if (c.r > .8f && c.g > .8f && c.b < .5f) result.Add(icon.transform.position); }
            return result;
        }

        public static bool NearAny(List<Vector3> points, Vector3 p, float radius)
        {
            for (int i = 0; i < points.Count; i++) if (HDist(points[i], p) <= radius) return true;
            return false;
        }
    }
}
